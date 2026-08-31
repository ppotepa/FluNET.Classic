using FluNET.Classic.Core;
using System.Text;
using System.Text.Json;

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
            languageContract = ClassicLanguageContract.Id,
            dependencies = module.Dependencies,
            assemblies = module.Assemblies.Select(x => x.GetName().Name).Where(x => x is not null),
            qualifiers = module.Qualifiers.Select(q => new { id = q.StableId, name = q.Name, type = q.TargetType?.FullName, sensitive = q.TargetType is not null && SensitiveValueMetadata.IsSensitiveType(q.TargetType), aliases = q.AllAliases }),
            predicates = module.Predicates.Select(p => new
            {
                id = p.StableId,
                name = p.Name,
                surface = p.AllSurfaceNames,
                syntax = p.Syntax.ToString(),
                precedence = p.Precedence,
                operandTypes = p.SupportedOperandTypes.Select(x => x.FullName),
                referenceOperandType = p.ReferenceOperandType?.FullName,
                capabilities = p.RequiredCapabilities
            }),
            operators = module.Operators.Select(o => new
            {
                id = o.StableId,
                name = o.Name,
                surface = o.AllSurfaceNames,
                precedence = o.Precedence,
                arity = o.Arity.ToString(),
                associativity = o.Associativity.ToString(),
                semantic = o.Semantic.ToString(),
                compatibility = o.Compatibility.ToString(),
                evaluation = o.Evaluation.ToString(),
                resultType = o.EffectiveResultType.FullName
            }),
            intrinsics = module.Intrinsics.Select(i => new
            {
                id = i.StableId,
                name = i.Name,
                surface = i.AllSurfaceNames,
                syntax = i.Syntax.ToString(),
                semantic = i.Semantic.ToString(),
                execution = i.Execution.ToString(),
                strategyType = i.StrategyType?.FullName,
                strategyRole = i.StrategyType is null ? null : i.StrategyRole
            }),
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
                    resultSensitive = SensitiveValueMetadata.IsSensitiveType(i.ResultType),
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
                            nullable = r.TypeShape.IsNullable,
                            sensitive = SensitiveValueMetadata.IsSensitiveType(r.ValueType),
                            direction = r.Direction.ToString(),
                            cardinality = r.Cardinality.ToString(),
                            required = r.Required,
                            outputProjection = Projection(r.OutputProjection)
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
        text.AppendLine($"Language contract: `{ClassicLanguageContract.Id}`");
        if (module.Dependencies.Count > 0)
            text.AppendLine($"Dependencies: {string.Join(", ", module.Dependencies.Select(x => $"`{x}`"))}");
        text.AppendLine();

        if (module.Predicates.Count > 0)
        {
            text.AppendLine("## Predicates");
            text.AppendLine();
            foreach (PredicateDescriptor predicate in module.Predicates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                text.AppendLine($"- `{predicate.Name}` — `{predicate.Syntax}`, precedence `{predicate.Precedence}`; operands: `{DescribeTypes(predicate.SupportedOperandTypes)}`");
            text.AppendLine();
        }

        if (module.Operators.Count > 0)
        {
            text.AppendLine("## Operators");
            text.AppendLine();
            foreach (OperatorDescriptor @operator in module.Operators.OrderBy(x => x.Precedence).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                text.AppendLine($"- `{@operator.Name}` — `{@operator.Arity}`, precedence `{@operator.Precedence}`, compatibility `{@operator.Compatibility}`, evaluation `{@operator.Evaluation}`");
            text.AppendLine();
        }

        if (module.Intrinsics.Count > 0)
        {
            text.AppendLine("## Intrinsics");
            text.AppendLine();
            foreach (IntrinsicDescriptor intrinsic in module.Intrinsics.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                text.AppendLine($"- `{intrinsic.Name}` — syntax `{intrinsic.Syntax}`, semantic `{intrinsic.Semantic}`, execution `{intrinsic.Execution}`{(intrinsic.StrategyType is null ? string.Empty : $", {intrinsic.StrategyRole} `{Friendly(intrinsic.StrategyType)}`")}");
            text.AppendLine();
        }

        foreach (IGrouping<string, VerbImplementationDescriptor> verb in Implementations(snapshot, module).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key))
        {
            text.AppendLine($"## {verb.Key}");
            text.AppendLine();
            foreach (VerbImplementationDescriptor implementation in verb.OrderBy(x => x.StableId, StringComparer.Ordinal))
                foreach (SentencePattern pattern in implementation.Patterns)
                {
                    string roles = string.Join(" ", pattern.Roles.OrderBy(x => x.Position).Select(FormatRole).Where(x => x.Length > 0));
                    text.AppendLine($"- `{verb.Key}{(roles.Length == 0 ? string.Empty : " " + roles)}` → `{Friendly(implementation.ResultType)}`");
                    if (SensitiveValueMetadata.IsSensitiveType(implementation.ResultType))
                        text.AppendLine("  - sensitive result: `true`");
                    foreach (RoleSlotDescriptor output in pattern.Roles.Where(x => x.Direction is RoleDirection.Output or RoleDirection.InputOutput && x.OutputProjection is not null))
                        text.AppendLine($"  - output `{output.Name}` projection: `{Projection(output.OutputProjection)}`");
                    if (implementation.Qualifiers.Count > 0)
                        text.AppendLine($"  - qualifiers: {string.Join(", ", implementation.Qualifiers.Select(x => $"`{x}`"))}");
                    if (implementation.Capabilities.Count > 0)
                        text.AppendLine($"  - capabilities: {string.Join(", ", implementation.Capabilities.Select(x => $"`{x}`"))}");
                    if (implementation.Traits.Count > 0)
                        text.AppendLine($"  - traits: {string.Join(", ", implementation.Traits.Select(x => $"`{x}`"))}");
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
        if (role.Direction == RoleDirection.Output)
            return string.Empty;
        string cardinality = role.Cardinality switch
        {
            RoleCardinality.ZeroOrOne => "?",
            RoleCardinality.ZeroOrMore => "*",
            RoleCardinality.OneOrMore => "+",
            _ => string.Empty
        };
        string nullability = role.TypeShape.IsNullable ? "?" : string.Empty;
        string sensitivity = SensitiveValueMetadata.IsSensitiveType(role.ValueType) ? " sensitive" : string.Empty;
        if (role.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase))
            return $"<{Friendly(role.ValueType)}{nullability}{sensitivity}>{cardinality}";
        string surface = role.AllSurfaceNames.Count > 1 ? string.Join("|", role.AllSurfaceNames) : role.Name;
        return $"{surface} <{Friendly(role.ValueType)}{nullability}{sensitivity}>{cardinality}";
    }

    private static string? Projection(OutputProjectionDescriptor? projection) => projection?.Kind switch
    {
        OutputProjectionKind.Member => $"member:{projection.Member}",
        OutputProjectionKind.Index => $"index:{projection.Index}",
        OutputProjectionKind.WholeResult => "whole",
        _ => null
    };

    private static string DescribeTypes(IReadOnlyList<Type> types) => types.Count == 0 ? "any" : string.Join(", ", types.Select(Friendly));
    private static string Friendly(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(Friendly))}>"
        : type.Name;

    private static string Slug(string text) => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
