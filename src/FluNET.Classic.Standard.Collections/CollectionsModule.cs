using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Collections;

/// <summary>
/// Owns collection-oriented language semantics. FILTER/WHERE and the collection stages are
/// compiler intrinsics so they preserve the source element type without reflection-specific
/// verb wrappers. Their surface grammar and execution semantics are declared here rather than
/// hardcoded in the parser or planner.
/// </summary>
public sealed class CollectionsModule : LanguageModule
{
    public override string Name => "collections";

    public override IReadOnlyCollection<IntrinsicDescriptor> Intrinsics => new IntrinsicDescriptor[]
    {
        new("intrinsic:collections:sort", "SORT", IntrinsicSyntaxKind.CollectionBy, Execution: IntrinsicExecutionKind.Materializing),
        new("intrinsic:collections:group", "GROUP", IntrinsicSyntaxKind.CollectionBy, Execution: IntrinsicExecutionKind.Materializing),
        new("intrinsic:collections:take", "TAKE", IntrinsicSyntaxKind.CollectionAmountFrom, Execution: IntrinsicExecutionKind.Streaming),
        new("intrinsic:collections:skip", "SKIP", IntrinsicSyntaxKind.CollectionAmountFrom, Execution: IntrinsicExecutionKind.Streaming),
        new("intrinsic:collections:distinct", "DISTINCT", IntrinsicSyntaxKind.CollectionDistinct, Execution: IntrinsicExecutionKind.Streaming),
        new("intrinsic:collections:count", "COUNT", IntrinsicSyntaxKind.CollectionSourceOptional, Execution: IntrinsicExecutionKind.Scalar)
    };
}
