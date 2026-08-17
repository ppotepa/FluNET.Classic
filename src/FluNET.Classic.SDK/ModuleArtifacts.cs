using System.Text;
using System.Text.Json;
using FluNET.Classic.Core;

namespace FluNET.Classic.SDK;

public sealed record ModuleArtifacts(string ManifestJson, string DocumentationMarkdown);

public sealed class ModuleArtifactGenerator
{
    public ModuleArtifacts Generate(LanguageSnapshot snapshot, ILanguageModule module) =>
        new(GenerateManifest(snapshot, module), GenerateMarkdown(snapshot, module));

    public string GenerateManifest(LanguageSnapshot snapshot, ILanguageModule module, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(module);
        VerbImplementationDescriptor[] implementations = Implementations(snapshot, module).ToArray();
        object manifest = new
        {
            id = $"module:{Slug(module.Name)}",
            name = module.Name,
            version = module.Version.ToString(),
            dependencies = module.Dependencies,
            assemblies = module.Assemblies.Select(x => x.GetName().Name).Where(x => x is not null),
            qualifiers = module.Qualifiers.Select(q => new { id = q.StableId, name = q.Name, type = q.TargetType?.FullName, aliases = q.AllAliases }),
            verbs = implementations.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(group => new
            {
                name = group.Key,
                overloads = group.Select(i => new
                {
                    id = i.StableId,
                    type = i.ImplementationType.FullName,
                    aliases = i.Aliases,
                    qualifiers = i.Qualifiers,
                    resultType = i.ResultType.FullName,
                    capabilities = i.Capabilities,
                    traits = i.Traits,
                    patterns = i.Patterns.Select(p => new
                    {
                        id = p.StableId,
                        roles = p.Roles.Select(r => new
                        {
                            id = r.StableId,
                            name = r.Name,
                            surface = r.AllSurfaceNames,
                            type = r.ValueType.FullName,
                            elementType = r.TypeShape.ElementType?.FullName,
                            direction = r.Direction.ToString(),
                            cardinality = r.Cardinality.ToString(),
                            required = r.Required
                        })
                    })
                })
            })
        };
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = indented });
    }

    public string GenerateMarkdown(LanguageSnapshot snapshot, ILanguageModule module)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(module);
        var text = new StringBuilder();
        text.AppendLine($"# {module.Name}");
        text.AppendLine();
        text.AppendLine($"Version: `{module.Version}`");
        if (module.Dependencies.Count > 0) text.AppendLine($"Dependencies: {string.Join(", ", module.Dependencies.Select(x => $"`{x}`"))}");
        text.AppendLine();

        foreach (IGrouping<string, VerbImplementationDescriptor> verb in Implementations(snapshot, module).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key))
        {
            text.AppendLine($"## {verb.Key}");
            text.AppendLine();
            foreach (VerbImplementationDescriptor implementation in verb.OrderBy(x => x.StableId, StringComparer.Ordinal))
            foreach (SentencePattern pattern in implementation.Patterns)
            {
                string roles = string.Join(" ", pattern.Roles.OrderBy(x => x.Position).Select(FormatRole).Where(x => x.Length > 0));
                text.AppendLine($"- `{verb.Key}{(roles.Length == 0 ? string.Empty : " " + roles)}` → `{Friendly(implementation.ResultType)}`");
                if (implementation.Qualifiers.Count > 0) text.AppendLine($"  - qualifiers: {string.Join(", ", implementation.Qualifiers.Select(x => $"`{x}`"))}");
                if (implementation.Capabilities.Count > 0) text.AppendLine($"  - capabilities: {string.Join(", ", implementation.Capabilities.Select(x => $"`{x}`"))}");
                if (implementation.Traits.Count > 0) text.AppendLine($"  - traits: {string.Join(", ", implementation.Traits.Select(x => $"`{x}`"))}");
            }
            text.AppendLine();
        }

        return text.ToString().TrimEnd() + Environment.NewLine;
    }

    private static IEnumerable<VerbImplementationDescriptor> Implementations(LanguageSnapshot snapshot, ILanguageModule module)
    {
        HashSet<string> assemblies = module.Assemblies.Select(x => x.GetName().Name ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return snapshot.Verbs.SelectMany(x => x.Implementations).Where(x => assemblies.Contains(x.ImplementationType.Assembly.GetName().Name ?? string.Empty));
    }

    private static string FormatRole(RoleSlotDescriptor role)
    {
        if (role.Direction == RoleDirection.Output) return string.Empty;
        string cardinality = role.Cardinality switch
        {
            RoleCardinality.ZeroOrOne => "?",
            RoleCardinality.ZeroOrMore => "*",
            RoleCardinality.OneOrMore => "+",
            _ => string.Empty
        };
        if (role.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase)) return $"<{Friendly(role.ValueType)}>{cardinality}";
        string surface = role.AllSurfaceNames.Count > 1 ? string.Join("|", role.AllSurfaceNames) : role.Name;
        return $"{surface} <{Friendly(role.ValueType)}>{cardinality}";
    }

    private static string Friendly(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(Friendly))}>"
        : type.Name;

    private static string Slug(string text) => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
