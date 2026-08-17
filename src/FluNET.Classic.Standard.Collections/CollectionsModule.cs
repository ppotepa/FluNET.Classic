using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Collections;

/// <summary>
/// Owns collection-oriented language semantics. FILTER/WHERE is intentionally implemented
/// as a typed intrinsic in the compiler so it preserves IEnumerable&lt;T&gt; element types
/// without reflection-specific verb hacks.
/// </summary>
public sealed class CollectionsModule : LanguageModule
{
    public override string Name => "collections";
}
