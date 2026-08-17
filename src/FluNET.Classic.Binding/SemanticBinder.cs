using System.Reflection;
using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Binding;

public sealed class SemanticBinder
{
    private readonly LanguageSnapshot _language;
    private readonly ValueResolverRegistry _resolvers;
    private readonly ValueConversionRegistry _conversions;
    private readonly PredicateRegistry _predicates;
    private readonly IServiceProvider? _services;
    private readonly List<BindingDiagnostic> _diagnostics = [];

    public SemanticBinder(LanguageSnapshot language, ValueResolverRegistry resolvers, ValueConversionRegistry conversions, PredicateRegistry predicates, IServiceProvider? services = null)
    {
        _language = language;
        _resolvers = resolvers;
        _conversions = conversions;
        _predicates = predicates;
        _services = services;
    }

    public BoundScript Bind(ScriptNode script, IReadOnlyDictionary<string, Type>? initialVariables = null)
    {
        _diagnostics.Clear();
        var symbols = new SymbolScope(null);
        if (initialVariables is not null)
            foreach ((string n, Type t) in initialVariables) symbols.Define(n, t);
        BoundStatement[] statements = script.Statements.Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray();
        return new(statements, _diagnostics.ToArray());
    }

    private BoundStatement? BindStatement(StatementNode s, SymbolScope symbols) => s switch
    {
        PipelineNode p => BindPipeline(p, symbols),
        IfNode i => BindIf(i, symbols),
        ForEachNode f => BindForEach(f, symbols),
        _ => null
    };

    private BoundPipeline BindPipeline(PipelineNode p, SymbolScope symbols)
    {
        var stages = new List<BoundStage>();
        Type? pipelineType = null;
        foreach (PipelineStageNode stage in p.Stages)
        {
            BoundStage? bound = stage switch
            {
                SentenceNode s => BindSentence(s, symbols, pipelineType),
                FilterStageNode f => BindFilter(f, symbols, pipelineType),
                CheckStageNode c => BindCheck(c, symbols),
                _ => null
            };
            if (bound is null) continue;
            stages.Add(bound);
            pipelineType = bound.ResultType;
            if (bound is BoundSentence sentence) RegisterOutputs(sentence, symbols);
            if (bound is BoundFilter { ResultAlias: { Length: > 0 } filterAlias }) symbols.Define(filterAlias, bound.ResultType);
            if (bound is BoundCheck { ResultAlias: { Length: > 0 } checkAlias }) symbols.Define(checkAlias, typeof(bool));
        }
        return new(stages, pipelineType, p.Span);
    }

    private BoundSentence? BindSentence(SentenceNode sentence, SymbolScope symbols, Type? pipelineType)
    {
        if (!_language.TryGetVerb(sentence.Verb, out VerbDescriptor verb))
        {
            _diagnostics.Add(new("FLU-BIND-001", $"Unknown verb '{sentence.Verb}'.", sentence.Span));
            return null;
        }
        QualifierDescriptor? qualifier = null;
        if (sentence.Qualifier is not null && !_language.TryGetQualifier(sentence.Qualifier, out qualifier!))
        {
            _diagnostics.Add(new("FLU-BIND-002", $"Unknown qualifier '{sentence.Qualifier}'.", sentence.Span));
            return null;
        }

        var candidates = new List<Candidate>();
        var rejected = new List<string>();
        foreach (VerbImplementationDescriptor implementation in verb.Implementations)
        foreach (SentencePattern pattern in implementation.Patterns)
        {
            if (!QualifierMatches(qualifier, implementation, pattern))
            {
                rejected.Add($"{Signature(implementation, pattern)}: qualifier mismatch");
                continue;
            }
            CandidateResult attempt = TryCandidate(sentence, verb, implementation, pattern, symbols, pipelineType, qualifier);
            if (attempt.Candidate is not null) candidates.Add(attempt.Candidate);
            else rejected.Add($"{Signature(implementation, pattern)}: {attempt.Reason}");
        }

        if (candidates.Count == 0)
        {
            _diagnostics.Add(new("FLU-BIND-010", $"No overload of {verb.Name} matches this sentence.", sentence.Span, rejected));
            return null;
        }
        Candidate[] ordered = candidates.OrderBy(x => x.Cost).ThenBy(x => x.Implementation.StableId, StringComparer.Ordinal).ThenBy(x => x.Pattern.StableId, StringComparer.Ordinal).ToArray();
        if (ordered.Length > 1 && ordered[0].Cost == ordered[1].Cost)
        {
            _diagnostics.Add(new("FLU-BIND-011", $"Ambiguous overload for {verb.Name} at cost {ordered[0].Cost}.", sentence.Span, ordered.Where(x => x.Cost == ordered[0].Cost).Select(x => Signature(x.Implementation, x.Pattern)).ToArray()));
            return null;
        }
        Candidate selected = ordered[0];
        return new(verb, selected.Implementation, selected.Pattern, selected.Roles, sentence.ResultAlias, selected.Cost, sentence.Span);
    }

