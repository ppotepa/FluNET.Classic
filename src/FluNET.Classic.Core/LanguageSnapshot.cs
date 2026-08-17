using System.Collections.ObjectModel;

namespace FluNET.Classic.Core;

public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, VerbDescriptor> _verbs;
    private readonly IReadOnlyDictionary<string, QualifierDescriptor> _qualifiers;

    public LanguageSnapshot(IEnumerable<VerbDescriptor> verbs, IEnumerable<QualifierDescriptor> qualifiers, IEnumerable<ModuleDescriptor> modules)
    {
        Dictionary<string, VerbDescriptor> verbLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs)
        {
            verbLookup.Add(verb.Name, verb);
            foreach (string alias in verb.Aliases) verbLookup.Add(alias, verb);
        }

        Dictionary<string, QualifierDescriptor> qualifierLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (QualifierDescriptor qualifier in qualifiers)
        {
            qualifierLookup[qualifier.Name] = qualifier;
            foreach (string alias in qualifier.AllAliases) qualifierLookup[alias] = qualifier;
        }

        _verbs = new ReadOnlyDictionary<string, VerbDescriptor>(verbLookup);
        _qualifiers = new ReadOnlyDictionary<string, QualifierDescriptor>(qualifierLookup);
        Verbs = verbLookup.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Qualifiers = qualifierLookup.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Modules = modules.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<VerbDescriptor> Verbs { get; }
    public IReadOnlyList<QualifierDescriptor> Qualifiers { get; }
    public IReadOnlyList<ModuleDescriptor> Modules { get; }

    public bool TryGetVerb(string name, out VerbDescriptor descriptor) => _verbs.TryGetValue(name, out descriptor!);
    public VerbDescriptor GetVerb(string name) => TryGetVerb(name, out VerbDescriptor result) ? result : throw new KeyNotFoundException($"Unknown verb '{name}'.");
    public bool TryGetQualifier(string name, out QualifierDescriptor descriptor) => _qualifiers.TryGetValue(name, out descriptor!);
    public IReadOnlyList<VerbImplementationDescriptor> GetOverloads(string verb) => GetVerb(verb).Implementations;
}
