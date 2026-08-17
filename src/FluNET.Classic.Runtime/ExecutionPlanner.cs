using FluNET.Classic.Binding;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

public sealed record ExecutionPlanDiagnostic(string Source, string Code, string Message);

public sealed record ExecutionPlanValue(string Kind, string Type, string? Detail = null, string? Conversion = null, int Cost = 0);

public sealed record ExecutionPlanRole(
    string Name,
    string Direction,
    string Cardinality,
    string ValueType,
    IReadOnlyList<ExecutionPlanValue> Values);

public sealed record ExecutionPlanStep(
    string Kind,
    string? Verb,
    string? Implementation,
    string? Pattern,
    string? ResultType,
    string? ResultAlias,
    int? BindingCost,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ExecutionTrait> Traits,
    IReadOnlyList<ExecutionPlanRole> Roles,
    IReadOnlyList<ExecutionPlanStep> Children);

public sealed record ExecutionPlan(
    bool Success,
    IReadOnlyList<ExecutionPlanDiagnostic> Diagnostics,
    IReadOnlyList<ExecutionPlanStep> Steps,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<ExecutionTrait> Traits,
    string? ResultType);

public sealed class ExecutionPlanner
{
    public ExecutionPlan Build(CheckResult check)
    {
        ArgumentNullException.ThrowIfNull(check);
        var diagnostics = new List<ExecutionPlanDiagnostic>();
        diagnostics.AddRange(check.Parse.Diagnostics.Select(x => new ExecutionPlanDiagnostic("syntax", x.Code, x.Message)));
        diagnostics.AddRange(check.Bound?.Diagnostics.Select(x => new ExecutionPlanDiagnostic("binding", x.Code, x.Message)) ?? Array.Empty<ExecutionPlanDiagnostic>());

        ExecutionPlanStep[] steps = check.Bound?.Statements.Select(BuildStatement).ToArray() ?? Array.Empty<ExecutionPlanStep>();
        ExecutionPlanStep[] all = Flatten(steps).ToArray();
        string[] capabilities = all.SelectMany(x => x.Capabilities).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        ExecutionTrait[] traits = all.SelectMany(x => x.Traits).Distinct().OrderBy(x => x).ToArray();
        string? resultType = check.Bound?.Statements.LastOrDefault() is BoundPipeline pipeline ? TypeName(pipeline.ResultType) : null;
        return new(check.Success, diagnostics, steps, capabilities, traits, resultType);
    }

    private static ExecutionPlanStep BuildStatement(BoundStatement statement) => statement switch
    {
        BoundPipeline pipeline => new(
            "pipeline", null, null, null, TypeName(pipeline.ResultType), null, null,
            Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), pipeline.Stages.Select(BuildStage).ToArray()),
        BoundIf conditional => new(
            "if", null, null, null, null, null, null,
            Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), Branches(conditional)),
        BoundForEach loop => new(
            "forEach", null, null, null, null, loop.Variable, null,
            Array.Empty<string>(), Array.Empty<ExecutionTrait>(),
            new[] { new ExecutionPlanRole("IN", "Input", "One", TypeName(loop.Source.Type), new[] { DescribeValue(loop.Source) }) },
            loop.Body.Statements.Select(BuildStatement).ToArray()),
        _ => new(statement.GetType().Name, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), Array.Empty<ExecutionPlanStep>())
    };

    private static IReadOnlyList<ExecutionPlanStep> Branches(BoundIf conditional)
    {
        var branches = new List<ExecutionPlanStep>
        {
            new("then", null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), conditional.Then.Statements.Select(BuildStatement).ToArray())
        };
        if (conditional.Else is not null)
            branches.Add(new("else", null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), conditional.Else.Statements.Select(BuildStatement).ToArray()));
        return branches;
    }

    private static ExecutionPlanStep BuildStage(BoundStage stage) => stage switch
    {
        BoundSentence sentence => new(
            "sentence",
            sentence.Verb.Name,
            sentence.Implementation.ImplementationType.FullName,
            sentence.Pattern.StableId,
            TypeName(sentence.ResultType),
            sentence.ResultAlias,
            sentence.Cost,
            sentence.Implementation.Capabilities,
            sentence.Implementation.Traits,
            sentence.Roles.Select(role => new ExecutionPlanRole(
                role.Slot.Name,
                role.Slot.Direction.ToString(),
                role.Slot.Cardinality.ToString(),
                TypeName(role.Slot.ValueType),
                role.Values.Select(DescribeValue).ToArray())).ToArray(),
            Array.Empty<ExecutionPlanStep>()),
        BoundFilter filter => new(
            "filter", "FILTER", null, null, TypeName(filter.ResultType), filter.ResultAlias, null,
            Array.Empty<string>(), new[] { ExecutionTrait.Pure },
            new[] { new ExecutionPlanRole("WHAT", "Input", "One", TypeName(filter.Source.Type), new[] { DescribeValue(filter.Source) }) },
            Array.Empty<ExecutionPlanStep>()),
        BoundCheck check => new(
            "check", "CHECK", null, null, TypeName(check.ResultType), check.ResultAlias, null,
            Array.Empty<string>(), new[] { ExecutionTrait.Pure }, Array.Empty<ExecutionPlanRole>(), Array.Empty<ExecutionPlanStep>()),
        _ => new(stage.GetType().Name, null, null, null, TypeName(stage.ResultType), null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), Array.Empty<ExecutionPlanStep>())
    };

    private static ExecutionPlanValue DescribeValue(BoundValue value) => value switch
    {
        BoundConstantValue constant => new(constant.Kind == ConversionKind.Resolution ? "resolved" : "constant", TypeName(constant.Type), null, constant.Kind == ConversionKind.Exact ? null : constant.Kind.ToString(), constant.Cost),
        BoundVariableValue variable => new(variable.IsOutput ? "output" : "variable", TypeName(variable.Type), variable.Name),
        BoundPipelineValue pipeline => new("pipeline", TypeName(pipeline.Type)),
        BoundPropertyValue property => new("property", TypeName(property.Type), property.Property),
        BoundInterpolatedValue interpolation => new("interpolation", TypeName(interpolation.Type), $"{interpolation.Parts.Count} part(s)"),
        BoundConversionValue conversion => new("conversion", TypeName(conversion.Type), $"{TypeName(conversion.Source.Type)} -> {TypeName(conversion.TargetType)}", conversion.Kind.ToString(), conversion.Cost),
        _ => new(value.GetType().Name, TypeName(value.Type))
    };

    private static IEnumerable<ExecutionPlanStep> Flatten(IEnumerable<ExecutionPlanStep> steps)
    {
        foreach (ExecutionPlanStep step in steps)
        {
            yield return step;
            foreach (ExecutionPlanStep child in Flatten(step.Children)) yield return child;
        }
    }

    private static string? TypeName(Type? type) => type?.FullName ?? type?.Name;
}
