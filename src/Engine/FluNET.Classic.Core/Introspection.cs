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
                            surfaceNames = r.AllSurfaceNames,
                            parameter = r.ParameterName,
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
            }),
            qualifiers = Snapshot.Qualifiers.Select(q => new
            {
                id = q.StableId,
                name = q.Name,
                type = q.TargetType?.FullName,
                sensitive = q.TargetType is not null && SensitiveValueMetadata.IsSensitiveType(q.TargetType),
                aliases = q.AllAliases
            }),
            predicates = Snapshot.Predicates.Select(p => new
            {
                id = p.StableId,
                name = p.Name,
                surfaceNames = p.AllSurfaceNames,
                syntax = p.Syntax.ToString(),
                precedence = p.Precedence,
                operandTypes = p.SupportedOperandTypes.Select(x => x.FullName),
                referenceOperandType = p.ReferenceOperandType?.FullName,
                capabilities = p.RequiredCapabilities,
                capabilityRequirements = p.CapabilityRequirements.Select(c => new { capability = c.Capability, operandType = c.OperandType?.FullName })
            }),
            operators = Snapshot.Operators.Select(o => new
            {
                id = o.StableId,
                name = o.Name,
                surfaceNames = o.AllSurfaceNames,
                precedence = o.Precedence,
                arity = o.Arity.ToString(),
                associativity = o.Associativity.ToString(),
                semantic = o.Semantic.ToString(),
                compatibility = o.Compatibility.ToString(),
                evaluation = o.Evaluation.ToString(),
                resultType = o.EffectiveResultType.FullName
            }),
            intrinsics = Snapshot.Intrinsics.Select(i => new
            {
                id = i.StableId,
                name = i.Name,
                surfaceNames = i.AllSurfaceNames,
                syntax = i.Syntax.ToString(),
                semantic = i.Semantic.ToString(),
                execution = i.Execution.ToString(),
                strategyType = i.StrategyType?.FullName,
                strategyRole = i.StrategyType is null ? null : i.StrategyRole
            }),
            structuralSyntax = Snapshot.StructuralSyntax,
            literalWords = Snapshot.LiteralWords.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            reservedWords = Snapshot.ReservedWords.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            modules = Snapshot.Modules
        };
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = indented });
    }

    public string DescribeVerb(string name)
    {
        VerbDescriptor verb = Snapshot.GetVerb(name);
        return string.Join(Environment.NewLine, verb.Implementations.SelectMany(i => i.Patterns.Select(p =>
            $"{verb.Name} {string.Join(" ", p.Roles.Select(r => $"{r.Name}:{Friendly(r.ValueType)}[{r.Cardinality}]{ProjectionSuffix(r.OutputProjection)}"))} -> {Friendly(i.ResultType)} | implementation={i.ImplementationType.FullName} | id={i.StableId} | qualifiers=[{string.Join(',', i.Qualifiers)}] | capabilities=[{string.Join(',', i.Capabilities)}] | traits=[{string.Join(',', i.Traits)}]")));
    }

    private static string? Projection(OutputProjectionDescriptor? projection) => projection?.Kind switch
    {
        OutputProjectionKind.Member => $"member:{projection.Member}",
        OutputProjectionKind.Index => $"index:{projection.Index}",
        OutputProjectionKind.WholeResult => "whole",
        _ => null
    };

    private static string ProjectionSuffix(OutputProjectionDescriptor? projection) => Projection(projection) is { } value ? $"<{value}>" : string.Empty;

    private static string Friendly(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(Friendly))}>"
        : type.Name;
}
