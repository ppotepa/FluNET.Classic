using System.Reflection;

namespace FluNET.Language;

public sealed record ClrTypeShape(
    Type Type,
    Type? ElementType,
    bool IsCollection,
    bool IsArray,
    bool IsNullable,
    bool IsEnum)
{
    public static ClrTypeShape From(Type type, NullabilityState nullability = NullabilityState.Unknown)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        Type? elementType = TryGetElementType(effectiveType);
        bool nullable = Nullable.GetUnderlyingType(type) is not null ||
                        (!type.IsValueType && nullability == NullabilityState.Nullable);

        return new ClrTypeShape(
            type,
            elementType,
            elementType is not null,
            effectiveType.IsArray,
            nullable,
            effectiveType.IsEnum);
    }

    private static Type? TryGetElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && IsSupportedCollection(type.GetGenericTypeDefinition()))
        {
            return type.GetGenericArguments()[0];
        }

        Type? enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0];
    }

    private static bool IsSupportedCollection(Type definition) =>
        definition == typeof(List<>) ||
        definition == typeof(IList<>) ||
        definition == typeof(ICollection<>) ||
        definition == typeof(IEnumerable<>) ||
        definition == typeof(IReadOnlyList<>) ||
        definition == typeof(IReadOnlyCollection<>);
}

public sealed record ParameterDescriptor(
    ParameterInfo Parameter,
    string Name,
    Type ParameterType,
    ClrTypeShape TypeShape,
    bool IsOptional,
    object? DefaultValue,
    bool IsParamArray,
    bool IsService,
    string? RoleName,
    RoleDirection RoleDirection,
    RoleCardinality Cardinality,
    int Position);

public sealed record ConstructorDescriptor(
    ConstructorInfo Constructor,
    IReadOnlyList<ParameterDescriptor> Parameters);

public sealed record RoleSlotDescriptor(
    string Name,
    Type ValueType,
    ClrTypeShape TypeShape,
    RoleDirection Direction,
    RoleCardinality Cardinality,
    int Position,
    string ParameterName,
    bool Required);

public sealed record SentencePattern(
    IReadOnlyList<RoleSlotDescriptor> Roles);

public sealed record VerbImplementationDescriptor(
    Type ImplementationType,
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<ConstructorDescriptor> Constructors,
    IReadOnlyList<SentencePattern> Patterns,
    Type? ResultType,
    IReadOnlyList<string> Capabilities);

public sealed record VerbDescriptor(
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<VerbImplementationDescriptor> Implementations);
