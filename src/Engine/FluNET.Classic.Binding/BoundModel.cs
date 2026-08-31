using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Binding;

public enum BindingDiagnosticSeverity
{
    Info, Warning, Error
}
public sealed record CandidateDetail(string PatternId, IReadOnlyList<string> RoleFailures);
public sealed record BindingDiagnostic(string Code, string Message, TextSpan Span, IReadOnlyList<string>? Candidates = null, BindingDiagnosticSeverity Severity = BindingDiagnosticSeverity.Error)
{
    public IReadOnlyList<CandidateDetail>? CandidateDetails => Candidates?.Select(candidate =>
        {
            int separator = candidate.IndexOf(':');
            string pattern = separator > 0 ? candidate[..separator] : candidate;
            string failure = separator > 0 ? candidate[(separator + 1)..].Trim() : candidate;
            return new CandidateDetail(pattern, string.IsNullOrWhiteSpace(failure) ? Array.Empty<string>() : new[] { failure });
        }).ToArray();
}
public sealed record BoundScript(IReadOnlyList<BoundStatement> Statements, IReadOnlyList<BindingDiagnostic> AllDiagnostics, IReadOnlyDictionary<string, BoundScriptCallable>? Definitions = null)
{
    public IReadOnlyList<BindingDiagnostic> Diagnostics => Errors;
    public bool HasErrors => AllDiagnostics.Any(x => x.Severity == BindingDiagnosticSeverity.Error);
    public IReadOnlyList<BindingDiagnostic> Errors => AllDiagnostics.Where(x => x.Severity == BindingDiagnosticSeverity.Error).ToArray();
    public IReadOnlyList<BindingDiagnostic> Warnings => AllDiagnostics.Where(x => x.Severity == BindingDiagnosticSeverity.Warning).ToArray();
    public IReadOnlyList<BindingDiagnostic> Infos => AllDiagnostics.Where(x => x.Severity == BindingDiagnosticSeverity.Info).ToArray();
}
public abstract record BoundStatement(TextSpan Span);
public sealed record BoundBlock(IReadOnlyList<BoundStatement> Statements, TextSpan Span);
public sealed record BoundPipeline(IReadOnlyList<BoundStage> Stages, Type? ResultType, TextSpan Span) : BoundStatement(Span);
public abstract record BoundStage(Type ResultType, TextSpan Span)
{
    public virtual bool IsSensitive => SensitiveValueMetadata.IsSensitiveType(ResultType);
}
public sealed record BoundSentence(VerbDescriptor Verb, VerbImplementationDescriptor Implementation, SentencePattern Pattern, IReadOnlyList<BoundRole> Roles, string? ResultAlias, int Cost, TextSpan Span) : BoundStage(Implementation.ResultType, Span)
{
    public override bool IsSensitive => base.IsSensitive || Roles.Any(x => x.IsSensitive);
}
public sealed record BoundFilter(BoundValue Source, BoundExpression Predicate, Type ElementType, string? ResultAlias, TextSpan Span)
    : BoundStage(SequenceResultType(Source.Type, ElementType), Span)
{
    public override bool IsSensitive => Source.IsSensitive || SensitiveValueMetadata.IsSensitiveType(ElementType);
    private static Type SequenceResultType(Type sourceType, Type elementType) =>
        ClrTypeShape.IsAsyncEnumerableType(sourceType)
            ? typeof(IAsyncEnumerable<>).MakeGenericType(elementType)
            : elementType.MakeArrayType();
}
public sealed record BoundCheck(BoundExpression Condition, string? ResultAlias, TextSpan Span) : BoundStage(typeof(bool), Span)
{
    public override bool IsSensitive => Condition.IsSensitive;
}
public sealed record BoundCollection(
    string Operation,
    BoundValue Source,
    Type ElementType,
    BoundExpression? Argument,
    string? ResultAlias,
    Type CollectionResultType,
    TextSpan Span,
    BoundValue? Strategy = null,
    IntrinsicDescriptor? Descriptor = null)
    : BoundStage(ResolveCollectionResultType(Descriptor, Operation, Source.Type, ElementType, Argument, CollectionResultType), Span)
{
    public IntrinsicSemanticKind Semantic => (Descriptor ?? throw new InvalidOperationException($"Intrinsic '{Operation}' has no language descriptor.")).Semantic;
    public override bool IsSensitive => Source.IsSensitive || SensitiveValueMetadata.IsSensitiveType(ElementType);

    private static Type ResolveCollectionResultType(IntrinsicDescriptor? descriptor, string operation, Type sourceType, Type elementType, BoundExpression? argument, Type fallback)
    {
        IntrinsicSemanticKind semantic = (descriptor ?? throw new InvalidOperationException($"Intrinsic '{operation}' has no language descriptor.")).Semantic;
        if (semantic == IntrinsicSemanticKind.Group && argument is not null)
            return typeof(CollectionGroup<,>).MakeGenericType(argument.Type, elementType).MakeArrayType();

        if (!ClrTypeShape.IsAsyncEnumerableType(sourceType))
            return fallback;
        return semantic switch
        {
            IntrinsicSemanticKind.Take or IntrinsicSemanticKind.Skip or IntrinsicSemanticKind.Distinct => typeof(IAsyncEnumerable<>).MakeGenericType(elementType),
            _ => fallback
        };
    }

}
public sealed record BoundScriptParameter(string RoleName, string Name, Type Type);
public sealed class BoundScriptCallable(string name, string? qualifier, DefinitionKind kind, IReadOnlyList<BoundScriptParameter> parameters, Type returnType, BoundBlock body, TextSpan span)
{
    public string Name { get; } = name;
    public string? Qualifier { get; } = qualifier;
    public DefinitionKind Kind { get; } = kind;
    public IReadOnlyList<BoundScriptParameter> Parameters { get; } = parameters;
    public Type ReturnType { get; } = returnType;
    public BoundBlock Body { get; set; } = body;
    public TextSpan Span { get; } = span;
    public bool IsFunction => Kind == DefinitionKind.Function;
}
public sealed record BoundScriptCall(BoundScriptCallable Callable, IReadOnlyList<BoundScriptArgument> Arguments, string? ResultAlias, TextSpan Span) : BoundStage(Callable.ReturnType, Span)
{
    public override bool IsSensitive => Arguments.Any(x => x.Value.IsSensitive) || SensitiveValueMetadata.IsSensitiveType(ResultType);
}
public sealed record BoundScriptArgument(BoundScriptParameter Parameter, BoundValue Value);
public sealed record BoundRecordCreate(FluRecordSchema Schema, IReadOnlyList<BoundRecordFieldValue> Fields, string? ResultAlias, TextSpan Span) : BoundStage(typeof(FluRecord), Span)
{
    public override bool IsSensitive => Fields.Any(x => x.Value.IsSensitive);
}
public sealed record BoundRecordFieldValue(FluRecordField Field, BoundValue Value);
public sealed record BoundReturn(BoundValue? Value, TextSpan Span) : BoundStatement(Span);
public sealed record BoundFlowVariable(string Name, Type Type);
public sealed record BoundIf(BoundExpression Condition, BoundBlock Then, BoundBlock? Else, IReadOnlyList<BoundFlowVariable> PromotedVariables, TextSpan Span) : BoundStatement(Span);
public sealed record BoundForEach(string Variable, BoundValue Source, Type ElementType, int? Parallelism, BoundBlock Body, TextSpan Span) : BoundStatement(Span);
public sealed record BoundTry(BoundBlock Body, BoundBlock? Failure, BoundBlock? Finally, TextSpan Span) : BoundStatement(Span);
public sealed record BoundRole(RoleSlotDescriptor Slot, IReadOnlyList<BoundValue> Values, TextSpan Span)
{
    public bool IsSensitive => Values.Any(x => x.IsSensitive) || SensitiveValueMetadata.IsSensitiveType(Slot.ValueType);
}
public abstract record BoundValue(Type Type, TextSpan Span)
{
    public virtual bool IsSensitive => SensitiveValueMetadata.IsSensitiveType(Type);
}
public sealed record BoundConstantValue(object? Value, Type ConstantType, TextSpan Span, ConversionKind Kind = ConversionKind.Exact, int Cost = 0) : BoundValue(ConstantType, Span)
{
    public override bool IsSensitive => base.IsSensitive || SensitiveValueMetadata.IsSensitiveValue(Value);
}
public sealed record BoundVariableValue(string Name, Type VariableType, bool IsOutput, TextSpan Span) : BoundValue(VariableType, Span);
public sealed record BoundPipelineValue(Type PipelineType, TextSpan Span) : BoundValue(PipelineType, Span);
public sealed record BoundPropertyValue(BoundValue Target, string Property, Type PropertyType, Func<object, object?> Accessor, TextSpan Span) : BoundValue(PropertyType, Span)
{
    public override bool IsSensitive => base.IsSensitive || Target.IsSensitive;
}
public sealed record BoundInterpolatedValue(IReadOnlyList<BoundValue> Parts, TextSpan Span) : BoundValue(typeof(string), Span)
{
    public override bool IsSensitive => Parts.Any(x => x.IsSensitive);
}
public sealed record BoundExpressionValue(BoundExpression Expression, TextSpan Span) : BoundValue(Expression.Type, Span)
{
    public override bool IsSensitive => Expression.IsSensitive;
}
public sealed record BoundConversionValue(BoundValue Source, Type TargetType, ConversionKind Kind, int Cost, TextSpan Span, ConversionPlan? Plan = null) : BoundValue(TargetType, Span)
{
    public override bool IsSensitive => base.IsSensitive || Source.IsSensitive;
    public IReadOnlyList<ConversionStep> Steps => Plan?.Steps ?? Array.Empty<ConversionStep>();
}
public abstract record BoundExpression(Type Type, TextSpan Span)
{
    public virtual bool IsSensitive => SensitiveValueMetadata.IsSensitiveType(Type);
}
public sealed record BoundValueExpression(BoundValue Value, TextSpan Span) : BoundExpression(Value.Type, Span)
{
    public override bool IsSensitive => Value.IsSensitive;
}
public sealed record BoundItemPropertyExpression(string Property, Type PropertyType, Func<object, object?> Accessor, TextSpan Span) : BoundExpression(PropertyType, Span);
public sealed record BoundUnaryExpression(OperatorDescriptor Descriptor, BoundExpression Operand, TextSpan Span) : BoundExpression(Descriptor.EffectiveResultType, Span)
{
    public string Operator => Descriptor.Name;
    public override bool IsSensitive => base.IsSensitive || Operand.IsSensitive;
}
public sealed record BoundPredicateExpression(PredicateDescriptor Descriptor, BoundExpression Operand, TextSpan Span) : BoundExpression(typeof(bool), Span)
{
    public string Predicate => Descriptor.Name;
    public override bool IsSensitive => Operand.IsSensitive;
}
public sealed record BoundBinaryExpression(BoundExpression Left, OperatorDescriptor Descriptor, BoundExpression Right, TextSpan Span) : BoundExpression(Descriptor.EffectiveResultType, Span)
{
    public string Operator => Descriptor.Name;
    public override bool IsSensitive => base.IsSensitive || Left.IsSensitive || Right.IsSensitive;
}
public sealed record BoundBetweenExpression(OperatorDescriptor Descriptor, BoundExpression Operand, BoundExpression Lower, BoundExpression Upper, TextSpan Span) : BoundExpression(Descriptor.EffectiveResultType, Span)
{
    public string Operator => Descriptor.Name;
    public override bool IsSensitive => base.IsSensitive || Operand.IsSensitive || Lower.IsSensitive || Upper.IsSensitive;
}
