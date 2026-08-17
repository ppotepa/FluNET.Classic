using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Binding;

public sealed record BindingDiagnostic(string Code, string Message, TextSpan Span, IReadOnlyList<string>? Candidates = null);
public sealed record BoundScript(IReadOnlyList<BoundStatement> Statements, IReadOnlyList<BindingDiagnostic> Diagnostics);
public abstract record BoundStatement(TextSpan Span);
public sealed record BoundBlock(IReadOnlyList<BoundStatement> Statements, TextSpan Span);
public sealed record BoundPipeline(IReadOnlyList<BoundStage> Stages, Type? ResultType, TextSpan Span) : BoundStatement(Span);
public abstract record BoundStage(Type ResultType, TextSpan Span)
{
    public virtual bool IsSensitive => SensitiveValueMetadata.IsSensitiveType(ResultType);
}
public sealed record BoundSentence(VerbDescriptor Verb, VerbImplementationDescriptor Implementation, SentencePattern Pattern, IReadOnlyList<BoundRole> Roles, string? ResultAlias, int Cost, TextSpan Span) : BoundStage(Implementation.ResultType, Span);
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
public sealed record BoundCollection(string Operation, BoundValue Source, Type ElementType, BoundExpression? Argument, string? ResultAlias, Type CollectionResultType, TextSpan Span, BoundValue? Strategy = null)
    : BoundStage(ResolveCollectionResultType(Operation, Source.Type, ElementType, Argument, CollectionResultType), Span)
{
    public override bool IsSensitive => Source.IsSensitive || SensitiveValueMetadata.IsSensitiveType(ElementType);
    private static Type ResolveCollectionResultType(string operation, Type sourceType, Type elementType, BoundExpression? argument, Type fallback)
    {
        string normalized = operation.ToUpperInvariant();
        if (normalized == "GROUP" && argument is not null)
            return typeof(CollectionGroup<,>).MakeGenericType(argument.Type, elementType).MakeArrayType();

        if (!ClrTypeShape.IsAsyncEnumerableType(sourceType)) return fallback;
        return normalized switch
        {
            "TAKE" or "SKIP" or "DISTINCT" => typeof(IAsyncEnumerable<>).MakeGenericType(elementType),
            _ => fallback
        };
    }
}
public sealed record BoundFlowVariable(string Name, Type Type);
public sealed record BoundIf(BoundExpression Condition, BoundBlock Then, BoundBlock? Else, IReadOnlyList<BoundFlowVariable> PromotedVariables, TextSpan Span) : BoundStatement(Span);
public sealed record BoundForEach(string Variable, BoundValue Source, Type ElementType, BoundBlock Body, TextSpan Span) : BoundStatement(Span);
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
public sealed record BoundConversionValue(BoundValue Source, Type TargetType, ConversionKind Kind, int Cost, TextSpan Span) : BoundValue(TargetType, Span)
{
    public override bool IsSensitive => base.IsSensitive || Source.IsSensitive;
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