    private CandidateResult TryCandidate(SentenceNode sentence, VerbDescriptor verb, VerbImplementationDescriptor implementation, SentencePattern pattern, SymbolScope symbols, Type? pipelineType, QualifierDescriptor? qualifier)
    {
        var supplied = new Dictionary<string, Queue<ExpressionNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (ClauseNode clause in sentence.Clauses)
        {
            RoleSlotDescriptor[] matches = pattern.Roles
                .Where(role => role.AllSurfaceNames.Contains(clause.RoleName, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0) return CandidateResult.Fail($"role {clause.RoleName} is not valid for this pattern");
            if (matches.Length > 1) return CandidateResult.Fail($"role {clause.RoleName} is ambiguous in this pattern");
            if (!supplied.TryGetValue(matches[0].Name, out Queue<ExpressionNode>? values))
                supplied[matches[0].Name] = values = new Queue<ExpressionNode>();
            foreach (ExpressionNode value in clause.Values) values.Enqueue(value);
        }
        var roles = new List<BoundRole>();
        int cost = pattern.Roles.Count(x => x.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore);
        bool aliasUsedAsOutput = false;
        bool pipelineUsed = false;

        foreach (RoleSlotDescriptor slot in pattern.Roles.OrderBy(x => x.Position))
        {
            supplied.TryGetValue(slot.Name, out Queue<ExpressionNode>? queue);
            queue ??= new Queue<ExpressionNode>();
            var expressions = new List<ExpressionNode>();
            if (slot.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore)
                while (queue.Count > 0) expressions.Add(queue.Dequeue());
            else if (queue.Count > 0)
                expressions.Add(queue.Dequeue());

            if (expressions.Count == 0 && slot.Direction is RoleDirection.Output or RoleDirection.InputOutput && !aliasUsedAsOutput && sentence.ResultAlias is not null)
            {
                expressions.Add(new VariableExpression(sentence.ResultAlias, sentence.Span));
                aliasUsedAsOutput = true;
            }

            if (expressions.Count == 0 && slot.Direction == RoleDirection.Input && pipelineType is not null && slot.Required && !pipelineUsed && TryPlanConversion(pipelineType, SlotBindingType(slot), out ConversionKind pipelineKind, out int pipelineCost))
            {
                BoundValue pipeline = new BoundPipelineValue(pipelineType, sentence.Span);
                if (pipelineKind != ConversionKind.Exact) pipeline = new BoundConversionValue(pipeline, SlotBindingType(slot), pipelineKind, pipelineCost, sentence.Span);
                roles.Add(new(slot, new[] { pipeline }, sentence.Span));
                cost += pipelineCost + 1;
                pipelineUsed = true;
                continue;
            }

            if (expressions.Count == 0)
            {
                if (slot.Direction == RoleDirection.Output) continue;
                if (slot.Required && slot.Cardinality is RoleCardinality.One or RoleCardinality.OneOrMore) return CandidateResult.Fail($"missing {slot.Name}");
                continue;
            }

            var values = new List<BoundValue>();
            Type expected = SlotBindingType(slot);
            foreach (ExpressionNode expression in expressions)
            {
                if (!TryBindValue(expression, expected, slot.Direction, verb.Name, qualifier?.Name, symbols, out BoundValue? value, out int valueCost))
                    return CandidateResult.Fail($"cannot bind {slot.Name} value to {expected.Name}");
                values.Add(value!);
                cost += valueCost;
            }
            roles.Add(new(slot, values, sentence.Span));
        }

        if (supplied.Values.Any(x => x.Count > 0)) return CandidateResult.Fail("too many role values");
        if (sentence.ResultAlias is not null && !aliasUsedAsOutput) cost += 1;
        return CandidateResult.Ok(new Candidate(implementation, pattern, roles, cost));
    }

    private BoundFilter? BindFilter(FilterStageNode filter, SymbolScope symbols, Type? pipelineType)
    {
        BoundValue? source;
        if (filter.Source is null)
        {
            if (pipelineType is null)
            {
                _diagnostics.Add(new("FLU-BIND-120", "FILTER requires a source or pipeline value.", filter.Span));
                return null;
            }
            source = new BoundPipelineValue(pipelineType, filter.Span);
        }
        else if (!TryBindValue(filter.Source, null, RoleDirection.Input, "FILTER", null, symbols, out source, out _))
        {
            _diagnostics.Add(new("FLU-BIND-121", "FILTER source cannot be bound.", filter.Source.Span));
            return null;
        }
        Type? element = ClrTypeShape.GetElementType(source!.Type);
        if (element is null)
        {
            _diagnostics.Add(new("FLU-BIND-122", $"FILTER requires a collection, got {source.Type.Name}.", filter.Source?.Span ?? filter.Span));
            return null;
        }
        BoundExpression predicate = BindExpression(filter.Predicate, symbols, element);
        if (predicate.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-123", "WHERE expression must be BOOLEAN.", filter.Predicate.Span));
        return new(source, predicate, element, filter.ResultAlias, filter.Span);
    }

    private BoundCheck BindCheck(CheckStageNode check, SymbolScope symbols)
    {
        BoundExpression condition = BindExpression(check.Condition, symbols, null);
        if (condition.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-124", "CHECK IF condition must be BOOLEAN.", check.Condition.Span));
        return new(condition, check.ResultAlias, check.Span);
    }

    private BoundIf BindIf(IfNode node, SymbolScope symbols)
    {
        BoundExpression condition = BindExpression(node.Condition, symbols, null);
        if (condition.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-130", "IF condition must be BOOLEAN.", node.Condition.Span));
        return new(condition, BindBlock(node.Then, new SymbolScope(symbols)), node.Else is null ? null : BindBlock(node.Else, new SymbolScope(symbols)), node.Span);
    }

    private BoundForEach? BindForEach(ForEachNode node, SymbolScope symbols)
    {
        if (!TryBindValue(node.Source, null, RoleDirection.Input, "FOR EACH", null, symbols, out BoundValue? source, out _))
        {
            _diagnostics.Add(new("FLU-BIND-140", "FOR EACH source cannot be bound.", node.Source.Span));
            return null;
        }
        Type? element = ClrTypeShape.GetElementType(source!.Type);
        if (element is null)
        {
            _diagnostics.Add(new("FLU-BIND-141", $"FOR EACH requires a collection, got {source.Type.Name}.", node.Source.Span));
            return null;
        }
        var child = new SymbolScope(symbols);
        child.Define(node.Variable, element);
        return new(node.Variable, source, element, BindBlock(node.Body, child), node.Span);
    }

    private BoundBlock BindBlock(BlockNode block, SymbolScope symbols) => new(block.Statements.Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray(), block.Span);

    private BoundExpression BindExpression(ExpressionNode expression, SymbolScope symbols, Type? itemType) => expression switch
    {
        FluNET.Classic.Syntax.BinaryExpression binary => BindBinary(binary, symbols, itemType),
        FluNET.Classic.Syntax.UnaryExpression unary => new BoundUnaryExpression(unary.Operator, BindExpression(unary.Operand, symbols, itemType), typeof(bool), unary.Span),
        PredicateExpression predicate => BindPredicate(predicate, symbols, itemType),
        IdentifierExpression identifier when itemType is not null => BindItemProperty(identifier, itemType),
        _ => TryBindValue(expression, null, RoleDirection.Input, "EXPRESSION", null, symbols, out BoundValue? value, out _)
            ? new BoundValueExpression(value!, expression.Span)
            : ErrorExpression(expression)
    };

    private BoundExpression BindPredicate(PredicateExpression predicate, SymbolScope symbols, Type? itemType)
    {
        BoundExpression operand;
        if (predicate.Predicate.Equals("EXISTS", StringComparison.OrdinalIgnoreCase) && predicate.Operand is ReferenceExpression reference &&
            TryBindValue(reference, typeof(FileInfo), RoleDirection.Input, "EXPRESSION", null, symbols, out BoundValue? file, out _))
        {
            operand = new BoundValueExpression(file!, reference.Span);
        }
        else
        {
            operand = BindExpression(predicate.Operand, symbols, itemType);
        }

        if (!_predicates.CanEvaluate(predicate.Predicate, operand.Type))
            _diagnostics.Add(new("FLU-BIND-152", $"Predicate '{predicate.Predicate}' cannot evaluate {operand.Type.Name}.", predicate.Span));
        return new BoundPredicateExpression(predicate.Predicate, operand, predicate.Span);
    }

    private BoundExpression BindBinary(FluNET.Classic.Syntax.BinaryExpression binary, SymbolScope symbols, Type? itemType)
    {
        BoundExpression left = BindExpression(binary.Left, symbols, itemType);
        BoundExpression right = BindExpression(binary.Right, symbols, itemType);
        string op = binary.Operator.ToUpperInvariant();
        Type result = op is "AND" or "OR" or "=" or "==" or "!=" or ">" or "<" or ">=" or "<=" or "IS" or "IS NOT" ? typeof(bool) : left.Type;
        return new BoundBinaryExpression(left, op, right, result, binary.Span);
    }

    private BoundExpression BindItemProperty(IdentifierExpression identifier, Type itemType)
    {
        if (!TryCompileAccessor(itemType, identifier.Name, out Type? propertyType, out Func<object, object?>? accessor))
        {
            _diagnostics.Add(new("FLU-BIND-150", $"'{itemType.Name}' has no property '{identifier.Name}'.", identifier.Span));
            return new BoundValueExpression(new BoundConstantValue(null, typeof(object), identifier.Span), identifier.Span);
        }
        return new BoundItemPropertyExpression(identifier.Name, propertyType!, accessor!, identifier.Span);
    }

    private BoundExpression ErrorExpression(ExpressionNode expression)
    {
        _diagnostics.Add(new("FLU-BIND-151", "Expression cannot be bound.", expression.Span));
        return new BoundValueExpression(new BoundConstantValue(null, typeof(object), expression.Span), expression.Span);
    }

    private bool TryBindValue(ExpressionNode expression, Type? expected, RoleDirection direction, string verb, string? qualifier, SymbolScope symbols, out BoundValue? bound, out int cost)
    {
        if (direction == RoleDirection.Output)
        {
            if (expression is VariableExpression output)
            {
                bound = new BoundVariableValue(output.Name, expected ?? typeof(object), true, output.Span);
                cost = 0;
                return true;
            }
            bound = null;
            cost = 0;
            return false;
        }

        switch (expression)
        {
            case VariableExpression variable:
                if (!symbols.TryGet(variable.Name, out Type variableType)) { bound = null; cost = 0; return false; }
                bound = new BoundVariableValue(variable.Name, variableType, false, variable.Span);
                return ApplyExpected(ref bound, expected, out cost);
            case PropertyExpression property:
                if (!TryBindProperty(property, symbols, out BoundPropertyValue? propertyValue)) { bound = null; cost = 0; return false; }
                bound = propertyValue;
                return ApplyExpected(ref bound, expected, out cost);
            case InterpolatedStringExpression interpolation:
                var parts = new List<BoundValue>();
                foreach (ExpressionNode part in interpolation.Parts)
                {
                    if (!TryBindValue(part, null, RoleDirection.Input, verb, qualifier, symbols, out BoundValue? partValue, out _)) { bound = null; cost = 0; return false; }
                    parts.Add(partValue!);
                }
                bound = new BoundInterpolatedValue(parts, interpolation.Span);
                return ApplyExpected(ref bound, expected, out cost);
            case LiteralExpression literal:
                if (expected is null)
                {
                    Type literalType = literal.Value?.GetType() ?? typeof(object);
                    bound = new BoundConstantValue(literal.Value, literalType, literal.Span);
                    cost = 0;
                    return true;
                }
                if (literal.Value is not null && _conversions.TryConvert(literal.Value, expected, out ConversionResult? conversion))
                {
                    bound = new BoundConstantValue(conversion!.Value, expected, literal.Span, conversion.Kind, conversion.Cost);
                    cost = conversion.Cost;
                    return true;
                }
                return ResolveText(literal.Value?.ToString() ?? string.Empty, expected, literal.Span, verb, qualifier, out bound, out cost);
            case ReferenceExpression reference:
                if (expected is null)
                {
                    bound = new BoundConstantValue(reference.Value, typeof(string), reference.Span);
                    cost = 0;
                    return true;
                }
                return ResolveText(reference.Value, expected, reference.Span, verb, qualifier, out bound, out cost);
            case IdentifierExpression identifier:
                if (expected is null)
                {
                    bound = new BoundConstantValue(identifier.Name, typeof(string), identifier.Span);
                    cost = 0;
                    return true;
                }
                return ResolveText(identifier.Name, expected, identifier.Span, verb, qualifier, out bound, out cost);
            default:
                bound = null;
                cost = 0;
                return false;
        }
    }

    private bool ResolveText(string text, Type expected, TextSpan span, string verb, string? qualifier, out BoundValue? bound, out int cost)
    {
        var context = new ResolutionContext(expected, null, verb, qualifier, _services);
        if (_resolvers.TryResolve(text, expected, context, out object? resolved))
        {
            bound = new BoundConstantValue(resolved, expected, span, ConversionKind.Resolution, 4);
            cost = 4;
            return true;
        }
        bound = null;
        cost = 0;
        return false;
    }

    private bool ApplyExpected(ref BoundValue? value, Type? expected, out int cost)
    {
        if (expected is null) { cost = 0; return true; }
        if (!TryPlanConversion(value!.Type, expected, out ConversionKind kind, out cost)) return false;
        if (kind != ConversionKind.Exact) value = new BoundConversionValue(value, expected, kind, cost, value.Span);
        return true;
    }

    private bool TryPlanConversion(Type source, Type target, out ConversionKind kind, out int cost) => _conversions.CanConvert(source, target, out kind, out cost);
    private static Type SlotBindingType(RoleSlotDescriptor slot) => slot.Cardinality is RoleCardinality.OneOrMore or RoleCardinality.ZeroOrMore ? slot.TypeShape.ElementType ?? slot.ValueType : slot.ValueType;

    private static bool QualifierMatches(QualifierDescriptor? qualifier, VerbImplementationDescriptor implementation, SentencePattern pattern)
    {
        if (qualifier is null) return true;
        if (implementation.Qualifiers.Contains(qualifier.Name, StringComparer.OrdinalIgnoreCase)) return true;
        if (qualifier.TargetType is null) return false;
        Type target = qualifier.TargetType;
        if (target == implementation.ResultType || target.IsAssignableFrom(implementation.ResultType) || ClrTypeShape.GetElementType(implementation.ResultType) == target) return true;
        return pattern.Roles.Any(r => r.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && (r.ValueType == target || r.TypeShape.ElementType == target));
    }

    private static void RegisterOutputs(BoundSentence sentence, SymbolScope symbols)
    {
        foreach (BoundVariableValue variable in sentence.Roles.Where(x => x.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput).SelectMany(x => x.Values).OfType<BoundVariableValue>().Where(x => x.IsOutput))
            symbols.Define(variable.Name, variable.VariableType);
        if (sentence.ResultAlias is { Length: > 0 } alias) symbols.Define(alias, sentence.ResultType);
    }

    private static bool TryBindProperty(PropertyExpression property, SymbolScope symbols, out BoundPropertyValue? bound)
    {
        if (!TryRoot(property.Target, symbols, out BoundValue? target) || !TryCompileAccessor(target!.Type, property.Property, out Type? type, out Func<object, object?>? accessor))
        {
            bound = null;
            return false;
        }
        bound = new(target, property.Property, type!, accessor!, property.Span);
        return true;
    }

    private static bool TryRoot(ExpressionNode expression, SymbolScope symbols, out BoundValue? value)
    {
        if (expression is VariableExpression variable && symbols.TryGet(variable.Name, out Type type))
        {
            value = new BoundVariableValue(variable.Name, type, false, variable.Span);
            return true;
        }
        if (expression is PropertyExpression property && TryRoot(property.Target, symbols, out BoundValue? nested) && TryCompileAccessor(nested!.Type, property.Property, out Type? propertyType, out Func<object, object?>? accessor))
        {
            value = new BoundPropertyValue(nested, property.Property, propertyType!, accessor!, property.Span);
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryCompileAccessor(Type type, string propertyName, out Type? propertyType, out Func<object, object?>? accessor)
    {
        PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            propertyType = null;
            accessor = null;
            return false;
        }
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(object), "instance");
        var cast = System.Linq.Expressions.Expression.Convert(instance, type);
        var read = System.Linq.Expressions.Expression.Property(cast, property);
        accessor = System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(System.Linq.Expressions.Expression.Convert(read, typeof(object)), instance).Compile();
        propertyType = property.PropertyType;
        return true;
    }

    private static string Signature(VerbImplementationDescriptor implementation, SentencePattern pattern) => $"{implementation.Name}({string.Join(", ", pattern.Roles.Select(r => $"{r.Name}:{r.ValueType.Name}:{r.Cardinality}"))})";
    private sealed record Candidate(VerbImplementationDescriptor Implementation, SentencePattern Pattern, IReadOnlyList<BoundRole> Roles, int Cost);
    private sealed record CandidateResult(Candidate? Candidate, string Reason)
    {
        public static CandidateResult Ok(Candidate candidate) => new(candidate, string.Empty);
        public static CandidateResult Fail(string reason) => new(null, reason);
    }

    private sealed class SymbolScope(SymbolScope? parent)
    {
        private readonly Dictionary<string, Type> _symbols = new(StringComparer.OrdinalIgnoreCase);
        public void Define(string name, Type type) => _symbols[name] = type;
        public bool TryGet(string name, out Type type)
        {
            if (_symbols.TryGetValue(name, out type!)) return true;
            if (parent is not null) return parent.TryGet(name, out type!);
            type = null!;
            return false;
        }
    }
}
