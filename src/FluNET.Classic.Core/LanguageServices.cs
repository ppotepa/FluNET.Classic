namespace FluNET.Classic.Core;

public sealed record CompletionItem(string Label, string Kind, string? Detail = null);
public sealed record HoverInfo(string Label, string Detail);

public sealed class ClassicLanguageService(LanguageSnapshot language)
{
    public IReadOnlyList<CompletionItem> Complete(string? prefix = null)
    {
        prefix ??= string.Empty;
        var items = new List<CompletionItem>();
        items.AddRange(language.Verbs.Where(x => x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x.Name, "verb", $"{x.Implementations.Count} overload(s)")));
        items.AddRange(language.Qualifiers.Where(x => x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x.Name, "qualifier", x.TargetType?.Name)));
        items.AddRange(language.Verbs.SelectMany(v => v.Implementations).SelectMany(i => i.Patterns).SelectMany(p => p.Roles).Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x, "role")));
        return items.OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public HoverInfo? Hover(string token)
    {
        if (language.TryGetVerb(token, out VerbDescriptor verb)) return new(verb.Name, $"{verb.Implementations.Count} overload(s): {string.Join("; ", verb.Implementations.Select(x => x.ResultType.Name).Distinct())}");
        if (language.TryGetQualifier(token, out QualifierDescriptor qualifier)) return new(qualifier.Name, qualifier.TargetType?.FullName ?? "behavior qualifier");
        return null;
    }
}
