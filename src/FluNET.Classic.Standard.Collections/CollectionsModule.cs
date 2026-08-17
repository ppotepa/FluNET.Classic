using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Collections;

/// <summary>
/// Owns collection-oriented language semantics. FILTER/WHERE and the collection stages are
/// compiler intrinsics so they preserve the source element type without reflection-specific
/// verb wrappers. Their surface grammar is declared here rather than hardcoded in the parser.
/// </summary>
public sealed class CollectionsModule : LanguageModule
{
    public override string Name => "collections";

    public override IReadOnlyCollection<IntrinsicDescriptor> Intrinsics => new IntrinsicDescriptor[]
    {
        new("intrinsic:collections:sort", "SORT", IntrinsicSyntaxKind.CollectionBy),
        new("intrinsic:collections:group", "GROUP", IntrinsicSyntaxKind.CollectionBy),
        new("intrinsic:collections:take", "TAKE", IntrinsicSyntaxKind.CollectionAmountFrom),
        new("intrinsic:collections:skip", "SKIP", IntrinsicSyntaxKind.CollectionAmountFrom),
        new("intrinsic:collections:distinct", "DISTINCT", IntrinsicSyntaxKind.CollectionDistinct),
        new("intrinsic:collections:count", "COUNT", IntrinsicSyntaxKind.CollectionSourceOptional)
    };
}
