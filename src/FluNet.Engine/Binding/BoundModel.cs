using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Binding;

public sealed record BoundScript(
    IReadOnlyList<BoundPipeline> Pipelines,
    IReadOnlyDictionary<string, Type> Variables,
    IReadOnlyList<BindingDiagnostic> Diagnostics);

public sealed record BoundPipeline(
    IReadOnlyList<BoundSentence> Sentences,
    Type? ResultType,
    TextSpan Span);

public sealed record BoundSentence(
    VerbDescriptor Verb,
    VerbImplementationDescriptor Implementation,
    SentencePattern Pattern,
    IReadOnlyList<BoundRole> Roles,
    Type? ResultType,
    string? Qualifier,
    TextSpan Span,
    int Cost);

public sealed record BoundRole(
    RoleSlotDescriptor Slot,
    IReadOnlyList<BoundValue> Values,
    TextSpan Span);

public abstract record BoundValue(Type Type, TextSpan Span);

public sealed record BoundConstantValue(
    object? Value,
    Type ValueType,
    TextSpan ValueSpan,
    ConversionKind ConversionKind = ConversionKind.Exact,
    int ConversionCost = 0)
    : BoundValue(ValueType, ValueSpan);

public sealed record BoundVariableValue(
    string Name,
    Type VariableType,
    bool IsOutput,
    TextSpan VariableSpan)
    : BoundValue(VariableType, VariableSpan);

public sealed record BoundPipelineValue(
    Type PipelineType,
    TextSpan PipelineSpan)
    : BoundValue(PipelineType, PipelineSpan);

public sealed record BoundPropertyValue(
    BoundValue Target,
    string Property,
    Type PropertyType,
    TextSpan PropertySpan)
    : BoundValue(PropertyType, PropertySpan);

public sealed record BoundInterpolatedValue(
    IReadOnlyList<BoundValue> Parts,
    TextSpan InterpolatedSpan)
    : BoundValue(typeof(string), InterpolatedSpan);

public sealed record BindingDiagnostic(
    string Code,
    string Message,
    TextSpan Span);
