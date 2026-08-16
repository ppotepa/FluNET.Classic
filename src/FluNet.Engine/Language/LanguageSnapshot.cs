using System.Collections.ObjectModel;

namespace FluNET.Language;

public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, VerbDescriptor> _verbs;
    private readonly IReadOnlyDictionary<string, QualifierDescriptor> _qualifiers;

    public LanguageSnapshot(
        IEnumerable<VerbDescriptor> verbs,
        IEnumerable<QualifierDescriptor>? qualifiers = null)
    {
        ArgumentNullException.ThrowIfNull(verbs);

        Dictionary<string, VerbDescriptor> verbLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs)
        {
            verbLookup[verb.Name] = verb;
            foreach (string alias in verb.Aliases)
            {
                verbLookup[alias] = verb;
            }
        }

        _verbs = new ReadOnlyDictionary<string, VerbDescriptor>(verbLookup);
        Verbs = verbLookup.Values.Distinct().OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        Dictionary<string, QualifierDescriptor> qualifierLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (QualifierDescriptor qualifier in qualifiers ?? StandardQualifiers.All)
        {
            qualifierLookup[qualifier.Name] = qualifier;
            foreach (string alias in qualifier.AllAliases)
            {
                qualifierLookup[alias] = qualifier;
            }
        }

        _qualifiers = new ReadOnlyDictionary<string, QualifierDescriptor>(qualifierLookup);
        Qualifiers = qualifierLookup.Values.Distinct().OrderBy(q => q.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<VerbDescriptor> Verbs { get; }

    public IReadOnlyList<QualifierDescriptor> Qualifiers { get; }

    public bool TryGetVerb(string name, out VerbDescriptor descriptor) =>
        _verbs.TryGetValue(name, out descriptor!);

    public VerbDescriptor GetVerb(string name) =>
        TryGetVerb(name, out VerbDescriptor descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"Unknown verb '{name}'.");

    public bool TryGetQualifier(string name, out QualifierDescriptor descriptor) =>
        _qualifiers.TryGetValue(name, out descriptor!);
}
