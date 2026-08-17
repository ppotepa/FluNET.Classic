using System.Collections;
using System.Globalization;
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
    { _language = language; _resolvers = resolvers; _conversions = conversions; _predicates = predicates; _services = services; }

    public BoundScript Bind(ScriptNode script, IReadOnlyDictionary<string, Type>? initialVariables = null)
    {
        _diagnostics.Clear(); var symbols = new SymbolScope(null); if (initialVariables is not null) foreach ((string name, Type type) in initialVariables) symbols.Define(name, type);
        BoundStatement[] statements = script.Statements.Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray(); return new(statements, _diagnostics.ToArray());
    }

    private BoundStatement? BindStatement(StatementNode statement, SymbolScope symbols) => statement switch { PipelineNode pipeline => BindPipeline(pipeline, symbols), IfNode conditional => BindIf(conditional, symbols), ForEachNode loop => BindForEach(loop, symbols), _ => null };

    private BoundPipeline BindPipeline(PipelineNode pipeline, SymbolScope symbols)
    {
        var stages = new List<BoundStage>(); Type? pipelineType = null;
        foreach (PipelineStageNode stage in pipeline.Stages)
        {
            BoundStage? bound = stage switch { SentenceNode sentence => BindSentence(sentence, symbols, pipelineType), FilterStageNode filter => BindFilter(filter, symbols, pipelineType), CheckStageNode check => BindCheck(check, symbols), CollectionStageNode collection => BindCollection(collection, symbols, pipelineType), _ => null };
            if (bound is null) continue; stages.Add(bound); pipelineType = bound.ResultType;
            if (bound is BoundSentence sentence) RegisterOutputs(sentence, symbols); if (bound is BoundFilter { ResultAlias: { Length: > 0 } filterAlias }) symbols.Define(filterAlias, bound.ResultType); if (bound is BoundCheck { ResultAlias: { Length: > 0 } checkAlias }) symbols.Define(checkAlias, typeof(bool)); if (bound is BoundCollection { ResultAlias: { Length: > 0 } collectionAlias }) symbols.Define(collectionAlias, bound.ResultType);
        }
        return new(stages, pipelineType, pipeline.Span);
    }

    private BoundSentence? BindSentence(SentenceNode sentence, SymbolScope symbols, Type? pipelineType)
    {
        if (!_language.TryGetVerb(sentence.Verb, out VerbDescriptor verb)) { _diagnostics.Add(new("FLU-BIND-001", $"Unknown verb '{sentence.Verb}'.", sentence.Span)); return null; }
        QualifierDescriptor? qualifier = null; if (sentence.Qualifier is not null && !_language.TryGetQualifier(sentence.Qualifier, out qualifier!)) { _diagnostics.Add(new("FLU-BIND-002", $"Unknown qualifier '{sentence.Qualifier}'.", sentence.Span)); return null; }
        var candidates = new List<Candidate>(); var rejected = new List<string>();
        foreach (VerbImplementationDescriptor implementation in verb.Implementations)
        foreach (SentencePattern pattern in implementation.Patterns)
        {
            if (!QualifierMatches(qualifier, implementation, pattern)) { rejected.Add($"{Signature(implementation, pattern)}: qualifier mismatch"); continue; }
            CandidateResult attempt = TryCandidate(sentence, verb, implementation, pattern, symbols, pipelineType, qualifier); if (attempt.Candidate is not null) candidates.Add(attempt.Candidate); else rejected.Add($"{Signature(implementation, pattern)}: {attempt.Reason}");
        }
        if (candidates.Count == 0) { _diagnostics.Add(new("FLU-BIND-010", $"No overload of {verb.Name} matches this sentence.", sentence.Span, rejected)); return null; }
        Candidate[] ordered = candidates.OrderBy(x => x.Cost).ThenBy(x => x.Implementation.StableId, StringComparer.Ordinal).ThenBy(x => x.Pattern.StableId, StringComparer.Ordinal).ToArray();
        if (ordered.Length > 1 && ordered[0].Cost == ordered[1].Cost) { _diagnostics.Add(new("FLU-BIND-011", $"Ambiguous overload for {verb.Name} at cost {ordered[0].Cost}.", sentence.Span, ordered.Where(x => x.Cost == ordered[0].Cost).Select(x => Signature(x.Implementation, x.Pattern)).ToArray())); return null; }
        Candidate selected = ordered[0]; return new(verb, selected.Implementation, selected.Pattern, selected.Roles, sentence.ResultAlias, selected.Cost, sentence.Span);
    }

    private CandidateResult TryCandidate(SentenceNode sentence, VerbDescriptor verb, VerbImplementationDescriptor implementation, SentencePattern pattern, SymbolScope symbols, Type? pipelineType, QualifierDescriptor? qualifier)
    {
        var supplied = new Dictionary<string, Queue<ExpressionNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (ClauseNode clause in sentence.Clauses)
        {
            RoleSlotDescriptor[] matches = pattern.Roles.Where(role => role.AllSurfaceNames.Contains(clause.RoleName, StringComparer.OrdinalIgnoreCase)).ToArray(); if (matches.Length == 0) return CandidateResult.Fail($"role {clause.RoleName} is not valid for this pattern"); if (matches.Length > 1) return CandidateResult.Fail($"role {clause.RoleName} is ambiguous in this pattern");
            if (!supplied.TryGetValue(matches[0].Name, out Queue<ExpressionNode>? values)) supplied[matches[0].Name] = values = new Queue<ExpressionNode>(); foreach (ExpressionNode value in clause.Values) values.Enqueue(value);
        }
        var roles = new List<BoundRole>(); int cost = pattern.Roles.Count(x => x.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore); bool aliasUsedAsOutput = false; bool pipelineUsed = false;
        foreach (RoleSlotDescriptor slot in pattern.Roles.OrderBy(x => x.Position))
        {
            supplied.TryGetValue(slot.Name, out Queue<ExpressionNode>? queue); queue ??= new Queue<ExpressionNode>(); var expressions = new List<ExpressionNode>(); if (slot.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore) while (queue.Count > 0) expressions.Add(queue.Dequeue()); else if (queue.Count > 0) expressions.Add(queue.Dequeue());
            if (expressions.Count == 0 && slot.Direction is RoleDirection.Output or RoleDirection.InputOutput && !aliasUsedAsOutput && sentence.ResultAlias is not null) { expressions.Add(new VariableExpression(sentence.ResultAlias, sentence.Span)); aliasUsedAsOutput = true; }
            if (expressions.Count == 0 && slot.Direction == RoleDirection.Input && pipelineType is not null && slot.Required && !pipelineUsed && TryPlanConversion(pipelineType, SlotBindingType(slot), out ConversionKind pipelineKind, out int pipelineCost)) { BoundValue pipeline = new BoundPipelineValue(pipelineType, sentence.Span); if (pipelineKind != ConversionKind.Exact) pipeline = new BoundConversionValue(pipeline, SlotBindingType(slot), pipelineKind, pipelineCost, sentence.Span); roles.Add(new(slot, new[] { pipeline }, sentence.Span)); cost += pipelineCost + 1; pipelineUsed = true; continue; }
            if (expressions.Count == 0) { if (slot.Direction == RoleDirection.Output) continue; if (slot.Required && slot.Cardinality is RoleCardinality.One or RoleCardinality.OneOrMore) return CandidateResult.Fail($"missing {slot.Name}"); continue; }
            var values = new List<BoundValue>(); Type expected = SlotBindingType(slot);
            foreach (ExpressionNode expression in expressions) { if (!TryBindValue(expression, expected, slot.Direction, verb.Name, qualifier?.Name, symbols, out BoundValue? value, out int valueCost, slot.Name)) return CandidateResult.Fail($"cannot bind {slot.Name} value to {expected.Name}"); values.Add(value!); cost += valueCost; }
            roles.Add(new(slot, values, sentence.Span));
        }
        if (supplied.Values.Any(x => x.Count > 0)) return CandidateResult.Fail("too many role values"); if (sentence.ResultAlias is not null && !aliasUsedAsOutput) cost += 1; return CandidateResult.Ok(new Candidate(implementation, pattern, roles, cost));
    }

    private BoundFilter? BindFilter(FilterStageNode filter, SymbolScope symbols, Type? pipelineType)
    {
        BoundValue? source = BindCollectionSource(filter.Source, symbols, pipelineType, filter.Span, "FILTER", "FLU-BIND-120"); if (source is null) return null; Type? element = ClrTypeShape.GetElementType(source.Type); if (element is null) { _diagnostics.Add(new("FLU-BIND-122", $"FILTER requires a collection, got {source.Type.Name}.", filter.Source?.Span ?? filter.Span)); return null; }
        BoundExpression predicate = BindExpression(filter.Predicate, symbols, element); if (predicate.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-123", "WHERE expression must be BOOLEAN.", filter.Predicate.Span)); return new(source, predicate, element, filter.ResultAlias, filter.Span);
    }
    private BoundCollection? BindCollection(CollectionStageNode node, SymbolScope symbols, Type? pipelineType)
    {
        BoundValue? source = BindCollectionSource(node.Source, symbols, pipelineType, node.Span, node.Operation, "FLU-BIND-160"); if (source is null) return null;
        Type? element = ClrTypeShape.GetElementType(source.Type); if (element is null) { _diagnostics.Add(new("FLU-BIND-161", $"{node.Operation} requires a collection, got {source.Type.Name}.", node.Span)); return null; }

        string op = node.Operation.ToUpperInvariant();
        BoundExpression? argument = null;
        if (node.Argument is not null) argument = op is "SORT" or "GROUP" or "DISTINCT" ? BindExpression(node.Argument, symbols, element) : BindExpression(node.Argument, symbols, null);
        if (op is "TAKE" or "SKIP" && (argument is null || !IsNumeric(argument.Type))) _diagnostics.Add(new("FLU-BIND-162", $"{op} requires a numeric amount.", node.Argument?.Span ?? node.Span));
        if (op is "TAKE" or "SKIP" && argument is not null && TryGetConstantDecimal(argument, out decimal amount) && amount < 0) _diagnostics.Add(new("FLU-BIND-166", $"{op} requires a non-negative amount, got {amount.ToString(CultureInfo.InvariantCulture)}.", node.Argument?.Span ?? node.Span));
        if (op is "SORT" or "GROUP" && argument is null) _diagnostics.Add(new("FLU-BIND-163", $"{op} requires BY selector.", node.Span));

        BoundValue? strategy = null;
        if (!_language.TryGetIntrinsic(op, out IntrinsicDescriptor intrinsic))
        {
            _diagnostics.Add(new("FLU-BIND-164", $"Unknown intrinsic '{op}'.", node.Span));
        }
        else if (node.Strategy is not null)
        {
            if (intrinsic.StrategyType is null)
            {
                _diagnostics.Add(new("FLU-BIND-164", $"Intrinsic '{op}' does not accept {intrinsic.StrategyRole} strategy.", node.Strategy.Span));
            }
            else if (!TryBindValue(node.Strategy, intrinsic.StrategyType, RoleDirection.Input, op, null, symbols, out strategy, out _, intrinsic.StrategyRole))
            {
                _diagnostics.Add(new("FLU-BIND-165", $"Cannot bind {intrinsic.StrategyRole} strategy for {op} to {Friendly(intrinsic.StrategyType)}.", node.Strategy.Span));
            }
        }

        Type resultType = op switch { "COUNT" => typeof(int), "GROUP" => typeof(CollectionGroup[]), _ => element.MakeArrayType() };
        return new(op, source, element, argument, node.ResultAlias, resultType, node.Span, strategy);
    }
    private BoundValue? BindCollectionSource(ExpressionNode? sourceExpression, SymbolScope symbols, Type? pipelineType, TextSpan span, string operation, string code)
    {
        if (sourceExpression is null) { if (pipelineType is null) { _diagnostics.Add(new(code, $"{operation} requires a source or pipeline value.", span)); return null; } return new BoundPipelineValue(pipelineType, span); }
        if (!TryBindValue(sourceExpression, null, RoleDirection.Input, operation, null, symbols, out BoundValue? source, out _)) { _diagnostics.Add(new(code, $"{operation} source cannot be bound.", sourceExpression.Span)); return null; } return source;
    }
    private BoundCheck BindCheck(CheckStageNode check, SymbolScope symbols) { BoundExpression condition = BindExpression(check.Condition, symbols, null); if (condition.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-124", "CHECK IF condition must be BOOLEAN.", check.Condition.Span)); return new(condition, check.ResultAlias, check.Span); }

    private BoundIf BindIf(IfNode node, SymbolScope symbols)
    {
        BoundExpression condition = BindExpression(node.Condition, symbols, null);
        if (condition.Type != typeof(bool)) _diagnostics.Add(new("FLU-BIND-130", "IF condition must be BOOLEAN.", node.Condition.Span));

        var thenScope = new SymbolScope(symbols);
        BoundBlock thenBlock = BindBlock(node.Then, thenScope);
        BoundBlock? elseBlock = null;
        IReadOnlyList<BoundFlowVariable> promoted = Array.Empty<BoundFlowVariable>();
        if (node.Else is not null)
        {
            var elseScope = new SymbolScope(symbols);
            elseBlock = BindBlock(node.Else, elseScope);
            promoted = MergeBranchSymbols(symbols, thenScope, elseScope, node.Span);
        }

        return new(condition, thenBlock, elseBlock, promoted, node.Span);
    }

    private IReadOnlyList<BoundFlowVariable> MergeBranchSymbols(SymbolScope parent, SymbolScope thenScope, SymbolScope elseScope, TextSpan span)
    {
        var promoted = new List<BoundFlowVariable>();
        foreach ((string name, Type thenType) in thenScope.LocalSymbols)
        {
            if (!elseScope.TryGetLocal(name, out Type elseType)) continue;
            if (TryCommonBranchType(thenType, elseType, out Type commonType))
            {
                parent.Define(name, commonType);
                promoted.Add(new(name, commonType));
                continue;
            }
            _diagnostics.Add(new("FLU-BIND-132", $"Variable '[{name}]' has incompatible branch types {Friendly(thenType)} and {Friendly(elseType)}.", span, new[] { Friendly(thenType), Friendly(elseType) }));
        }
        return promoted;
    }

    private static bool TryCommonBranchType(Type left, Type right, out Type common)
    {
        if (left == right) { common = left; return true; }
        if (left.IsAssignableFrom(right)) { common = left; return true; }
        if (right.IsAssignableFrom(left)) { common = right; return true; }
        common = null!;
        return false;
    }

    private BoundForEach? BindForEach(ForEachNode node, SymbolScope symbols)
    {
        if (!TryBindValue(node.Source, null, RoleDirection.Input, "FOR EACH", null, symbols, out BoundValue? source, out _)) { _diagnostics.Add(new("FLU-BIND-140", "FOR EACH source cannot be bound.", node.Source.Span)); return null; } Type? element = ClrTypeShape.GetElementType(source!.Type); if (element is null) { _diagnostics.Add(new("FLU-BIND-141", $"FOR EACH requires a collection, got {source.Type.Name}.", node.Source.Span)); return null; } var child = new SymbolScope(symbols); child.Define(node.Variable, element); return new(node.Variable, source, element, BindBlock(node.Body, child), node.Span);
    }
    private BoundBlock BindBlock(BlockNode block, SymbolScope symbols) => new(block.Statements.Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray(), block.Span);

    private BoundExpression BindExpression(ExpressionNode expression, SymbolScope symbols, Type? itemType) => expression switch
    {
        BinaryExpression binary => BindBinary(binary, symbols, itemType), BetweenExpression between => BindBetween(between, symbols, itemType), UnaryExpression unary => BindUnary(unary, symbols, itemType), PredicateExpression predicate => BindPredicate(predicate, symbols, itemType), PropertyExpression property when itemType is not null => BindItemPropertyPath(property, itemType), IdentifierExpression identifier when itemType is not null => BindItemPropertyPath(identifier, itemType),
        _ => TryBindValue(expression, null, RoleDirection.Input, "EXPRESSION", null, symbols, out BoundValue? value, out _) ? new BoundValueExpression(value!, expression.Span) : ErrorExpression(expression)
    };

    private BoundExpression BindUnary(UnaryExpression unary, SymbolScope symbols, Type? itemType)
    {
        BoundExpression operand = BindExpression(unary.Operand, symbols, itemType);
        if (!_language.TryGetOperator(unary.Operator, out OperatorDescriptor descriptor) || descriptor.Arity != OperatorArity.Unary)
        {
            descriptor = new OperatorDescriptor($"operator:unknown:{unary.Operator.ToLowerInvariant()}", unary.Operator, 1, OperatorArity.Unary);
            _diagnostics.Add(new("FLU-BIND-154", $"Unknown unary operator '{unary.Operator}'.", unary.Span));
        }
        ValidateOperatorCompatibility(descriptor, operand, null, unary.Span);
        return new BoundUnaryExpression(descriptor, operand, unary.Span);
    }
    private BoundExpression BindPredicate(PredicateExpression predicate, SymbolScope symbols, Type? itemType)
    {
        if (!_language.TryGetPredicate(predicate.Predicate, out PredicateDescriptor descriptor)) { BoundExpression unknownOperand = BindExpression(predicate.Operand, symbols, itemType); _diagnostics.Add(new("FLU-BIND-152", $"Unknown predicate '{predicate.Predicate}'.", predicate.Span)); return new BoundPredicateExpression(new PredicateDescriptor($"predicate:unknown:{predicate.Predicate.ToLowerInvariant()}", predicate.Predicate, PredicateSyntaxKind.IsState), unknownOperand, predicate.Span); }
        BoundExpression operand = BindPredicateOperand(predicate.Operand, descriptor, symbols, itemType); if (!descriptor.CanApplyTo(operand.Type)) _diagnostics.Add(new("FLU-BIND-152", $"Predicate '{descriptor.Name}' cannot evaluate {Friendly(operand.Type)}; expected {DescribeTypes(descriptor.SupportedOperandTypes)}.", predicate.Span)); else if (!_predicates.CanEvaluate(descriptor.Name, operand.Type)) _diagnostics.Add(new("FLU-BIND-153", $"Predicate '{descriptor.Name}' has no registered evaluator for {Friendly(operand.Type)}.", predicate.Span)); return new BoundPredicateExpression(descriptor, operand, predicate.Span);
    }
    private BoundExpression BindPredicateOperand(ExpressionNode expression, PredicateDescriptor descriptor, SymbolScope symbols, Type? itemType) { if (expression is ReferenceExpression && descriptor.ReferenceOperandType is { } referenceType && TryBindValue(expression, referenceType, RoleDirection.Input, "EXPRESSION", null, symbols, out BoundValue? resolved, out _)) return new BoundValueExpression(resolved!, expression.Span); return BindExpression(expression, symbols, itemType); }
    private BoundExpression BindBinary(BinaryExpression binary, SymbolScope symbols, Type? itemType)
    {
        BoundExpression left = BindExpression(binary.Left, symbols, itemType); BoundExpression right = BindExpression(binary.Right, symbols, itemType);
        if (!_language.TryGetOperator(binary.Operator, out OperatorDescriptor descriptor) || descriptor.Arity != OperatorArity.Binary)
        {
            descriptor = new OperatorDescriptor($"operator:unknown:{binary.Operator.ToLowerInvariant()}", binary.Operator, 1);
            _diagnostics.Add(new("FLU-BIND-156", $"Unknown binary operator '{binary.Operator}'.", binary.Span));
        }
        ValidateOperatorCompatibility(descriptor, left, right, binary.Span);
        return new BoundBinaryExpression(left, descriptor, right, binary.Span);
    }
    private BoundExpression BindBetween(BetweenExpression between, SymbolScope symbols, Type? itemType)
    {
        BoundExpression operand = BindExpression(between.Operand, symbols, itemType); BoundExpression lower = BindExpression(between.Lower, symbols, itemType); BoundExpression upper = BindExpression(between.Upper, symbols, itemType);
        if (!_language.TryGetOperator(between.Operator, out OperatorDescriptor descriptor) || descriptor.Arity != OperatorArity.Ternary)
        {
            descriptor = new OperatorDescriptor($"operator:unknown:{between.Operator.ToLowerInvariant()}", between.Operator, 1, OperatorArity.Ternary);
            _diagnostics.Add(new("FLU-BIND-159", $"Unknown ternary operator '{between.Operator}'.", between.Span));
        }
        bool valid = IsOperatorCompatible(descriptor.Compatibility, operand.Type, lower.Type) && IsOperatorCompatible(descriptor.Compatibility, operand.Type, upper.Type);
        if (!valid) _diagnostics.Add(new("FLU-BIND-157", $"Operator '{descriptor.Name}' requires compatible bounds; got {Friendly(operand.Type)}, {Friendly(lower.Type)}, {Friendly(upper.Type)}.", between.Span));
        return new BoundBetweenExpression(descriptor, operand, lower, upper, between.Span);
    }
    private void ValidateOperatorCompatibility(OperatorDescriptor descriptor, BoundExpression left, BoundExpression? right, TextSpan span)
    {
        bool valid = right is null
            ? descriptor.Compatibility is OperatorCompatibilityRule.Any || descriptor.Compatibility == OperatorCompatibilityRule.BooleanOperand && left.Type == typeof(bool)
            : IsOperatorCompatible(descriptor.Compatibility, left.Type, right.Type);
        if (!valid)
        {
            string operands = right is null ? Friendly(left.Type) : $"{Friendly(left.Type)} and {Friendly(right.Type)}";
            _diagnostics.Add(new("FLU-BIND-158", $"Operator '{descriptor.Name}' is not valid for {operands}.", span));
        }
    }
    private bool IsOperatorCompatible(OperatorCompatibilityRule rule, Type left, Type right) => rule switch
    {
        OperatorCompatibilityRule.Any => true,
        OperatorCompatibilityRule.BooleanOperand => left == typeof(bool),
        OperatorCompatibilityRule.BooleanPair => left == typeof(bool) && right == typeof(bool),
        OperatorCompatibilityRule.ComparablePair => AreComparable(left, right),
        OperatorCompatibilityRule.OrderedPair => AreOrderCompatible(left, right),
        OperatorCompatibilityRule.ContainerContainsValue => CanContain(left, right),
        OperatorCompatibilityRule.ValueInContainer => right != typeof(string) && CanContain(right, left),
        OperatorCompatibilityRule.StringPair => left == typeof(string) && right == typeof(string),
        OperatorCompatibilityRule.TemporalPair => IsTemporal(left) && IsTemporal(right) && AreComparable(left, right),
        _ => false
    };
    private bool CanContain(Type containerType, Type valueType) { Type container = Nullable.GetUnderlyingType(containerType) ?? containerType; Type value = Nullable.GetUnderlyingType(valueType) ?? valueType; if (container == typeof(string)) return value == typeof(string); if (typeof(IDictionary).IsAssignableFrom(container)) return true; Type? element = ClrTypeShape.GetElementType(container); return element is not null && AreComparable(element, value); }
    private bool AreComparable(Type left, Type right) { Type a = Nullable.GetUnderlyingType(left) ?? left; Type b = Nullable.GetUnderlyingType(right) ?? right; if (a == typeof(object) || b == typeof(object)) return true; if (a == b || a.IsAssignableFrom(b) || b.IsAssignableFrom(a)) return true; if (IsNumeric(a) && IsNumeric(b)) return true; return _conversions.CanConvert(a, b, out _, out _) || _conversions.CanConvert(b, a, out _, out _); }
    private bool AreOrderCompatible(Type left, Type right) { if (!AreComparable(left, right)) return false; Type a = Nullable.GetUnderlyingType(left) ?? left; Type b = Nullable.GetUnderlyingType(right) ?? right; return IsNumeric(a) && IsNumeric(b) || IsTemporal(a) && IsTemporal(b) || typeof(IComparable).IsAssignableFrom(a) && (a == b || a.IsAssignableFrom(b) || b.IsAssignableFrom(a)); }
    private static bool IsTemporal(Type type) { Type effective = Nullable.GetUnderlyingType(type) ?? type; return effective == typeof(DateOnly) || effective == typeof(TimeOnly) || effective == typeof(DateTime) || effective == typeof(DateTimeOffset) || effective == typeof(TimeSpan); }

    private BoundExpression BindItemPropertyPath(ExpressionNode expression, Type itemType)
    {
        string[] segments = PropertySegments(expression).ToArray(); if (segments.Length == 0) return ErrorExpression(expression);
        Type currentType = itemType; var properties = new List<PropertyInfo>();
        foreach (string segment in segments)
        {
            PropertyInfo? property = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null) { string[] suggestions = ClosestProperties(currentType, segment); _diagnostics.Add(new("FLU-BIND-150", $"'{Friendly(currentType)}' has no property '{segment}'.", expression.Span, suggestions)); return new BoundValueExpression(new BoundConstantValue(null, typeof(object), expression.Span), expression.Span); }
            properties.Add(property); currentType = property.PropertyType;
        }
        object? Access(object instance) { object? current = instance; foreach (PropertyInfo property in properties) { if (current is null) return null; current = property.GetValue(current); } return current; }
        return new BoundItemPropertyExpression(string.Join('.', segments), currentType, Access, expression.Span);
    }
    private static IEnumerable<string> PropertySegments(ExpressionNode expression)
    {
        switch (expression)
        {
            case IdentifierExpression identifier: yield return identifier.Name; break;
            case PropertyExpression property:
                foreach (string segment in PropertySegments(property.Target)) yield return segment; yield return property.Property; break;
        }
    }
    private static string[] ClosestProperties(Type type, string requested) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => (x.Name, Distance: EditDistance(x.Name, requested))).OrderBy(x => x.Distance).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(3).Select(x => x.Name).ToArray();
    private static int EditDistance(string left, string right)
    {
        left = left.ToUpperInvariant(); right = right.ToUpperInvariant(); int[,] matrix = new int[left.Length + 1, right.Length + 1]; for (int i = 0; i <= left.Length; i++) matrix[i, 0] = i; for (int j = 0; j <= right.Length; j++) matrix[0, j] = j;
        for (int i = 1; i <= left.Length; i++) for (int j = 1; j <= right.Length; j++) matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1)); return matrix[left.Length, right.Length];
    }

    private BoundExpression ErrorExpression(ExpressionNode expression) { _diagnostics.Add(new("FLU-BIND-151", "Expression cannot be bound.", expression.Span)); return new BoundValueExpression(new BoundConstantValue(null, typeof(object), expression.Span), expression.Span); }

    private bool TryBindValue(ExpressionNode expression, Type? expected, RoleDirection direction, string verb, string? qualifier, SymbolScope symbols, out BoundValue? bound, out int cost, string? roleName = null)
    {
        if (direction == RoleDirection.Output) { if (expression is VariableExpression output) { bound = new BoundVariableValue(output.Name, expected ?? typeof(object), true, output.Span); cost = 0; return true; } bound = null; cost = 0; return false; }
        switch (expression)
        {
            case BinaryExpression or BetweenExpression or UnaryExpression or PredicateExpression:
                BoundExpression expressionValue = BindExpression(expression, symbols, null); bound = new BoundExpressionValue(expressionValue, expression.Span); return ApplyExpected(ref bound, expected, out cost);
            case VariableExpression variable: if (!symbols.TryGet(variable.Name, out Type variableType)) { bound = null; cost = 0; return false; } bound = new BoundVariableValue(variable.Name, variableType, false, variable.Span); return ApplyExpected(ref bound, expected, out cost);
            case PropertyExpression property: if (!TryBindProperty(property, symbols, out BoundPropertyValue? propertyValue)) { bound = null; cost = 0; return false; } bound = propertyValue; return ApplyExpected(ref bound, expected, out cost);
            case InterpolatedStringExpression interpolation:
                var parts = new List<BoundValue>(); foreach (ExpressionNode part in interpolation.Parts) { if (!TryBindValue(part, null, RoleDirection.Input, verb, qualifier, symbols, out BoundValue? partValue, out _)) { bound = null; cost = 0; return false; } parts.Add(partValue!); } bound = new BoundInterpolatedValue(parts, interpolation.Span); return ApplyExpected(ref bound, expected, out cost);
            case LiteralExpression literal:
                if (expected is null) { Type literalType = literal.Value?.GetType() ?? typeof(object); bound = new BoundConstantValue(literal.Value, literalType, literal.Span); cost = 0; return true; }
                if (literal.Value is not null && _conversions.TryConvert(literal.Value, expected, out ConversionResult? conversion)) { bound = new BoundConstantValue(conversion!.Value, expected, literal.Span, conversion.Kind, conversion.Cost); cost = conversion.Cost; return true; }
                return ResolveText(literal.Value?.ToString() ?? string.Empty, expected, literal.Span, verb, qualifier, roleName, out bound, out cost);
            case ReferenceExpression reference:
                if (expected is null) { bound = new BoundConstantValue(reference.Value, typeof(string), reference.Span); cost = 0; return true; } return ResolveText(reference.Value, expected, reference.Span, verb, qualifier, roleName, out bound, out cost);
            case IdentifierExpression identifier:
                if (expected is null) { bound = new BoundConstantValue(identifier.Name, typeof(string), identifier.Span); cost = 0; return true; } return ResolveText(identifier.Name, expected, identifier.Span, verb, qualifier, roleName, out bound, out cost);
            default: bound = null; cost = 0; return false;
        }
    }

    private bool ResolveText(string text, Type expected, TextSpan span, string verb, string? qualifier, string? roleName, out BoundValue? bound, out int cost) { var context = new ResolutionContext(expected, roleName, verb, qualifier, _services); if (_resolvers.TryResolve(text, expected, context, out object? resolved)) { bound = new BoundConstantValue(resolved, expected, span, ConversionKind.Resolution, 4); cost = 4; return true; } bound = null; cost = 0; return false; }
    private bool ApplyExpected(ref BoundValue? value, Type? expected, out int cost) { if (expected is null) { cost = 0; return true; } if (!TryPlanConversion(value!.Type, expected, out ConversionKind kind, out cost)) return false; if (kind != ConversionKind.Exact) value = new BoundConversionValue(value, expected, kind, cost, value.Span); return true; }
    private bool TryPlanConversion(Type source, Type target, out ConversionKind kind, out int cost) => _conversions.CanConvert(source, target, out kind, out cost);
    private static Type SlotBindingType(RoleSlotDescriptor slot) => slot.Cardinality is RoleCardinality.OneOrMore or RoleCardinality.ZeroOrMore ? slot.TypeShape.ElementType ?? slot.ValueType : slot.ValueType;
    private static bool QualifierMatches(QualifierDescriptor? qualifier, VerbImplementationDescriptor implementation, SentencePattern pattern) { if (qualifier is null) return true; if (implementation.Qualifiers.Contains(qualifier.Name, StringComparer.OrdinalIgnoreCase)) return true; if (qualifier.TargetType is null) return false; Type target = qualifier.TargetType; if (target == implementation.ResultType || target.IsAssignableFrom(implementation.ResultType) || ClrTypeShape.GetElementType(implementation.ResultType) == target) return true; return pattern.Roles.Any(role => role.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && (role.ValueType == target || role.TypeShape.ElementType == target)); }
    private static void RegisterOutputs(BoundSentence sentence, SymbolScope symbols) { foreach (BoundVariableValue variable in sentence.Roles.Where(x => x.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput).SelectMany(x => x.Values).OfType<BoundVariableValue>().Where(x => x.IsOutput)) symbols.Define(variable.Name, variable.VariableType); if (sentence.ResultAlias is { Length: > 0 } alias) symbols.Define(alias, sentence.ResultType); }
    private static bool TryBindProperty(PropertyExpression property, SymbolScope symbols, out BoundPropertyValue? bound) { if (!TryRoot(property.Target, symbols, out BoundValue? target) || !TryCompileAccessor(target!.Type, property.Property, out Type? type, out Func<object, object?>? accessor)) { bound = null; return false; } bound = new(target, property.Property, type!, accessor!, property.Span); return true; }
    private static bool TryRoot(ExpressionNode expression, SymbolScope symbols, out BoundValue? value) { if (expression is VariableExpression variable && symbols.TryGet(variable.Name, out Type type)) { value = new BoundVariableValue(variable.Name, type, false, variable.Span); return true; } if (expression is PropertyExpression property && TryRoot(property.Target, symbols, out BoundValue? nested) && TryCompileAccessor(nested!.Type, property.Property, out Type? propertyType, out Func<object, object?>? accessor)) { value = new BoundPropertyValue(nested, property.Property, propertyType!, accessor!, property.Span); return true; } value = null; return false; }
    private static bool TryCompileAccessor(Type type, string propertyName, out Type? propertyType, out Func<object, object?>? accessor) { PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase); if (property is null) { propertyType = null; accessor = null; return false; } var instance = System.Linq.Expressions.Expression.Parameter(typeof(object), "instance"); var cast = System.Linq.Expressions.Expression.Convert(instance, type); var read = System.Linq.Expressions.Expression.Property(cast, property); accessor = System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(System.Linq.Expressions.Expression.Convert(read, typeof(object)), instance).Compile(); propertyType = property.PropertyType; return true; }
    private static bool TryGetConstantDecimal(BoundExpression expression, out decimal value)
    {
        if (expression is BoundValueExpression { Value: BoundConstantValue constant } && constant.Value is not null && IsNumeric(constant.Value.GetType()))
        {
            try { value = Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture); return true; }
            catch { }
        }
        value = 0;
        return false;
    }
    private static bool IsNumeric(Type type) => Type.GetTypeCode(Nullable.GetUnderlyingType(type) ?? type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    private static string Friendly(Type type) => type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(',', type.GetGenericArguments().Select(Friendly))}>" : type.Name;
    private static string DescribeTypes(IReadOnlyList<Type> types) => types.Count == 0 ? "any" : string.Join(", ", types.Select(Friendly));
    private static string Signature(VerbImplementationDescriptor implementation, SentencePattern pattern) => $"{implementation.Name}({string.Join(", ", pattern.Roles.Select(role => $"{role.Name}:{role.ValueType.Name}:{role.Cardinality}"))})";
    private sealed record Candidate(VerbImplementationDescriptor Implementation, SentencePattern Pattern, IReadOnlyList<BoundRole> Roles, int Cost);
    private sealed record CandidateResult(Candidate? Candidate, string Reason) { public static CandidateResult Ok(Candidate candidate) => new(candidate, string.Empty); public static CandidateResult Fail(string reason) => new(null, reason); }
    private sealed class SymbolScope(SymbolScope? parent)
    {
        private readonly Dictionary<string, Type> _symbols = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, Type> LocalSymbols => _symbols;
        public void Define(string name, Type type) => _symbols[name] = type;
        public bool TryGetLocal(string name, out Type type) => _symbols.TryGetValue(name, out type!);
        public bool TryGet(string name, out Type type) { if (_symbols.TryGetValue(name, out type!)) return true; if (parent is not null) return parent.TryGet(name, out type!); type = null!; return false; }
    }
}
