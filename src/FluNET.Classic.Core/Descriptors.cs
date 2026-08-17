using System.Reflection;

namespace FluNET.Classic.Core;

public enum LanguageDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record LanguageDiagnostic(
    string Code,
    string Message,
    LanguageDiagnosticSeverity Severity,
    Type? RelatedType = null);

public sealed record ClrTypeShape(
    Type Type,
    Type EffectiveType,
    Type? ElementType,
    bool IsCollection,
    bool IsArray,
    bool IsNullable,
    bool IsEnum)
{
    public bool IsAsyncEnumerable => IsAsyncEnumerableType(EffectiveType);

    public static ClrTypeShape From(Type type, NullabilityState nullability = NullabilityState.Unknown)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        Type? element = GetElementType(effective);
        bool nullable = Nullable.GetUnderlyingType(type) is not null || (!type.IsValueType && nullability == NullabilityState.Nullable);
        return new(type, effective, element, element is not null, effective.IsArray, nullable, effective.IsEnum);
    }

    public static Type? GetElementType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return null;
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType)
        {
            Type definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IEnumerable<>) || definition == typeof(ICollection<>) || definition == typeof(IList<>) ||
                definition == typeof(IReadOnlyCollection<>) || definition == typeof(IReadOnlyList<>) || definition == typeof(List<>) ||
                definition == typeof(IAsyncEnumerable<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        Type? asyncEnumerable = type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        if (asyncEnumerable is not null) return asyncEnumerable.GetGenericArguments()[0];

        return type.GetInterfaces()
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    public static bool IsAsyncEnumerableType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>)) return true;
        return type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
    }
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
    IReadOnlyList<string> SurfaceNames,
    RoleDirection Direction,
    RoleCardinality Cardinality,
    int Position);

public sealed record ConstructorDescriptor(
    string StableId,
    ConstructorInfo Constructor,
    IReadOnlyList<ParameterDescriptor> Parameters,
    Func<object?[], object> Activator);

public sealed record RoleSlotDescriptor(
    string StableId,
    string Name,
    Type ValueType,
    ClrTypeShape TypeShape,
    RoleDirection Direction,
    RoleCardinality Cardinality,
    int Position,
    string ParameterName,
    bool Required,
    IReadOnlyList<string> SurfaceNames)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(SurfaceNames)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed record SentencePattern(
    string StableId,
    ConstructorDescriptor Constructor,
    IReadOnlyList<RoleSlotDescriptor> Roles);

public sealed record VerbImplementationDescriptor(
    string StableId,
    Type ImplementationType,
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Qualifiers,
    IReadOnlyList<ConstructorDescriptor> Constructors,
    IReadOnlyList<SentencePattern> Patterns,
    Type ResultType,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ExecutionTrait> Traits,
    Func<object, VerbExecutionContext, CancellationToken, ValueTask<object?>> Invoker);

public sealed record VerbDescriptor(
    string StableId,
    string Name,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<VerbImplementationDescriptor> Implementations);

public sealed record QualifierDescriptor(
    string StableId,
    string Name,
    Type? TargetType = null,
    IReadOnlyList<string>? Aliases = null)
{
    public IReadOnlyList<string> AllAliases => Aliases ?? Array.Empty<string>();
}

public sealed record ModuleDescriptor(
    string StableId,
    string Name,
    Version Version,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Assemblies);

public sealed class LanguageBuildResult
{
    public LanguageBuildResult(LanguageSnapshot? snapshot, IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        Snapshot = snapshot;
        Diagnostics = diagnostics;
    }

    public LanguageSnapshot? Snapshot { get; }
    public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; }
    public bool Success => Snapshot is not null && Diagnostics.All(x => x.Severity != LanguageDiagnosticSeverity.Error);

    public LanguageSnapshot ThrowIfFailed()
    {
        if (Success) return Snapshot!;
        throw new LanguageCompilationException(Diagnostics);
    }
}

public sealed class LanguageCompilationException : Exception
{
    public LanguageCompilationException(IEnumerable<LanguageDiagnostic> diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(x => $"{x.Code}: {x.Message}")))
    {
        Diagnostics = diagnostics.ToArray();
    }

    public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; }
}
