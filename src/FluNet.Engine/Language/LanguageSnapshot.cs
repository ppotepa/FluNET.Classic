using System.Collections.ObjectModel;

namespace FluNET.Language;

public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, VerbDescriptor> _verbs;

    public LanguageSnapshot(IEnumerable<VerbDescriptor> verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);

        Dictionary<string, VerbDescriptor> lookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs)
        {
            lookup[verb.Name] = verb;
            foreach (string alias in verb.Aliases)
            {
                lookup[alias] = verb;
            }
        }

        _verbs = new ReadOnlyDictionary<string, VerbDescriptor>(lookup);
        Verbs = lookup.Values.Distinct().OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<VerbDescriptor> Verbs { get; }

    public bool TryGetVerb(string name, out VerbDescriptor descriptor) =>
        _verbs.TryGetValue(name, out descriptor!);

    public VerbDescriptor GetVerb(string name) =>
        TryGetVerb(name, out VerbDescriptor descriptor)
            ? descriptor
            : throw new KeyNotFoundException($"Unknown verb '{name}'.");
}
