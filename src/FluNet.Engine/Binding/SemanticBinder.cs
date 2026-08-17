using System.Reflection;
using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Binding;

public sealed class SemanticBinder
{
    private readonly LanguageSnapshot _language;
    private readonly ValueResolverRegistry _resolvers;
    private readonly ValueConversionRegistry _conversions;
    private readonly IServiceProvider? _services;

    public SemanticBinder(
        LanguageSnapshot language,
        ValueResolverRegistry? resolvers = null,
        ValueConversionRegistry? conversions = null,
        IServiceProvider? services = null)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _resolvers = resolvers ?? new ValueResolverRegistry();
        _conversions = conversions ?? new ValueConversionRegistry();
        _services = services;
    }

    public BoundScript Bind(ScriptNode script)
    {
        ArgumentNullException.ThrowIfNull(script);

        var diagnostics = new List<BindingDiagnostic>();
        var variables = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var pipelines = new List<BoundPipeline>();

        foreach (PipelineNode pipeline in script.Pipelines)
        {
            pipelines.Add(BindPipeline(pipeline, variables, diagnostics));
        }

        return new BoundScript(
            pipelines,
            new Dictionary<string, Type>(variables, StringComparer.OrdinalIgnoreCase),
            diagnostics);
    }

    private BoundPipeline BindPipeline(
        PipelineNode pipeline,
        Dictionary<string, Type> variables,
        List<BindingDiagnostic> diagnostics)
    {
        var sentences = new List<BoundSentence>();
        Type? pipelineType = null;

        foreach (SentenceNode sentence in pipeline.Sentences)
        {
            BoundSentence? bound = BindSentence(sentence, variables, pipelineType, diagnostics);
            if (bound is null)
            {
                continue;
            }

            sentences.Add(bound);
            pipelineType = bound.ResultType;
            RegisterOutputs(bound, variables);
        }

        return new BoundPipeline(sentences, pipelineType, pipeline.Span);
    }

    private BoundSentence? BindSentence(
        SentenceNode sentence,
        Dictionary<string, Type> variables,
        Type? pipelineType,
        List<BindingDiagnostic> diagnostics)
    {
        if (!_language.TryGetVerb(sentence.Verb, out VerbDescriptor verb))
        {
            diagnostics.Add(new BindingDiagnostic(
                "FLU-BIND-001",
                $"Unknown verb '{sentence.Verb}'.",
                sentence.Span));
            return null;
        }

        QualifierDescriptor? qualifier = null;
        if (sentence.Qualifier is not null && !_language.TryGetQualifier(sentence.Qualifier, out qualifier!))
        {
            diagnostics.Add(new BindingDiagnostic(
                "FLU-BIND-002",
                $"Unknown qualifier '{sentence.Qualifier}'.",
                sentence.Span));
            return null;
        }

        var candidates = new List<Candidate>();
        foreach (VerbImplementationDescriptor implementation in verb.Implementations)
        {
            foreach (SentencePattern pattern in implementation.Patterns)
            {
                Candidate? candidate = TryBindCandidate(
                    sentence,
                    verb,
                    implementation,
                    pattern,
                    qualifier,
                    variables,
                    pipelineType);

                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        if (candidates.Count == 0)
        {
            diagnostics.Add(new BindingDiagnostic(
                "FLU-BIND-003",
                $"No overload of {verb.Name} matches the supplied roles and value types.",
                sentence.Span));
            return null;
        }

        int bestCost = candidates.Min(c => c.Cost);
        Candidate[] best = candidates.Where(c => c.Cost == bestCost).ToArray();
        if (best.Length != 1)
        {
            string signatures = string.Join(", ", best.Select(FormatSignature));
            diagnostics.Add(new BindingDiagnostic(
                "FLU-BIND-004",
                $"Ambiguous overload for {verb.Name}. Candidates: {signatures}.",
                sentence.Span));
            return null;
        }

        Candidate selected = best[0];
        return new BoundSentence(
            verb,
            selected.Implementation,
            selected.Pattern,
            selected.Roles,
            selected.Implementation.ResultType,
            qualifier?.Name,
            sentence.Span,
            selected.Cost);
    }

    private Candidate? TryBindCandidate(
        SentenceNode sentence,
        VerbDescriptor verb,
        VerbImplementationDescriptor implementation,
        SentencePattern pattern,
        QualifierDescriptor? qualifier,
        Dictionary<string, Type> variables,
        Type? pipelineType)
    {
        if (!QualifierMatches(qualifier, implementation, pattern))
        {
            return null;
        }

        Dictionary<string, Queue<ExpressionNode>> supplied = sentence.Clauses
            .GroupBy(c => c.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new Queue<ExpressionNode>(g.SelectMany(c => c.Values)),
                StringComparer.OrdinalIgnoreCase);

        var roles = new List<BoundRole>();
        int totalCost = 0;

        foreach (RoleSlotDescriptor slot in pattern.Roles.OrderBy(r => r.Position))
        {
            supplied.TryGetValue(slot.Name, out Queue<ExpressionNode>? values);
            values ??= new Queue<ExpressionNode>();

            int take = slot.Cardinality switch
            {
                RoleCardinality.One => 1,
                RoleCardinality.ZeroOrOne => Math.Min(1, values.Count),
                RoleCardinality.OneOrMore => values.Count,
                RoleCardinality.ZeroOrMore => values.Count,
                _ => 0
            };

            if (slot.Cardinality is RoleCardinality.One or RoleCardinality.OneOrMore && take == 0)
            {
                if (slot.Direction == RoleDirection.Input && pipelineType is not null &&
                    CanUseType(pipelineType, slot.ValueType, out int pipelineCost))
                {
                    roles.Add(new BoundRole(
                        slot,
                        [new BoundPipelineValue(pipelineType, sentence.Span)],
                        sentence.Span));
                    totalCost += pipelineCost + 1;
                    continue;
                }

                return null;
            }

            var boundValues = new List<BoundValue>();
            for (int i = 0; i < take; i++)
            {
                ExpressionNode expression = values.Dequeue();
                if (!TryBindValue(expression, slot, verb.Name, qualifier?.Name, variables, out BoundValue? value, out int cost))
                {
                    return null;
                }

                boundValues.Add(value!);
                totalCost += cost;
            }

            if (slot.Cardinality == RoleCardinality.One && values.Count > 0)
            {
                return null;
            }

            if (boundValues.Count > 0)
            {
                roles.Add(new BoundRole(slot, boundValues, sentence.Span));
            }
        }

        if (supplied.Values.Any(q => q.Count > 0))
        {
            return null;
        }

        return new Candidate(implementation, pattern, roles, totalCost);
    }

    private bool TryBindValue(
        ExpressionNode expression,
        RoleSlotDescriptor slot,
        string verbName,
        string? qualifier,
        Dictionary<string, Type> variables,
        out BoundValue? bound,
        out int cost)
    {
        Type expectedType = slot.Cardinality is RoleCardinality.OneOrMore or RoleCardinality.ZeroOrMore
            ? slot.TypeShape.ElementType ?? slot.ValueType
            : slot.ValueType;

        if (slot.Direction == RoleDirection.Output)
        {
            if (expression is VariableExpression output)
            {
                bound = new BoundVariableValue(output.Name, expectedType, true, output.Span);
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
                if (!variables.TryGetValue(variable.Name, out Type? variableType) ||
                    !CanUseType(variableType, expectedType, out cost))
                {
                    bound = null;
                    cost = 0;
                    return false;
                }

                bound = new BoundVariableValue(variable.Name, variableType, false, variable.Span);
                return true;

            case PropertyExpression property:
                if (!TryBindProperty(property, variables, out BoundPropertyValue? propertyValue, out Type? propertyType) ||
                    !CanUseType(propertyType!, expectedType, out cost))
                {
                    bound = null;
                    cost = 0;
                    return false;
                }

                bound = propertyValue;
                return true;

            case InterpolatedStringExpression interpolated:
                if (expectedType != typeof(string))
                {
                    bound = null;
                    cost = 0;
                    return false;
                }

                var parts = new List<BoundValue>();
                foreach (ExpressionNode part in interpolated.Parts)
                {
                    if (part is LiteralExpression literalPart)
                    {
                        parts.Add(new BoundConstantValue(literalPart.Value, typeof(string), literalPart.Span));
                    }
                    else if (part is VariableExpression variablePart && variables.TryGetValue(variablePart.Name, out Type? partType))
                    {
                        parts.Add(new BoundVariableValue(variablePart.Name, partType, false, variablePart.Span));
                    }
                    else
                    {
                        bound = null;
                        cost = 0;
                        return false;
                    }
                }

                bound = new BoundInterpolatedValue(parts, interpolated.Span);
                cost = 0;
                return true;

            case LiteralExpression literalExpression:
                return TryResolveText(
                    literalExpression.Value,
                    literalExpression.Span,
                    expectedType,
                    slot,
                    verbName,
                    qualifier,
                    out bound,
                    out cost);

            case ReferenceExpression referenceExpression:
                return TryResolveText(
                    referenceExpression.Value,
                    referenceExpression.Span,
                    expectedType,
                    slot,
                    verbName,
                    qualifier,
                    out bound,
                    out cost);

            default:
                bound = null;
                cost = 0;
                return false;
        }
    }

    private bool TryResolveText(
        string text,
        TextSpan span,
        Type expectedType,
        RoleSlotDescriptor slot,
        string verbName,
        string? qualifier,
        out BoundValue? bound,
        out int cost)
    {
        var context = new ResolutionContext(expectedType, slot.Name, verbName, qualifier, _services);
        if (_resolvers.TryResolve(text, expectedType, context, out object? resolved))
        {
            bound = new BoundConstantValue(resolved, expectedType, span, ConversionKind.Resolution, 4);
            cost = 4;
            return true;
        }

        bound = null;
        cost = 0;
        return false;
    }

    private static bool TryBindProperty(
        PropertyExpression property,
        Dictionary<string, Type> variables,
        out BoundPropertyValue? bound,
        out Type? type)
    {
        if (property.Target is not VariableExpression variable ||
            !variables.TryGetValue(variable.Name, out Type? targetType))
        {
            bound = null;
            type = null;
            return false;
        }

        PropertyInfo? propertyInfo = targetType.GetProperty(
            property.Property,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (propertyInfo is null)
        {
            bound = null;
            type = null;
            return false;
        }

        var target = new BoundVariableValue(variable.Name, targetType, false, variable.Span);
        bound = new BoundPropertyValue(target, propertyInfo.Name, propertyInfo.PropertyType, property.Span);
        type = propertyInfo.PropertyType;
        return true;
    }

    private bool CanUseType(Type sourceType, Type targetType, out int cost)
    {
        if (sourceType == targetType)
        {
            cost = 0;
            return true;
        }

        if (targetType.IsAssignableFrom(sourceType))
        {
            cost = 1;
            return true;
        }

        if (sourceType.IsValueType)
        {
            object? sample = Activator.CreateInstance(sourceType);
            if (sample is not null && _conversions.TryConvert(sample, targetType, out ConversionResult? result))
            {
                cost = result!.Cost;
                return true;
            }
        }

        cost = 0;
        return false;
    }

    private static bool QualifierMatches(
        QualifierDescriptor? qualifier,
        VerbImplementationDescriptor implementation,
        SentencePattern pattern)
    {
        if (qualifier?.TargetType is null)
        {
            return true;
        }

        Type target = qualifier.TargetType;
        if (implementation.ResultType is not null &&
            (target == implementation.ResultType || target.IsAssignableFrom(implementation.ResultType)))
        {
            return true;
        }

        return pattern.Roles.Any(r =>
            r.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) &&
            (target == r.ValueType || target.IsAssignableFrom(r.ValueType) ||
             r.TypeShape.ElementType == target));
    }

    private static void RegisterOutputs(BoundSentence sentence, Dictionary<string, Type> variables)
    {
        foreach (BoundRole role in sentence.Roles.Where(r => r.Slot.Direction is RoleDirection.Output or RoleDirection.InputOutput))
        {
            foreach (BoundVariableValue variable in role.Values.OfType<BoundVariableValue>().Where(v => v.IsOutput))
            {
                variables[variable.Name] = variable.VariableType;
            }
        }
    }

    private static string FormatSignature(Candidate candidate)
    {
        string roles = string.Join(" ", candidate.Pattern.Roles.Select(r => $"{r.Name}:{r.ValueType.Name}"));
        return $"{candidate.Implementation.ImplementationType.Name}({roles})";
    }

    private sealed record Candidate(
        VerbImplementationDescriptor Implementation,
        SentencePattern Pattern,
        IReadOnlyList<BoundRole> Roles,
        int Cost);
}
