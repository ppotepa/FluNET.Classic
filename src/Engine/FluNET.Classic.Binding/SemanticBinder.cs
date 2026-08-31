using FluNET.Classic.Core;
using FluNET.Classic.Syntax;
using System.Collections;
using System.Globalization;
using System.Reflection;

namespace FluNET.Classic.Binding;

public sealed class SemanticBinder
{
    private readonly LanguageSnapshot _language;
    private readonly ValueResolverRegistry _resolvers;
    private readonly ValueConversionRegistry _conversions;
    private readonly PredicateRegistry _predicates;
    private readonly IServiceProvider? _services;
    private readonly List<BindingDiagnostic> _diagnostics = [];
    private readonly Dictionary<string, BoundScriptCallable> _scriptCallables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FluRecordSchema> _recordSchemas = new(StringComparer.OrdinalIgnoreCase);
    private Type? _activeReturnType;

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
        _scriptCallables.Clear();
        _recordSchemas.Clear();
        _activeReturnType = null;
        foreach (RecordDefinitionNode record in script.Statements.OfType<RecordDefinitionNode>())
            RegisterRecord(record);
        foreach (DefinitionNode definition in script.Statements.OfType<DefinitionNode>())
            RegisterDefinition(definition);
        foreach (DefinitionNode definition in script.Statements.OfType<DefinitionNode>())
            BindDefinition(definition);
        DiagnoseDefinitionCycles();
        var symbols = new SymbolScope(null);
        if (initialVariables is not null)
            foreach ((string name, Type type) in initialVariables)
                symbols.Define(name, type);
        BoundStatement[] statements = script.Statements.Where(x => x is not DefinitionNode and not RecordDefinitionNode).Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray();
        return new(statements, _diagnostics.ToArray(), _scriptCallables);
    }

    private BoundStatement? BindStatement(StatementNode statement, SymbolScope symbols) => statement switch { PipelineNode pipeline => BindPipeline(pipeline, symbols), IfNode conditional => BindIf(conditional, symbols), ForEachNode loop => BindForEach(loop, symbols), TryNode @try => BindTry(@try, symbols), ReturnNode @return => BindReturn(@return, symbols), DefinitionNode or RecordDefinitionNode => null, _ => null };

    private void RegisterRecord(RecordDefinitionNode record)
    {
        if (_recordSchemas.ContainsKey(record.Name))
        {
            _diagnostics.Add(new("FLU-BIND-230", $"Record '{record.Name}' is already defined.", record.Span));
            return;
        }
        var fields = new List<FluRecordField>();
        foreach (RecordFieldNode field in record.Fields)
        {
            if (!TryResolveType(field.TypeName, out Type? type))
            {
                _diagnostics.Add(new("FLU-BIND-231", $"Unknown type '{field.TypeName}' in record '{record.Name}'.", field.Span));
                type = typeof(object);
            }
            fields.Add(new(field.Name, type!));
        }
        try
        {
            _recordSchemas.Add(record.Name, new FluRecordSchema(record.Name, fields));
        }
        catch (ArgumentException exception) { _diagnostics.Add(new("FLU-BIND-232", exception.Message, record.Span)); }
    }

    private void RegisterDefinition(DefinitionNode definition)
    {
        if (_scriptCallables.ContainsKey(definition.Name))
        {
            _diagnostics.Add(new("FLU-BIND-200", $"Definition '{definition.Name}' is already defined.", definition.Span));
            return;
        }
        var parameters = new List<BoundScriptParameter>();
        foreach (DefinitionParameterNode parameter in definition.Parameters)
        {
            if (!TryResolveType(parameter.TypeName, out Type? type))
            {
                _diagnostics.Add(new("FLU-BIND-201", $"Unknown type '{parameter.TypeName}' in definition '{definition.Name}'.", parameter.Span));
                type = typeof(object);
            }
            parameters.Add(new(parameter.RoleName, parameter.Name, type!));
        }
        if (!TryResolveType(definition.ReturnTypeName, out Type? returnType))
        {
            _diagnostics.Add(new("FLU-BIND-202", $"Unknown return type '{definition.ReturnTypeName}' in definition '{definition.Name}'.", definition.Span));
            returnType = typeof(object);
        }
        _scriptCallables.Add(definition.Name, new(definition.Name, definition.Qualifier, definition.Kind, parameters, returnType!, new BoundBlock(Array.Empty<BoundStatement>(), definition.Body.Span), definition.Span));
    }

    private void BindDefinition(DefinitionNode definition)
    {
        if (!_scriptCallables.TryGetValue(definition.Name, out BoundScriptCallable? callable))
            return;
        var symbols = new SymbolScope(null);
        foreach (BoundScriptParameter parameter in callable.Parameters)
            symbols.Define(parameter.Name, parameter.Type);
        Type? previous = _activeReturnType;
        _activeReturnType = callable.ReturnType;
        BoundBlock body = BindBlock(definition.Body, symbols);
        _activeReturnType = previous;
        if (callable.ReturnType != typeof(void) && !ContainsReturn(body))
            _diagnostics.Add(new("FLU-BIND-206", $"Definition '{callable.Name}' does not return {Friendly(callable.ReturnType)}.", definition.Span));
        callable.Body = body;
    }

    private static bool ContainsReturn(BoundBlock block) => block.Statements.Any(statement => statement switch
    {
        BoundReturn => true,
        BoundIf conditional => ContainsReturn(conditional.Then) || conditional.Else is not null && ContainsReturn(conditional.Else),
        BoundForEach loop => ContainsReturn(loop.Body),
        BoundTry @try => ContainsReturn(@try.Body) || @try.Failure is not null && ContainsReturn(@try.Failure) || @try.Finally is not null && ContainsReturn(@try.Finally),
        _ => false
    });

    private void DiagnoseDefinitionCycles()
    {
        var visited = new HashSet<BoundScriptCallable>();
        var active = new HashSet<BoundScriptCallable>();
        foreach (BoundScriptCallable callable in _scriptCallables.Values)
            Visit(callable);

        void Visit(BoundScriptCallable callable)
        {
            if (active.Contains(callable))
            {
                _diagnostics.Add(new("FLU-BIND-208", $"Definition cycle detected at '{callable.Name}'.", callable.Span));
                return;
            }
            if (!visited.Add(callable))
                return;
            active.Add(callable);
            foreach (BoundScriptCallable child in Calls(callable.Body))
                Visit(child);
            active.Remove(callable);
        }
    }

    private static IEnumerable<BoundScriptCallable> Calls(BoundBlock block)
    {
        foreach (BoundStatement statement in block.Statements)
        {
            switch (statement)
            {
                case BoundPipeline pipeline:
                    foreach (BoundScriptCall call in pipeline.Stages.OfType<BoundScriptCall>())
                        yield return call.Callable;
                    break;
                case BoundIf conditional:
                    foreach (BoundScriptCallable call in Calls(conditional.Then))
                        yield return call;
                    if (conditional.Else is not null)
                        foreach (BoundScriptCallable call in Calls(conditional.Else))
                            yield return call;
                    break;
                case BoundForEach loop:
                    foreach (BoundScriptCallable call in Calls(loop.Body))
                        yield return call;
                    break;
                case BoundTry @try:
                    foreach (BoundScriptCallable call in Calls(@try.Body))
                        yield return call;
                    if (@try.Failure is not null)
                        foreach (BoundScriptCallable call in Calls(@try.Failure))
                            yield return call;
                    if (@try.Finally is not null)
                        foreach (BoundScriptCallable call in Calls(@try.Finally))
                            yield return call;
                    break;
            }
        }
    }

    private BoundReturn? BindReturn(ReturnNode node, SymbolScope symbols)
    {
        if (_activeReturnType is null)
        {
            _diagnostics.Add(new("FLU-BIND-203", "RETURN is only valid inside a task or function.", node.Span));
            return null;
        }
        if (node.Value is null)
        {
            if (_activeReturnType != typeof(void))
                _diagnostics.Add(new("FLU-BIND-204", $"This definition must return {Friendly(_activeReturnType)}.", node.Span));
            return new(null, node.Span);
        }
        if (!TryBindValue(node.Value, _activeReturnType == typeof(void) ? null : _activeReturnType, RoleDirection.Input, "RETURN", null, symbols, out BoundValue? value, out _, "WHAT"))
            _diagnostics.Add(new("FLU-BIND-205", $"RETURN value cannot be converted to {Friendly(_activeReturnType)}.", node.Span));
        return new(value, node.Span);
    }

    private BoundTry BindTry(TryNode node, SymbolScope symbols)
    {
        BoundBlock body = BindBlock(node.Body, new SymbolScope(symbols));
        BoundBlock? failure = node.Failure is null ? null : BindBlock(node.Failure, new SymbolScope(symbols));
        BoundBlock? @finally = node.Finally is null ? null : BindBlock(node.Finally, new SymbolScope(symbols));
        return new(body, failure, @finally, node.Span);
    }

    private BoundPipeline BindPipeline(PipelineNode pipeline, SymbolScope symbols)
    {
        var stages = new List<BoundStage>();
        Type? pipelineType = null;
        foreach (PipelineStageNode stage in pipeline.Stages)
        {
            BoundStage? bound = stage switch
            {
                SentenceNode sentence => BindSentence(sentence, symbols, pipelineType),
                FilterStageNode filter => BindFilter(filter, symbols, pipelineType),
                CheckStageNode check => BindCheck(check, symbols),
                CollectionStageNode collection => BindCollection(collection, symbols, pipelineType),
                _ => null
            };
            if (bound is null)
                continue;
            stages.Add(bound);
            pipelineType = bound.ResultType;
            if (bound is BoundSentence boundSentence)
                RegisterOutputs(boundSentence, symbols);
            if (bound is BoundScriptCall scriptCall && scriptCall.ResultAlias is { Length: > 0 })
                DefineBinding(symbols, scriptCall.ResultAlias, scriptCall.ResultType, scriptCall.Span);
            if (bound is BoundRecordCreate recordCreate && recordCreate.ResultAlias is { Length: > 0 })
                DefineBinding(symbols, recordCreate.ResultAlias, recordCreate.ResultType, recordCreate.Span);
            if (bound is BoundFilter { ResultAlias: { Length: > 0 } filterAlias })
                DefineBinding(symbols, filterAlias, bound.ResultType, bound.Span);
            if (bound is BoundCheck { ResultAlias: { Length: > 0 } checkAlias })
                DefineBinding(symbols, checkAlias, typeof(bool), bound.Span);
            if (bound is BoundCollection { ResultAlias: { Length: > 0 } collectionAlias })
                DefineBinding(symbols, collectionAlias, bound.ResultType, bound.Span);
        }
        return new(stages, pipelineType, pipeline.Span);
    }

    private BoundStage? BindSentence(SentenceNode sentence, SymbolScope symbols, Type? pipelineType)
    {
        if (sentence.Verb.Equals("MAKE", StringComparison.OrdinalIgnoreCase) && sentence.Qualifier is not null && _recordSchemas.TryGetValue(sentence.Qualifier, out FluRecordSchema? schema))
            return BindRecordCreate(sentence, schema, symbols);
        if (!_language.TryGetVerb(sentence.Verb, out VerbDescriptor verb) && _scriptCallables.TryGetValue(sentence.Verb, out BoundScriptCallable? callable))
        {
            if (callable.Qualifier is not null && !callable.Qualifier.Equals(sentence.Qualifier, StringComparison.OrdinalIgnoreCase))
            {
                _diagnostics.Add(new("FLU-BIND-207", $"Call to {callable.Name} requires qualifier {callable.Qualifier}.", sentence.Span));
                return null;
            }
            return BindScriptCall(sentence, callable, symbols, pipelineType);
        }
        if (!_language.TryGetVerb(sentence.Verb, out verb))
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
                if (attempt.Candidate is not null)
                    candidates.Add(attempt.Candidate);
                else
                    rejected.Add($"{Signature(implementation, pattern)}: {attempt.Reason}");
            }
        if (candidates.Count == 0)
        {
            _diagnostics.Add(new("FLU-BIND-010", $"No overload of {verb.Name} matches this sentence.", sentence.Span, rejected));
            return null;
        }
        Candidate[] ordered = candidates.OrderBy(x => x.Cost).ThenByDescending(CandidateFit).ThenBy(x => x.Implementation.StableId, StringComparer.Ordinal).ThenBy(x => x.Pattern.StableId, StringComparer.Ordinal).ToArray();
        if (ordered.Length > 1 && ordered[0].Cost == ordered[1].Cost && CandidateFit(ordered[0]) == CandidateFit(ordered[1]))
        {
            _diagnostics.Add(new("FLU-BIND-011", $"Ambiguous overload for {verb.Name} at cost {ordered[0].Cost}.", sentence.Span, ordered.Where(x => x.Cost == ordered[0].Cost).Select(x => Signature(x.Implementation, x.Pattern)).ToArray()));
            return null;
        }
        Candidate selected = ordered[0];
        return new BoundSentence(verb, selected.Implementation, selected.Pattern, selected.Roles, sentence.ResultAlias, selected.Cost, sentence.Span);
    }

    private BoundRecordCreate BindRecordCreate(SentenceNode sentence, FluRecordSchema schema, SymbolScope symbols)
    {
        ExpressionNode[] values = sentence.Clauses.SelectMany(x => x.Values).ToArray();
        if (values.Length != schema.Fields.Count)
            _diagnostics.Add(new("FLU-BIND-233", $"MAKE {schema.Name} requires {schema.Fields.Count} values, got {values.Length}.", sentence.Span));
        var bound = new List<BoundRecordFieldValue>();
        for (int index = 0; index < Math.Min(values.Length, schema.Fields.Count); index++)
        {
            FluRecordField field = schema.Fields[index];
            if (!TryBindValue(values[index], field.Type, RoleDirection.Input, "MAKE", schema.Name, symbols, out BoundValue? value, out _, field.Name))
                _diagnostics.Add(new("FLU-BIND-234", $"Cannot bind record field {field.Name} to {Friendly(field.Type)}.", values[index].Span));
            else
                bound.Add(new(field, value!));
        }
        return new BoundRecordCreate(schema, bound, sentence.ResultAlias, sentence.Span);
    }

    private BoundScriptCall? BindScriptCall(SentenceNode sentence, BoundScriptCallable callable, SymbolScope symbols, Type? pipelineType)
    {
        var supplied = sentence.Clauses.ToDictionary(x => x.RoleName, x => new Queue<ExpressionNode>(x.Values), StringComparer.OrdinalIgnoreCase);
        var arguments = new List<BoundScriptArgument>();
        bool pipelineUsed = false;
        foreach (BoundScriptParameter parameter in callable.Parameters)
        {
            supplied.TryGetValue(parameter.RoleName, out Queue<ExpressionNode>? values);
            ExpressionNode? expression = values is { Count: > 0 } ? values.Dequeue() : null;
            if (expression is null && parameter.RoleName.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && pipelineType is not null && !pipelineUsed && _conversions.TryPlan(pipelineType, parameter.Type, out ConversionPlan? pipelinePlan))
            {
                BoundValue pipeline = new BoundPipelineValue(pipelineType, sentence.Span);
                if (pipelinePlan!.Kind != ConversionKind.Exact)
                    pipeline = new BoundConversionValue(pipeline, parameter.Type, pipelinePlan.Kind, pipelinePlan.Cost, sentence.Span, pipelinePlan);
                arguments.Add(new(parameter, pipeline));
                pipelineUsed = true;
                continue;
            }
            if (expression is null)
            {
                _diagnostics.Add(new("FLU-BIND-210", $"Call to {callable.Name} is missing {parameter.RoleName}.", sentence.Span));
                continue;
            }
            if (!TryBindValue(expression, parameter.Type, RoleDirection.Input, callable.Name, null, symbols, out BoundValue? value, out _, parameter.RoleName))
            {
                _diagnostics.Add(new("FLU-BIND-211", $"Cannot bind {parameter.RoleName} for {callable.Name} to {Friendly(parameter.Type)}.", expression.Span));
                continue;
            }
            arguments.Add(new(parameter, value!));
            if (values is { Count: > 0 })
                _diagnostics.Add(new("FLU-BIND-212", $"Call to {callable.Name} has too many values for {parameter.RoleName}.", values.Peek().Span));
        }
        foreach ((string role, Queue<ExpressionNode> values) in supplied.Where(x => x.Value.Count > 0))
            _diagnostics.Add(new("FLU-BIND-213", $"Role {role} is not declared by {callable.Name}.", values.Peek().Span));
        return new BoundScriptCall(callable, arguments, sentence.ResultAlias, sentence.Span);
    }

    private static bool TryResolveType(string name, out Type? type)
    {
        type = name.ToUpperInvariant() switch
        {
            "TEXT" or "STRING" or "URI" => typeof(string),
            "BOOLEAN" or "BOOL" => typeof(bool),
            "NUMBER" or "DECIMAL" => typeof(decimal),
            "INTEGER" or "INT" => typeof(int),
            "BYTES" => typeof(byte[]),
            "FILE" => typeof(FileInfo),
            "ANY" or "OBJECT" => typeof(object),
            "NONE" or "VOID" => typeof(void),
            _ => Type.GetType(name, throwOnError: false, ignoreCase: true)
        };
        return type is not null;
    }

    private CandidateResult TryCandidate(SentenceNode sentence, VerbDescriptor verb, VerbImplementationDescriptor implementation, SentencePattern pattern, SymbolScope symbols, Type? pipelineType, QualifierDescriptor? qualifier)
    {
        var supplied = new Dictionary<string, Queue<ExpressionNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (ClauseNode clause in sentence.Clauses)
        {
            RoleSlotDescriptor[] matches = pattern.Roles.Where(role => role.AllSurfaceNames.Contains(clause.RoleName, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
                return CandidateResult.Fail($"role {clause.RoleName} is not valid for this pattern");
            if (matches.Length > 1)
                return CandidateResult.Fail($"role {clause.RoleName} is ambiguous in this pattern");
            if (!supplied.TryGetValue(matches[0].Name, out Queue<ExpressionNode>? values))
                supplied[matches[0].Name] = values = new Queue<ExpressionNode>();
            foreach (ExpressionNode value in clause.Values)
                values.Enqueue(value);
        }
        var roles = new List<BoundRole>();
        int cost = pattern.Roles.Count(x => x.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore);
        bool pipelineUsed = false;
        foreach (RoleSlotDescriptor slot in pattern.Roles.OrderBy(x => x.Position))
        {
            supplied.TryGetValue(slot.Name, out Queue<ExpressionNode>? queue);
            queue ??= new Queue<ExpressionNode>();
            var expressions = new List<ExpressionNode>();
            if (slot.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore)
                while (queue.Count > 0)
                    expressions.Add(queue.Dequeue());
            else if (queue.Count > 0)
                expressions.Add(queue.Dequeue());
            if (expressions.Count == 0 && slot.Direction == RoleDirection.Input && pipelineType is not null && slot.Required && !pipelineUsed && _conversions.TryPlan(pipelineType, SlotBindingType(slot), out ConversionPlan? pipelinePlan))
            {
                BoundValue pipeline = new BoundPipelineValue(pipelineType, sentence.Span);
                if (pipelinePlan!.Kind != ConversionKind.Exact)
                    pipeline = new BoundConversionValue(pipeline, SlotBindingType(slot), pipelinePlan.Kind, pipelinePlan.Cost, sentence.Span, pipelinePlan);
                roles.Add(new(slot, new[] { pipeline }, sentence.Span));
                cost += pipelinePlan.Cost + 1;
                pipelineUsed = true;
                continue;
            }
            if (expressions.Count == 0)
            {
                if (slot.Direction == RoleDirection.Output)
                    continue;
                if (slot.Required && slot.Cardinality is RoleCardinality.One or RoleCardinality.OneOrMore)
                    return CandidateResult.Fail($"missing {slot.Name}");
                continue;
            }
            var values = new List<BoundValue>();
            Type expected = SlotBindingType(slot);
            foreach (ExpressionNode expression in expressions)
            {
                if (expression is LiteralExpression { Value: null } && !slot.TypeShape.IsNullable)
                    return CandidateResult.Fail($"{slot.Name} does not accept null");
                if (!TryBindValue(expression, expected, slot.Direction, verb.Name, qualifier?.Name, symbols, out BoundValue? value, out int valueCost, slot.Name))
                    return CandidateResult.Fail($"cannot bind {slot.Name} value to {expected.Name}");
                values.Add(value!);
                cost += valueCost;
            }
            roles.Add(new(slot, values, sentence.Span));
        }
        if (supplied.Values.Any(x => x.Count > 0))
            return CandidateResult.Fail("too many role values");
        return CandidateResult.Ok(new Candidate(implementation, pattern, roles, cost));
    }

    private BoundFilter? BindFilter(FilterStageNode filter, SymbolScope symbols, Type? pipelineType)
    {
        BoundValue? source = BindCollectionSource(filter.Source, symbols, pipelineType, filter.Span, "FILTER", "FLU-BIND-120");
        if (source is null)
            return null;
        Type? element = ClrTypeShape.GetElementType(source.Type);
        if (element is null)
        {
            _diagnostics.Add(new("FLU-BIND-122", $"FILTER requires a collection, got {source.Type.Name}.", filter.Source?.Span ?? filter.Span));
            return null;
        }
        BoundExpression predicate = BindExpression(filter.Predicate, symbols, element);
        if (predicate.Type != typeof(bool))
            _diagnostics.Add(new("FLU-BIND-123", "WHERE expression must be BOOLEAN.", filter.Predicate.Span));
        return new(source, predicate, element, filter.ResultAlias, filter.Span);
    }
    private BoundCollection? BindCollection(CollectionStageNode node, SymbolScope symbols, Type? pipelineType)
    {
        string op = node.Operation.ToUpperInvariant();
        if (!_language.TryGetIntrinsic(op, out IntrinsicDescriptor intrinsic))
        {
            _diagnostics.Add(new("FLU-BIND-164", $"Unknown intrinsic '{op}'.", node.Span));
            return null;
        }

        BoundValue? source = BindCollectionSource(node.Source, symbols, pipelineType, node.Span, intrinsic.Name, "FLU-BIND-160");
        if (source is null)
            return null;
        Type? element = ClrTypeShape.GetElementType(source.Type);
        if (element is null)
        {
            _diagnostics.Add(new("FLU-BIND-161", $"{intrinsic.Name} requires a collection, got {source.Type.Name}.", node.Span));
            return null;
        }

        IntrinsicSemanticKind semantic = intrinsic.Semantic;
        BoundExpression? argument = null;
        if (node.Argument is not null)
            argument = semantic is IntrinsicSemanticKind.Sort or IntrinsicSemanticKind.Group or IntrinsicSemanticKind.Distinct
                ? BindExpression(node.Argument, symbols, element)
                : BindExpression(node.Argument, symbols, null);

        if (semantic is IntrinsicSemanticKind.Take or IntrinsicSemanticKind.Skip && (argument is null || !IsNumeric(argument.Type)))
            _diagnostics.Add(new("FLU-BIND-162", $"{intrinsic.Name} requires a numeric amount.", node.Argument?.Span ?? node.Span));
        if (semantic is IntrinsicSemanticKind.Take or IntrinsicSemanticKind.Skip && argument is not null && TryGetConstantDecimal(argument, out decimal amount) && amount < 0)
            _diagnostics.Add(new("FLU-BIND-166", $"{intrinsic.Name} requires a non-negative amount, got {amount.ToString(CultureInfo.InvariantCulture)}.", node.Argument?.Span ?? node.Span));
        if (semantic is IntrinsicSemanticKind.Sort or IntrinsicSemanticKind.Group && argument is null)
            _diagnostics.Add(new("FLU-BIND-163", $"{intrinsic.Name} requires BY selector.", node.Span));

        BoundValue? strategy = null;
        if (node.Strategy is not null)
        {
            if (intrinsic.StrategyType is null)
            {
                _diagnostics.Add(new("FLU-BIND-164", $"Intrinsic '{intrinsic.Name}' does not accept {intrinsic.StrategyRole} strategy.", node.Strategy.Span));
            }
            else if (!TryBindValue(node.Strategy, intrinsic.StrategyType, RoleDirection.Input, intrinsic.Name, null, symbols, out strategy, out _, intrinsic.StrategyRole))
            {
                _diagnostics.Add(new("FLU-BIND-165", $"Cannot bind {intrinsic.StrategyRole} strategy for {intrinsic.Name} to {Friendly(intrinsic.StrategyType)}.", node.Strategy.Span));
            }
        }

        Type resultType = semantic switch
        {
            IntrinsicSemanticKind.Count => typeof(int),
            IntrinsicSemanticKind.Group when argument is not null => typeof(CollectionGroup<,>).MakeGenericType(argument.Type, element).MakeArrayType(),
            IntrinsicSemanticKind.Group => typeof(object[]),
            _ => element.MakeArrayType()
        };
        return new(intrinsic.Name, source, element, argument, node.ResultAlias, resultType, node.Span, strategy, intrinsic);
    }
    private BoundValue? BindCollectionSource(ExpressionNode? sourceExpression, SymbolScope symbols, Type? pipelineType, TextSpan span, string operation, string code)
    {
        if (sourceExpression is null)
        {
            if (pipelineType is null)
            {
                _diagnostics.Add(new(code, $"{operation} requires a source or pipeline value.", span));
                return null;
            }
            return new BoundPipelineValue(pipelineType, span);
        }
        if (!TryBindValue(sourceExpression, null, RoleDirection.Input, operation, null, symbols, out BoundValue? source, out _))
        {
            _diagnostics.Add(new(code, $"{operation} source cannot be bound.", sourceExpression.Span));
            return null;
        }
        return source;
    }
    private BoundCheck BindCheck(CheckStageNode check, SymbolScope symbols)
    {
        BoundExpression condition = BindExpression(check.Condition, symbols, null);
        if (condition.Type != typeof(bool))
            _diagnostics.Add(new("FLU-BIND-124", "CHECK IF condition must be BOOLEAN.", check.Condition.Span));
        return new(condition, check.ResultAlias, check.Span);
    }

    private BoundIf BindIf(IfNode node, SymbolScope symbols)
    {
        BoundExpression condition = BindExpression(node.Condition, symbols, null);
        if (condition.Type != typeof(bool))
            _diagnostics.Add(new("FLU-BIND-130", "IF condition must be BOOLEAN.", node.Condition.Span));

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
            if (!elseScope.TryGetLocal(name, out Type elseType))
                continue;
            if (TryCommonBranchType(thenType, elseType, out Type commonType))
            {
                if (!parent.Define(name, commonType))
                {
                    _diagnostics.Add(new("FLU-BIND-133", $"Variable '[{name}]' is already defined in an enclosing scope.", span));
                    continue;
                }
                promoted.Add(new(name, commonType));
                continue;
            }
            _diagnostics.Add(new("FLU-BIND-132", $"Variable '[{name}]' has incompatible branch types {Friendly(thenType)} and {Friendly(elseType)}.", span, new[] { Friendly(thenType), Friendly(elseType) }));
        }
        return promoted;
    }

    private static bool TryCommonBranchType(Type left, Type right, out Type common)
    {
        if (left == right)
        {
            common = left;
            return true;
        }
        if (left.IsAssignableFrom(right))
        {
            common = left;
            return true;
        }
        if (right.IsAssignableFrom(left))
        {
            common = right;
            return true;
        }
        common = null!;
        return false;
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
        if (!child.Define(node.Variable, element))
        {
            _diagnostics.Add(new("FLU-BIND-142", $"Iterator '[{node.Variable}]' conflicts with an existing binding.", node.Span));
            child.DefineLocal(node.Variable, element);
        }
        return new(node.Variable, source, element, node.Parallelism, BindBlock(node.Body, child), node.Span);
    }
    private BoundBlock BindBlock(BlockNode block, SymbolScope symbols) => new(block.Statements.Select(x => BindStatement(x, symbols)).Where(x => x is not null).Cast<BoundStatement>().ToArray(), block.Span);

    private BoundExpression BindExpression(ExpressionNode expression, SymbolScope symbols, Type? itemType) => expression switch
    {
        BinaryExpression binary => BindBinary(binary, symbols, itemType),
        BetweenExpression between => BindBetween(between, symbols, itemType),
        UnaryExpression unary => BindUnary(unary, symbols, itemType),
        PredicateExpression predicate => BindPredicate(predicate, symbols, itemType),
        PropertyExpression property when itemType is not null => BindItemPropertyPath(property, itemType),
        IdentifierExpression identifier when itemType is not null => BindItemPropertyPath(identifier, itemType),
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
        if (!_language.TryGetPredicate(predicate.Predicate, out PredicateDescriptor descriptor))
        {
            BoundExpression unknownOperand = BindExpression(predicate.Operand, symbols, itemType);
            _diagnostics.Add(new("FLU-BIND-152", $"Unknown predicate '{predicate.Predicate}'.", predicate.Span));
            return new BoundPredicateExpression(new PredicateDescriptor($"predicate:unknown:{predicate.Predicate.ToLowerInvariant()}", predicate.Predicate, PredicateSyntaxKind.IsState), unknownOperand, predicate.Span);
        }
        BoundExpression operand = BindPredicateOperand(predicate.Operand, descriptor, symbols, itemType);
        if (!descriptor.CanApplyTo(operand.Type))
            _diagnostics.Add(new("FLU-BIND-152", $"Predicate '{descriptor.Name}' cannot evaluate {Friendly(operand.Type)}; expected {DescribeTypes(descriptor.SupportedOperandTypes)}.", predicate.Span));
        else if (!_predicates.CanEvaluate(descriptor.Name, operand.Type))
            _diagnostics.Add(new("FLU-BIND-153", $"Predicate '{descriptor.Name}' has no registered evaluator for {Friendly(operand.Type)}.", predicate.Span));
        return new BoundPredicateExpression(descriptor, operand, predicate.Span);
    }
    private BoundExpression BindPredicateOperand(ExpressionNode expression, PredicateDescriptor descriptor, SymbolScope symbols, Type? itemType)
    {
        if (expression is ReferenceExpression && descriptor.ReferenceOperandType is { } referenceType && TryBindValue(expression, referenceType, RoleDirection.Input, "EXPRESSION", null, symbols, out BoundValue? resolved, out _))
            return new BoundValueExpression(resolved!, expression.Span);
        return BindExpression(expression, symbols, itemType);
    }
    private BoundExpression BindBinary(BinaryExpression binary, SymbolScope symbols, Type? itemType)
    {
        BoundExpression left = BindExpression(binary.Left, symbols, itemType);
        BoundExpression right = BindExpression(binary.Right, symbols, itemType);
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
        BoundExpression operand = BindExpression(between.Operand, symbols, itemType);
        BoundExpression lower = BindExpression(between.Lower, symbols, itemType);
        BoundExpression upper = BindExpression(between.Upper, symbols, itemType);
        if (!_language.TryGetOperator(between.Operator, out OperatorDescriptor descriptor) || descriptor.Arity != OperatorArity.Ternary)
        {
            descriptor = new OperatorDescriptor($"operator:unknown:{between.Operator.ToLowerInvariant()}", between.Operator, 1, OperatorArity.Ternary);
            _diagnostics.Add(new("FLU-BIND-159", $"Unknown ternary operator '{between.Operator}'.", between.Span));
        }
        bool valid = IsOperatorCompatible(descriptor.Compatibility, operand.Type, lower.Type) && IsOperatorCompatible(descriptor.Compatibility, operand.Type, upper.Type);
        if (!valid)
            _diagnostics.Add(new("FLU-BIND-157", $"Operator '{descriptor.Name}' requires compatible bounds; got {Friendly(operand.Type)}, {Friendly(lower.Type)}, {Friendly(upper.Type)}.", between.Span));
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
    private bool CanContain(Type containerType, Type valueType)
    {
        Type container = Nullable.GetUnderlyingType(containerType) ?? containerType;
        Type value = Nullable.GetUnderlyingType(valueType) ?? valueType;
        if (container == typeof(string))
            return value == typeof(string);
        if (typeof(IDictionary).IsAssignableFrom(container))
            return true;
        Type? element = ClrTypeShape.GetElementType(container);
        return element is not null && AreComparable(element, value);
    }
    private bool AreComparable(Type left, Type right)
    {
        Type a = Nullable.GetUnderlyingType(left) ?? left;
        Type b = Nullable.GetUnderlyingType(right) ?? right;
        if (a == typeof(object) || b == typeof(object))
            return true;
        if (a == b || a.IsAssignableFrom(b) || b.IsAssignableFrom(a))
            return true;
        if (IsNumeric(a) && IsNumeric(b))
            return true;
        return _conversions.CanConvert(a, b, out _, out _) || _conversions.CanConvert(b, a, out _, out _);
    }
    private bool AreOrderCompatible(Type left, Type right)
    {
        if (!AreComparable(left, right))
            return false;
        Type a = Nullable.GetUnderlyingType(left) ?? left;
        Type b = Nullable.GetUnderlyingType(right) ?? right;
        return IsNumeric(a) && IsNumeric(b) || IsTemporal(a) && IsTemporal(b) || typeof(IComparable).IsAssignableFrom(a) && (a == b || a.IsAssignableFrom(b) || b.IsAssignableFrom(a));
    }
    private static bool IsTemporal(Type type)
    {
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        return effective == typeof(DateOnly) || effective == typeof(TimeOnly) || effective == typeof(DateTime) || effective == typeof(DateTimeOffset) || effective == typeof(TimeSpan);
    }

    private BoundExpression BindItemPropertyPath(ExpressionNode expression, Type itemType)
    {
        string[] segments = PropertySegments(expression).ToArray();
        if (segments.Length == 0)
            return ErrorExpression(expression);
        Type currentType = itemType;
        var properties = new List<PropertyInfo>();
        foreach (string segment in segments)
        {
            PropertyInfo? property = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                string[] suggestions = ClosestProperties(currentType, segment);
                _diagnostics.Add(new("FLU-BIND-150", $"'{Friendly(currentType)}' has no property '{segment}'.", expression.Span, suggestions));
                return new BoundValueExpression(new BoundConstantValue(null, typeof(object), expression.Span), expression.Span);
            }
            properties.Add(property);
            currentType = property.PropertyType;
        }
        object? Access(object instance)
        {
            object? current = instance;
            foreach (PropertyInfo property in properties)
            {
                if (current is null)
                    return null;
                current = property.GetValue(current);
            }
            return current;
        }
        return new BoundItemPropertyExpression(string.Join('.', segments), currentType, Access, expression.Span);
    }
    private static IEnumerable<string> PropertySegments(ExpressionNode expression)
    {
        switch (expression)
        {
            case IdentifierExpression identifier:
                yield return identifier.Name;
                break;
            case PropertyExpression property:
                foreach (string segment in PropertySegments(property.Target))
                    yield return segment;
                yield return property.Property;
                break;
        }
    }
    private static string[] ClosestProperties(Type type, string requested) => type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(x => (x.Name, Distance: EditDistance(x.Name, requested))).OrderBy(x => x.Distance).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Take(3).Select(x => x.Name).ToArray();
    private static int EditDistance(string left, string right)
    {
        left = left.ToUpperInvariant();
        right = right.ToUpperInvariant();
        int[,] matrix = new int[left.Length + 1, right.Length + 1];
        for (int i = 0; i <= left.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= right.Length; j++)
            matrix[0, j] = j;
        for (int i = 1; i <= left.Length; i++)
            for (int j = 1; j <= right.Length; j++)
                matrix[i, j] = Math.Min(Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1), matrix[i - 1, j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
        return matrix[left.Length, right.Length];
    }

    private BoundExpression ErrorExpression(ExpressionNode expression)
    {
        _diagnostics.Add(new("FLU-BIND-151", "Expression cannot be bound.", expression.Span));
        return new BoundValueExpression(new BoundConstantValue(null, typeof(object), expression.Span), expression.Span);
    }

    private bool TryBindValue(ExpressionNode expression, Type? expected, RoleDirection direction, string verb, string? qualifier, SymbolScope symbols, out BoundValue? bound, out int cost, string? roleName = null)
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
            case BinaryExpression or BetweenExpression or UnaryExpression or PredicateExpression:
                BoundExpression expressionValue = BindExpression(expression, symbols, null);
                bound = new BoundExpressionValue(expressionValue, expression.Span);
                return ApplyExpected(ref bound, expected, expression.Span, out cost);
            case VariableExpression variable:
                if (!symbols.TryGet(variable.Name, out Type variableType))
                {
                    bound = null;
                    cost = 0;
                    return false;
                }
                bound = new BoundVariableValue(variable.Name, variableType, false, variable.Span);
                return ApplyExpected(ref bound, expected, variable.Span, out cost);
            case PropertyExpression property:
                if (TryBindProperty(property, symbols, out BoundPropertyValue? propertyValue))
                {
                    bound = propertyValue;
                    return ApplyExpected(ref bound, expected, property.Span, out cost);
                }
                if (expected is not null && ResolveText(string.Join('.', PropertySegments(property)), expected, property.Span, verb, qualifier, roleName, ResolutionSourceKind.Identifier, out bound, out cost))
                    return true;
                bound = null;
                cost = 0;
                return false;
            case InterpolatedStringExpression interpolation:
                var parts = new List<BoundValue>();
                foreach (ExpressionNode part in interpolation.Parts)
                {
                    if (!TryBindValue(part, null, RoleDirection.Input, verb, qualifier, symbols, out BoundValue? partValue, out _))
                    {
                        bound = null;
                        cost = 0;
                        return false;
                    }
                    parts.Add(partValue!);
                }
                bound = new BoundInterpolatedValue(parts, interpolation.Span);
                return ApplyExpected(ref bound, expected, interpolation.Span, out cost);
            case LiteralExpression literal:
                if (literal.Value is null)
                {
                    if (expected is null)
                    {
                        bound = new BoundConstantValue(null, typeof(object), literal.Span);
                        cost = 0;
                        return true;
                    }
                    bool acceptsNull = !expected.IsValueType || Nullable.GetUnderlyingType(expected) is not null;
                    if (!acceptsNull)
                    {
                        bound = null;
                        cost = 0;
                        return false;
                    }
                    bound = new BoundConstantValue(null, expected, literal.Span, ConversionKind.Assignable, 1);
                    cost = 1;
                    return true;
                }
                if (expected is null)
                {
                    Type literalType = literal.Value.GetType();
                    bound = new BoundConstantValue(literal.Value, literalType, literal.Span);
                    cost = 0;
                    return true;
                }
                if (_conversions.TryConvert(literal.Value, expected, out ConversionResult? conversion))
                {
                    bound = new BoundConstantValue(conversion!.Value, expected, literal.Span, conversion.Kind, conversion.Cost);
                    cost = conversion.Cost;
                    return true;
                }
                return ResolveText(literal.Value.ToString() ?? string.Empty, expected, literal.Span, verb, qualifier, roleName, ResolutionSourceKind.Literal, out bound, out cost);
            case ReferenceExpression reference:
                if (expected is null)
                {
                    bound = new BoundConstantValue(reference.Value, typeof(string), reference.Span);
                    cost = 0;
                    return true;
                }
                return ResolveText(reference.Value, expected, reference.Span, verb, qualifier, roleName, ResolutionSourceKind.Reference, out bound, out cost);
            case IdentifierExpression identifier:
                if (expected is null)
                {
                    bound = new BoundConstantValue(identifier.Name, typeof(string), identifier.Span);
                    cost = 0;
                    return true;
                }
                return ResolveText(identifier.Name, expected, identifier.Span, verb, qualifier, roleName, ResolutionSourceKind.Identifier, out bound, out cost);
            default:
                bound = null;
                cost = 0;
                return false;
        }
    }

    private bool ResolveText(string text, Type expected, TextSpan span, string verb, string? qualifier, string? roleName, ResolutionSourceKind sourceKind, out BoundValue? bound, out int cost)
    {
        var context = new ResolutionContext(expected, roleName, verb, qualifier, _services, SourceKind: sourceKind);
        ResolutionResult resolution = _resolvers.Resolve(text, expected, context);
        if (resolution.Success)
        {
            bound = new BoundConstantValue(resolution.Value, expected, span, ConversionKind.Resolution, 4);
            cost = 4;
            return true;
        }
        if (resolution.Status == ResolutionStatus.Ambiguous)
        {
            _diagnostics.Add(new("FLU-BIND-171", $"Value '{text}' has multiple matching resolvers for {Friendly(expected)}.", span, resolution.Candidates.Select(x => x.Resolver).ToArray()));
        }
        bound = null;
        cost = 0;
        return false;
    }
    private bool ApplyExpected(ref BoundValue? value, Type? expected, TextSpan span, out int cost)
    {
        if (expected is null)
        {
            cost = 0;
            return true;
        }
        ConversionPlanningResult planning = _conversions.Plan(value!.Type, expected);
        if (!planning.Success)
        {
            if (planning.Status == ConversionPlanningStatus.Ambiguous)
                _diagnostics.Add(new("FLU-BIND-170", $"Conversion from {Friendly(value.Type)} to {Friendly(expected)} is ambiguous.", span, planning.Alternatives.Select(x => x.Signature).ToArray()));
            cost = 0;
            return false;
        }
        ConversionPlan plan = planning.Plan!;
        cost = plan!.Cost;
        if (plan.Kind != ConversionKind.Exact)
            value = new BoundConversionValue(value, expected, plan.Kind, plan.Cost, value.Span, plan);
        return true;
    }
    private static Type SlotBindingType(RoleSlotDescriptor slot) => slot.Cardinality is RoleCardinality.OneOrMore or RoleCardinality.ZeroOrMore ? slot.TypeShape.ElementType ?? slot.ValueType : slot.ValueType;
    private static bool QualifierMatches(QualifierDescriptor? qualifier, VerbImplementationDescriptor implementation, SentencePattern pattern)
    {
        if (qualifier is null)
            return true;
        if (implementation.Qualifiers.Contains(qualifier.Name, StringComparer.OrdinalIgnoreCase))
            return true;
        if (qualifier.TargetType is null)
            return false;
        Type target = qualifier.TargetType;
        if (target == implementation.ResultType || target.IsAssignableFrom(implementation.ResultType) || ClrTypeShape.GetElementType(implementation.ResultType) == target)
            return true;
        return pattern.Roles.Any(role => role.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && (role.ValueType == target || role.TypeShape.ElementType == target));
    }
    private void RegisterOutputs(BoundSentence sentence, SymbolScope symbols)
    {
        foreach (BoundVariableValue variable in sentence.Roles.Where(x => x.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput).SelectMany(x => x.Values).OfType<BoundVariableValue>().Where(x => x.IsOutput))
            DefineBinding(symbols, variable.Name, variable.VariableType, variable.Span);
        if (sentence.ResultAlias is { Length: > 0 } alias)
            DefineBinding(symbols, alias, sentence.ResultType, sentence.Span);
    }
    private void DefineBinding(SymbolScope symbols, string name, Type type, TextSpan span)
    {
        if (!symbols.Define(name, type))
            _diagnostics.Add(new("FLU-BIND-131", $"Variable '[{name}]' is already defined; bindings are immutable.", span));
    }

    private static int CandidateFit(Candidate candidate) => candidate.Roles.SelectMany(x => x.Values).OfType<BoundConstantValue>().Sum(value => value.Value switch
    {
        FileInfo file when file.Exists => 3,
        DirectoryInfo directory when directory.Exists => 3,
        FileInfo file when !string.IsNullOrEmpty(file.Extension) => 1,
        _ => 0
    });
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
        if (type == typeof(FluRecord))
        {
            propertyType = typeof(object);
            accessor = instance => ((FluRecord)instance).Get(propertyName);
            return true;
        }
        PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null)
        {
            propertyType = null;
            accessor = null;
            return false;
        }
        var instanceParameter = System.Linq.Expressions.Expression.Parameter(typeof(object), "instance");
        var cast = System.Linq.Expressions.Expression.Convert(instanceParameter, type);
        var read = System.Linq.Expressions.Expression.Property(cast, property);
        accessor = System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(System.Linq.Expressions.Expression.Convert(read, typeof(object)), instanceParameter).Compile();
        propertyType = property.PropertyType;
        return true;
    }
    private static bool TryGetConstantDecimal(BoundExpression expression, out decimal value)
    {
        if (expression is BoundValueExpression { Value: BoundConstantValue constant } && constant.Value is not null && IsNumeric(constant.Value.GetType()))
        {
            try
            {
                value = Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture);
                return true;
            }
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
    private sealed record CandidateResult(Candidate? Candidate, string Reason)
    {
        public static CandidateResult Ok(Candidate candidate) => new(candidate, string.Empty); public static CandidateResult Fail(string reason) => new(null, reason);
    }
    private sealed class SymbolScope(SymbolScope? parent)
    {
        private readonly Dictionary<string, Type> _symbols = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, Type> LocalSymbols => _symbols;
        public bool Define(string name, Type type)
        {
            if (_symbols.ContainsKey(name) || (parent is not null && parent.TryGet(name, out _)))
                return false;
            _symbols[name] = type;
            return true;
        }
        public void DefineLocal(string name, Type type) => _symbols[name] = type;
        public bool TryGetLocal(string name, out Type type) => _symbols.TryGetValue(name, out type!);
        public bool TryGet(string name, out Type type)
        {
            if (_symbols.TryGetValue(name, out type!))
                return true;
            if (parent is not null)
                return parent.TryGet(name, out type!);
            type = null!;
            return false;
        }
    }
}
