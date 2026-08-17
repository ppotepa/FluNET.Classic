using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Binding;

public sealed record BindingDiagnostic(string Code, string Message, TextSpan Span, IReadOnlyList<string>? Candidates = null);

public sealed record BoundScript(IReadOnlyList<BoundStatement> Statements, IReadOnlyList<BindingDiagnostic> Diagnostics);
public abstract record BoundStatement(TextSpan Span);
public sealed record BoundBlock(IReadOnlyList<BoundStatement> Statements, TextSpan Span);
public sealed record BoundPipeline(IReadOnlyList<BoundStage> Stages, Type? ResultType, TextSpan Span) : BoundStatement(Span);
public abstract record BoundStage(Type ResultType, TextSpan Span);

public sealed record BoundSentence(
    VerbDescriptor Verb,
    VerbImplementationDescriptor Implementation,
    SentencePattern Pattern,
    IReadOnlyList<BoundRole> Roles,
    string? ResultAlias,
    int Cost,
    TextSpan Span) : BoundStage(Implementation.ResultType, Span);

public sealed record BoundFilter(BoundValue Source, BoundExpression Predicate, Type ElementType, string? ResultAlias, TextSpan Span)
    : BoundStage(ElementType.MakeArrayType(), Span);

public sealed record BoundCheck(BoundExpression Condition, string? ResultAlias, TextSpan Span)
    : BoundStage(typeof(bool), Span);

public sealed record BoundIf(BoundExpression Condition, BoundBlock Then, BoundBlock? Else, TextSpan Span) : BoundStatement(Span);
public sealed record BoundForEach(string Variable, BoundValue Source, Type ElementType, BoundBlock Body, TextSpan Span) : BoundStatement(Span);

public sealed record BoundRole(RoleSlotDescriptor Slot, IReadOnlyList<BoundValue> Values, TextSpan Span);

public abstract record BoundValue(Type Type, TextSpan Span);
public sealed record BoundConstantValue(object? Value, Type ConstantType, TextSpan Span, ConversionKind Kind = ConversionKind.Exact, int Cost = 0) : BoundValue(ConstantType, Span);
public sealed record BoundVariableValue(string Name, Type VariableType, bool IsOutput, TextSpan Span) : BoundValue(VariableType, Span);
public sealed record BoundPipelineValue(Type PipelineType, TextSpan Span) : BoundValue(PipelineType, Span);
public sealed record BoundPropertyValue(BoundValue Target, string Property, Type PropertyType, Func<object, object?> Accessor, TextSpan Span) : BoundValue(PropertyType, Span);
public sealed record BoundInterpolatedValue(IReadOnlyList<BoundValue> Parts, TextSpan Span) : BoundValue(typeof(string), Span);
public sealed record BoundConversionValue(BoundValue Source, Type TargetType, ConversionKind Kind, int Cost, TextSpan Span) : BoundValue(TargetType, Span);

public abstract record BoundExpression(Type Type, TextSpan Span);
public sealed record BoundValueExpression(BoundValue Value, TextSpan Span) : BoundExpression(Value.Type, Span);
public sealed record BoundItemPropertyExpression(string Property, Type PropertyType, Func<object, object?> Accessor, TextSpan Span) : BoundExpression(PropertyType, Span);
public sealed record BoundUnaryExpression(string Operator, BoundExpression Operand, Type ResultType, TextSpan Span) : BoundExpression(ResultType, Span);
public sealed record BoundPredicateExpression(string Predicate, BoundExpression Operand, TextSpan Span) : BoundExpression(typeof(bool), Span);
public sealed record BoundBinaryExpression(BoundExpression Left, string Operator, BoundExpression Right, Type ResultType, TextSpan Span) : BoundExpression(ResultType, Span);
