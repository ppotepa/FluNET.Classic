using FluNET.Classic.Binding;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

public sealed record ExecutionPlanDiagnostic(string Source, string Code, string Message);
public sealed record ExecutionPlanConversionStep(string SourceType, string TargetType, string Kind, int Cost);
public sealed record ExecutionPlanValue(
    string Kind,
    string Type,
    string? Detail = null,
    string? Conversion = null,
    int Cost = 0,
    bool Sensitive = false,
    IReadOnlyList<ExecutionPlanConversionStep>? ConversionSteps = null);
public sealed record ExecutionPlanRole(
    string Name,
    string Direction,
    string Cardinality,
    string ValueType,
    IReadOnlyList<ExecutionPlanValue> Values,
    bool Sensitive = false,
    string? Projection = null);
public sealed record ExecutionPlanStep(string Kind, string? Verb, string? Implementation, string? Pattern, string? ResultType, string? ResultAlias, int? BindingCost, string? ExecutionMode, IReadOnlyList<string> Capabilities, IReadOnlyList<ExecutionTrait> Traits, IReadOnlyList<ExecutionPlanRole> Roles, IReadOnlyList<ExecutionPlanStep> Children, bool Sensitive = false);
public sealed record ExecutionPlan
{
    public bool Success
    {
        get;
    }

    public IReadOnlyList<ExecutionPlanDiagnostic> Diagnostics
    {
        get;
    }

    public IReadOnlyList<ExecutionPlanStep> Steps
    {
        get;
    }

    public IReadOnlyList<string> RequiredCapabilities
    {
        get;
    }

    public IReadOnlyList<ExecutionTrait> Traits
    {
        get;
    }

    public string? ResultType
    {
        get;
    }

    public ExecutionPlan(
        bool Success,
        IReadOnlyList<ExecutionPlanDiagnostic> Diagnostics,
        IReadOnlyList<ExecutionPlanStep> Steps,
        IReadOnlyList<string> RequiredCapabilities,
        IReadOnlyList<ExecutionTrait> Traits,
        string? ResultType)
    {
        this.Success = Success;
        this.Diagnostics = (Diagnostics ?? throw new ArgumentNullException(nameof(Diagnostics))).ToArray();
        this.Steps = (Steps ?? throw new ArgumentNullException(nameof(Steps))).ToArray();
        this.RequiredCapabilities = (RequiredCapabilities ?? throw new ArgumentNullException(nameof(RequiredCapabilities))).ToArray();
        this.Traits = (Traits ?? throw new ArgumentNullException(nameof(Traits))).ToArray();
        this.ResultType = ResultType;
    }
}

public sealed class ExecutionPlanner
{
    private readonly LanguageSnapshot _language;

    public ExecutionPlanner(LanguageSnapshot language) => _language = language;

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

