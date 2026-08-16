using FluNET.Syntax.Core;
using System.Reflection;

namespace FluNET.Language;

public sealed class LanguageCompiler
{
    private readonly NullabilityInfoContext _nullability = new();

    public LanguageSnapshot Compile(IEnumerable<Assembly>? assemblies = null)
    {
        LanguageBuildResult result = Build(assemblies);
        result.ThrowIfFailed();
        return result.Snapshot!;
    }

    public LanguageBuildResult Build(IEnumerable<Assembly>? assemblies = null)
    {
        Assembly[] sourceAssemblies = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies())
            .Distinct()
            .ToArray();

        var diagnostics = new List<LanguageDiagnostic>();
        var implementations = new List<VerbImplementationDescriptor>();

        foreach (Type type in sourceAssemblies.SelectMany(GetLoadableTypes)
                     .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IVerb).IsAssignableFrom(t)))
        {
            VerbImplementationDescriptor? descriptor = CompileVerb(type);
            if (descriptor is null)
            {
                diagnostics.Add(new LanguageDiagnostic(
                    "FLU-LANG-001",
                    $"Could not infer a verb family for '{type.FullName}'. Use [Verb] or an IVerbFamily marker.",
                    LanguageDiagnosticSeverity.Error,
                    type));
                continue;
            }

            if (descriptor.Patterns.Count == 0)
            {
                diagnostics.Add(new LanguageDiagnostic(
                    "FLU-LANG-014",
                    $"Verb implementation '{type.FullName}' has no bindable constructor pattern.",
                    LanguageDiagnosticSeverity.Error,
                    type));
            }

            implementations.Add(descriptor);
        }

        VerbDescriptor[] verbs = implementations
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new VerbDescriptor(
                group.Key,
                group.SelectMany(x => x.Aliases)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                group.ToArray()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ValidateNames(verbs, diagnostics);
        ValidatePatterns(implementations, diagnostics);

        if (diagnostics.Any(d => d.Severity == LanguageDiagnosticSeverity.Error))
        {
            return new LanguageBuildResult(null, diagnostics);
        }

        return new LanguageBuildResult(new LanguageSnapshot(verbs), diagnostics);
    }

    public VerbImplementationDescriptor? CompileVerb(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsAbstract || type.IsInterface || !typeof(IVerb).IsAssignableFrom(type))
        {
            return null;
        }

        string? verbName = ResolveVerbName(type);
        if (string.IsNullOrWhiteSpace(verbName))
        {
            return null;
        }

        string[] aliases = type.GetCustomAttributes<AliasAttribute>(inherit: true)
            .Select(x => x.Alias)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ConstructorDescriptor[] constructors = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Select(c => CompileConstructor(c, verbName))
            .ToArray();

        SentencePattern[] patterns = constructors
            .Select(c => new SentencePattern(
                c,
                c.Parameters
                    .Where(p => !p.IsService && p.RoleName is not null)
                    .Select(p => new RoleSlotDescriptor(
                        p.RoleName!,
                        p.ParameterType,
                        p.TypeShape,
                        p.RoleDirection,
                        p.Cardinality,
                        p.Position,
                        p.Name,
                        !p.IsOptional))
                    .ToArray()))
            .Where(p => p.Roles.Count > 0)
            .ToArray();

        Type? resultType = ResolveResultType(type);
        string[] capabilities = type.GetCustomAttributes<RequiresCapabilityAttribute>(inherit: true)
            .Select(x => x.Capability)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new VerbImplementationDescriptor(
            type,
            verbName,
            aliases,
            constructors,
            patterns,
            resultType,
            capabilities);
    }

    private ConstructorDescriptor CompileConstructor(ConstructorInfo constructor, string verbName)
    {
        ParameterDescriptor[] parameters = constructor.GetParameters()
            .Select(parameter => CompileParameter(parameter, verbName))
            .ToArray();

        return new ConstructorDescriptor(constructor, parameters);
    }

    private ParameterDescriptor CompileParameter(ParameterInfo parameter, string verbName)
    {
        RoleAttribute? roleAttribute = parameter.GetCustomAttributes()
            .OfType<RoleAttribute>()
            .SingleOrDefault();

        bool fromServices = parameter.IsDefined(typeof(FromServicesAttribute), inherit: false);
        string? roleName = fromServices
            ? null
            : roleAttribute?.Name ?? InferRoleName(parameter.Name);

        RoleDirection direction = parameter.GetCustomAttribute<RoleDirectionAttribute>()?.Direction
            ?? roleAttribute?.Direction
            ?? InferRoleDirection(verbName, roleName);

        bool isParamArray = parameter.IsDefined(typeof(ParamArrayAttribute), inherit: false);
        RoleCardinality cardinality = isParamArray
            ? RoleCardinality.ZeroOrMore
            : parameter.IsOptional
                ? RoleCardinality.ZeroOrOne
                : RoleCardinality.One;

        NullabilityInfo nullability = _nullability.Create(parameter);
        ClrTypeShape shape = ClrTypeShape.From(parameter.ParameterType, nullability.ReadState);

        return new ParameterDescriptor(
            parameter,
            parameter.Name ?? $"arg{parameter.Position}",
            parameter.ParameterType,
            shape,
            parameter.IsOptional,
            parameter.HasDefaultValue ? parameter.DefaultValue : null,
            isParamArray,
            fromServices,
            roleName,
            direction,
            cardinality,
            parameter.Position);
    }

    private static void ValidateNames(
        IReadOnlyList<VerbDescriptor> verbs,
        ICollection<LanguageDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (VerbDescriptor verb in verbs)
        {
            RegisterName(verb.Name, verb.Name);
            foreach (string alias in verb.Aliases)
            {
                RegisterName(alias, verb.Name);
            }
        }

        void RegisterName(string name, string owner)
        {
            if (owners.TryGetValue(name, out string? existing) &&
                !string.Equals(existing, owner, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new LanguageDiagnostic(
                    "FLU-LANG-002",
                    $"Language name '{name}' is claimed by both '{existing}' and '{owner}'.",
                    LanguageDiagnosticSeverity.Error));
                return;
            }

            owners[name] = owner;
        }
    }

    private static void ValidatePatterns(
        IEnumerable<VerbImplementationDescriptor> implementations,
        ICollection<LanguageDiagnostic> diagnostics)
    {
        foreach (VerbImplementationDescriptor implementation in implementations)
        {
            foreach (SentencePattern pattern in implementation.Patterns)
            {
                RoleSlotDescriptor? variadic = pattern.Roles
                    .FirstOrDefault(r => r.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore);

                if (variadic is not null && variadic.Position != pattern.Roles.Max(r => r.Position))
                {
                    diagnostics.Add(new LanguageDiagnostic(
                        "FLU-LANG-015",
                        $"Variadic role '{variadic.Name}' in '{implementation.ImplementationType.FullName}' must be the last language role in its constructor.",
                        LanguageDiagnosticSeverity.Error,
                        implementation.ImplementationType));
                }
            }
        }
    }

    private static string? ResolveVerbName(Type type)
    {
        VerbAttribute? explicitName = type.GetCustomAttribute<VerbAttribute>(inherit: true);
        if (explicitName is not null)
        {
            return explicitName.Name.ToUpperInvariant();
        }

        Type? familyInterface = type.GetInterfaces()
            .FirstOrDefault(i => i != typeof(IVerbFamily) && typeof(IVerbFamily).IsAssignableFrom(i));
        if (familyInterface is not null)
        {
            string familyName = familyInterface.Name;
            if (familyName.StartsWith('I') && familyName.Length > 1)
            {
                familyName = familyName[1..];
            }

            return familyName.ToUpperInvariant();
        }

        for (Type? current = type.BaseType; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (!current.IsAbstract)
            {
                continue;
            }

            string name = current.IsGenericType
                ? current.GetGenericTypeDefinition().Name
                : current.Name;
            int tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name[..tick];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.ToUpperInvariant();
            }
        }

        return null;
    }

    private static Type? ResolveResultType(Type type)
    {
        Type? generalized = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerb<>));
        if (generalized is not null)
        {
            return generalized.GetGenericArguments()[0];
        }

        Type? twoRoleContract = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IVerb<,>));

        return twoRoleContract?.GetGenericArguments()[0];
    }

    private static string? InferRoleName(string? parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        return parameterName.ToUpperInvariant() switch
        {
            "WHAT" => "WHAT",
            "FROM" => "FROM",
            "TO" => "TO",
            "USING" => "USING",
            "WITH" => "WITH",
            "THEN" => "THEN",
            _ => null
        };
    }

    private static RoleDirection InferRoleDirection(string verbName, string? roleName)
    {
        if (!string.Equals(roleName, "WHAT", StringComparison.OrdinalIgnoreCase))
        {
            return RoleDirection.Input;
        }

        return verbName.ToUpperInvariant() switch
        {
            "GET" or "LOAD" or "DOWNLOAD" => RoleDirection.Output,
            _ => RoleDirection.Input
        };
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }
}
