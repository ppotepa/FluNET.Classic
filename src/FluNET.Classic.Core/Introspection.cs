using System.Text.Json;

namespace FluNET.Classic.Core;

public sealed class LanguageIntrospectionService(LanguageSnapshot snapshot)
{
    public LanguageSnapshot Snapshot { get; } = snapshot;

    public string ToJson(bool indented = true)
    {
        object manifest = new
        {
            verbs = Snapshot.Verbs.Select(v => new
            {
                id = v.StableId,
                name = v.Name,
                aliases = v.Aliases,
                overloads = v.Implementations.Select(i => new
                {
                    id = i.StableId,
                    type = i.ImplementationType.FullName,
                    qualifiers = i.Qualifiers,
                    resultType = i.ResultType.FullName,
                    capabilities = i.Capabilities,
                    traits = i.Traits,
                    patterns = i.Patterns.Select(p => new
                    {
                        id = p.StableId,
                        roles = p.Roles.Select(r => new
                        {
                            name = r.Name,
                            surfaceNames = r.AllSurfaceNames,
                            parameter = r.ParameterName,
                            type = r.ValueType.FullName,
                            elementType = r.TypeShape.ElementType?.FullName,
                            direction = r.Direction.ToString(),
                            cardinality = r.Cardinality.ToString(),
                            required = r.Required
                        })
                    })
                })
            }),
            qualifiers = Snapshot.Qualifiers.Select(q => new { id = q.StableId, name = q.Name, type = q.TargetType?.FullName, aliases = q.AllAliases }),
            modules = Snapshot.Modules
        };

        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = indented });
    }

    public string DescribeVerb(string name)
    {
        VerbDescriptor verb = Snapshot.GetVerb(name);
        return string.Join(Environment.NewLine, verb.Implementations.SelectMany(i => i.Patterns.Select(p =>
            $"{verb.Name} {string.Join(" ", p.Roles.Select(r => $"{r.Name}:{Friendly(r.ValueType)}[{r.Cardinality}]"))} -> {Friendly(i.ResultType)}")));
    }

    private static string Friendly(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(Friendly))}>"
        : type.Name;
}
