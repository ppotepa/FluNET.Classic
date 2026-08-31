namespace FluNET.Classic.Core;

/// <summary>Canonical semantic role names used by sentence metadata.</summary>
public static class LanguageRoleNames
{
    public const string What = "WHAT";
    public const string From = "FROM";
    public const string To = "TO";
    public const string Using = "USING";
    public const string With = "WITH";
    public const string As = "AS";
    public const string In = "IN";
    public const string At = "AT";
    public const string For = "FOR";
    public const string Until = "UNTIL";
    public const string By = "BY";

    public static IReadOnlySet<string> Contextual
    {
        get;
    } = new HashSet<string>(
        new[] { What, From, To, Using, With, As, In, At, For, Until, By },
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Structural words that must never be claimed by a sentence role or role alias.
    /// Words such as AS and FOR remain contextual roles as well as structural words in
    /// specific constructs, so they are intentionally excluded from this set.
    /// </summary>
    public static IReadOnlySet<string> StructuralOnly
    {
        get;
    } = StandardLanguageSurface.StructuralSyntax
        .SelectMany(surface => surface.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Where(surface => !Contextual.Contains(surface))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsContextual(string? name) => name is not null && Contextual.Contains(name);
}