    private ExecutionPlanStep BuildStatement(BoundStatement statement) => statement switch
    {
        BoundPipeline pipeline => new("pipeline", null, null, null, TypeName(pipeline.ResultType), null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), pipeline.Stages.Select(BuildStage).ToArray(), pipeline.Stages.LastOrDefault()?.IsSensitive == true),
        BoundIf conditional => new("if", null, null, null, null, null, null, null, ExpressionCapabilities(conditional.Condition), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), Branches(conditional), conditional.Condition.IsSensitive),
        BoundForEach loop => new("forEach", null, null, null, null, loop.Variable, null, loop.Parallelism is { } workers ? $"Parallel({workers})" : ClrTypeShape.IsAsyncEnumerableType(loop.Source.Type) ? "Streaming" : null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), new[] { new ExecutionPlanRole("IN", "Input", "One", TypeName(loop.Source.Type)!, new[] { DescribeValue(loop.Source) }, loop.Source.IsSensitive) }, loop.Body.Statements.Select(BuildStatement).ToArray(), loop.Source.IsSensitive),
        BoundTry @try => new("try", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), TryBranches(@try)),
        _ => Empty(statement.GetType().Name)
    };

    private IReadOnlyList<ExecutionPlanStep> TryBranches(BoundTry @try)
    {
        var branches = new List<ExecutionPlanStep>
        {
            new("body", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), @try.Body.Statements.Select(BuildStatement).ToArray())
        };
        if (@try.Failure is not null)
            branches.Add(new("failure", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), @try.Failure.Statements.Select(BuildStatement).ToArray()));
        if (@try.Finally is not null)
            branches.Add(new("finally", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), @try.Finally.Statements.Select(BuildStatement).ToArray()));
        return branches;
    }

    private IReadOnlyList<ExecutionPlanStep> Branches(BoundIf conditional)
    {
        var branches = new List<ExecutionPlanStep> { new("then", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), conditional.Then.Statements.Select(BuildStatement).ToArray()) };
        if (conditional.Else is not null)
            branches.Add(new("else", null, null, null, null, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), conditional.Else.Statements.Select(BuildStatement).ToArray()));
        return branches;
    }

    private ExecutionPlanStep BuildStage(BoundStage stage) => stage switch
    {
        BoundSentence sentence => new("sentence", sentence.Verb.Name, sentence.Implementation.ImplementationType.FullName, sentence.Pattern.StableId, TypeName(sentence.ResultType), sentence.ResultAlias, sentence.Cost, sentence.Implementation.Traits.Contains(ExecutionTrait.Streaming) ? "Streaming" : null, sentence.Implementation.Capabilities, sentence.Implementation.Traits, sentence.Roles.Select(role => new ExecutionPlanRole(role.Slot.Name, role.Slot.Direction.ToString(), role.Slot.Cardinality.ToString(), TypeName(role.Slot.ValueType)!, role.Values.Select(DescribeValue).ToArray(), role.IsSensitive, Projection(role.Slot.OutputProjection))).ToArray(), Array.Empty<ExecutionPlanStep>(), sentence.IsSensitive),
        BoundScriptCall call => new("script", call.Callable.Name, null, null, TypeName(call.ResultType), call.ResultAlias, null, "Nested", Array.Empty<string>(), call.Callable.IsFunction ? new[] { ExecutionTrait.Pure } : Array.Empty<ExecutionTrait>(), call.Arguments.Select(argument => new ExecutionPlanRole(argument.Parameter.RoleName, "Input", "One", TypeName(argument.Parameter.Type)!, new[] { DescribeValue(argument.Value) }, argument.Value.IsSensitive)).ToArray(), call.Callable.Body.Statements.Select(BuildStatement).ToArray(), call.IsSensitive),
        BoundRecordCreate record => new("record", "MAKE", null, null, TypeName(record.ResultType), record.ResultAlias, null, "Immutable", Array.Empty<string>(), new[] { ExecutionTrait.Pure }, record.Fields.Select(field => new ExecutionPlanRole(field.Field.Name, "Input", "One", TypeName(field.Field.Type)!, new[] { DescribeValue(field.Value) }, field.Value.IsSensitive)).ToArray(), Array.Empty<ExecutionPlanStep>(), record.IsSensitive),
        BoundFilter filter => new("filter", "FILTER", null, null, TypeName(filter.ResultType), filter.ResultAlias, null, ClrTypeShape.IsAsyncEnumerableType(filter.Source.Type) ? "Streaming" : "Materializing", ExpressionCapabilities(filter.Predicate), new[] { ExecutionTrait.Pure }, new[] { new ExecutionPlanRole("WHAT", "Input", "One", TypeName(filter.Source.Type)!, new[] { DescribeValue(filter.Source) }, filter.Source.IsSensitive) }, Array.Empty<ExecutionPlanStep>(), filter.IsSensitive),
        BoundCheck check => new("check", "CHECK", null, null, TypeName(check.ResultType), check.ResultAlias, null, "Scalar", ExpressionCapabilities(check.Condition), new[] { ExecutionTrait.Pure }, Array.Empty<ExecutionPlanRole>(), Array.Empty<ExecutionPlanStep>(), check.IsSensitive),
        BoundCollection collection => new("collection", collection.Operation, null, null, TypeName(collection.ResultType), collection.ResultAlias, null, IntrinsicExecutionMode(collection.Operation), Array.Empty<string>(), new[] { ExecutionTrait.Pure }, CollectionRoles(collection), Array.Empty<ExecutionPlanStep>(), collection.IsSensitive),
        _ => Empty(stage.GetType().Name, TypeName(stage.ResultType))
    };

    private IReadOnlyList<ExecutionPlanRole> CollectionRoles(BoundCollection collection)
    {
        var roles = new List<ExecutionPlanRole>
        {
            new("WHAT", "Input", "One", TypeName(collection.Source.Type)!, new[] { DescribeValue(collection.Source) }, collection.Source.IsSensitive)
        };

        if (collection.Argument is not null)
        {
            string argumentRole = collection.Operation is "SORT" or "GROUP" or "DISTINCT" ? "BY" : "WITH";
            roles.Add(new(argumentRole, "Input", "One", TypeName(collection.Argument.Type)!, Array.Empty<ExecutionPlanValue>(), collection.Argument.IsSensitive));
        }

        if (collection.Strategy is not null)
        {
            string strategyRole = _language.TryGetIntrinsic(collection.Operation, out IntrinsicDescriptor intrinsic)
                ? intrinsic.StrategyRole
                : "USING";
            roles.Add(new(strategyRole, "Input", "One", TypeName(collection.Strategy.Type)!, new[] { DescribeValue(collection.Strategy) }, collection.Strategy.IsSensitive));
        }

        return roles;
    }

    private static string? Projection(OutputProjectionDescriptor? projection) => projection?.Kind switch
    {
        OutputProjectionKind.Member => $"member:{projection.Member}",
        OutputProjectionKind.Index => $"index:{projection.Index}",
        OutputProjectionKind.WholeResult => "whole",
        _ => null
    };

    private string? IntrinsicExecutionMode(string operation) => _language.TryGetIntrinsic(operation, out IntrinsicDescriptor intrinsic) ? intrinsic.Execution.ToString() : null;
    private static ExecutionPlanStep Empty(string kind, string? resultType = null) => new(kind, null, null, null, resultType, null, null, null, Array.Empty<string>(), Array.Empty<ExecutionTrait>(), Array.Empty<ExecutionPlanRole>(), Array.Empty<ExecutionPlanStep>());
    private static ExecutionPlanValue DescribeValue(BoundValue value) => value switch
    {
        BoundConstantValue constant => new(constant.Kind == ConversionKind.Resolution ? "resolved" : "constant", TypeName(constant.Type)!, constant.IsSensitive ? "***" : constant.Value?.ToString(), constant.Kind == ConversionKind.Exact ? null : constant.Kind.ToString(), constant.Cost, constant.IsSensitive),
        BoundVariableValue variable => new(variable.IsOutput ? "output" : "variable", TypeName(variable.Type)!, variable.Name, Sensitive: variable.IsSensitive),
        BoundPipelineValue pipeline => new("pipeline", TypeName(pipeline.Type)!, Sensitive: pipeline.IsSensitive),
        BoundPropertyValue property => new("property", TypeName(property.Type)!, property.Property, Sensitive: property.IsSensitive),
        BoundInterpolatedValue interpolation => new("interpolation", TypeName(interpolation.Type)!, $"{interpolation.Parts.Count} part(s)", Sensitive: interpolation.IsSensitive),
        BoundExpressionValue expression => new("expression", TypeName(expression.Type)!, Sensitive: expression.IsSensitive),
        BoundConversionValue conversion => new(
            "conversion",
            TypeName(conversion.Type)!,
            $"{TypeName(conversion.Source.Type)} -> {TypeName(conversion.TargetType)}",
            conversion.Kind.ToString(),
            conversion.Cost,
            conversion.IsSensitive,
            conversion.Steps.Select(step => new ExecutionPlanConversionStep(
                TypeName(step.SourceType)!,
                TypeName(step.TargetType)!,
                step.Kind.ToString(),
                step.Cost)).ToArray()),
        _ => new(value.GetType().Name, TypeName(value.Type)!, Sensitive: value.IsSensitive)
    };
    private static string[] ExpressionCapabilities(BoundExpression expression) => EnumerateExpressionCapabilities(expression).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static IEnumerable<string> EnumerateExpressionCapabilities(BoundExpression expression)
    {
        if (expression is BoundPredicateExpression predicate)
            foreach (string capability in predicate.Descriptor.CapabilitiesFor(predicate.Operand.Type))
                yield return capability;
        switch (expression)
        {
            case BoundUnaryExpression unary:
                foreach (string capability in EnumerateExpressionCapabilities(unary.Operand))
                    yield return capability;
                break;
            case BoundBinaryExpression binary:
                foreach (string capability in EnumerateExpressionCapabilities(binary.Left))
                    yield return capability;
                foreach (string capability in EnumerateExpressionCapabilities(binary.Right))
                    yield return capability;
                break;
            case BoundBetweenExpression between:
                foreach (string capability in EnumerateExpressionCapabilities(between.Operand))
                    yield return capability;
                foreach (string capability in EnumerateExpressionCapabilities(between.Lower))
                    yield return capability;
                foreach (string capability in EnumerateExpressionCapabilities(between.Upper))
                    yield return capability;
                break;
            case BoundPredicateExpression boundPredicate:
                foreach (string capability in EnumerateExpressionCapabilities(boundPredicate.Operand))
                    yield return capability;
                break;
        }
    }
    private static IEnumerable<ExecutionPlanStep> Flatten(IEnumerable<ExecutionPlanStep> steps)
    {
        foreach (ExecutionPlanStep step in steps)
        {
            yield return step;
            foreach (ExecutionPlanStep child in Flatten(step.Children))
                yield return child;
        }
    }
    private static string? TypeName(Type? type) => type?.FullName ?? type?.Name;
}
