using System.Linq.Expressions;
using System.Reflection;

namespace FluNET.Classic.Core;

public sealed class LanguageCompiler
{
    private readonly NullabilityInfoContext _nullability = new();

    public LanguageSnapshot Compile(IEnumerable<Assembly>? assemblies = null, IEnumerable<ILanguageModule>? modules = null, IEnumerable<QualifierDescriptor>? qualifiers = null) => Build(assemblies, modules, qualifiers).ThrowIfFailed();

    public LanguageBuildResult Build(IEnumerable<Assembly>? assemblies = null, IEnumerable<ILanguageModule>? modules = null, IEnumerable<QualifierDescriptor>? qualifiers = null)
    {
        ILanguageModule[] moduleArray = (modules ?? Array.Empty<ILanguageModule>()).ToArray();
        var diagnostics = new List<LanguageDiagnostic>();
        ValidateModules(moduleArray, diagnostics);
        Assembly[] sourceAssemblies = (assemblies ?? (moduleArray.Length > 0 ? moduleArray.SelectMany(x => x.Assemblies) : AppDomain.CurrentDomain.GetAssemblies())).Distinct().ToArray();
        var implementations = new List<VerbImplementationDescriptor>();
        foreach (Type type in sourceAssemblies.SelectMany(GetLoadableTypes).Where(x => !x.IsAbstract && !x.IsInterface && typeof(IVerb).IsAssignableFrom(x)))
        {
            VerbImplementationDescriptor? implementation = CompileVerb(type, diagnostics);
            if (implementation is not null) implementations.Add(implementation);
        }
        VerbDescriptor[] verbs = implementations.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(group => new VerbDescriptor($"verb:{Slug(group.Key)}", group.Key, group.SelectMany(x => x.Aliases).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), group.ToArray())).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        ValidateNames(verbs, diagnostics);
        ValidatePatterns(implementations, diagnostics);
        QualifierDescriptor[] qualifierArray = StandardQualifiers.All.Concat(moduleArray.SelectMany(x => x.Qualifiers)).Concat(qualifiers ?? Array.Empty<QualifierDescriptor>()).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
        ModuleDescriptor[] moduleDescriptors = moduleArray.Select(x => new ModuleDescriptor($"module:{Slug(x.Name)}", x.Name, x.Version, x.Dependencies.ToArray(), x.Assemblies.Select(a => a.GetName().Name ?? a.FullName ?? "unknown").ToArray())).ToArray();
        if (diagnostics.Any(x => x.Severity == LanguageDiagnosticSeverity.Error)) return new(null, diagnostics);
        return new(new LanguageSnapshot(verbs, qualifierArray, moduleDescriptors), diagnostics);
    }

    private VerbImplementationDescriptor? CompileVerb(Type type, ICollection<LanguageDiagnostic> diagnostics)
    {
        string? name = ResolveVerbName(type);
        if (name is null) { diagnostics.Add(new("FLU-LANG-001", $"Could not infer verb family for '{type.FullName}'. Add [Verb] or a semantic verb-family interface.", LanguageDiagnosticSeverity.Error, type)); return null; }
        string[] aliases = type.GetCustomAttributes<AliasAttribute>(true).Select(x => x.Alias.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] implementationQualifiers = type.GetCustomAttributes<QualifierAttribute>(true).Select(x => x.Name.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Type resultType = ResolveResultType(type) ?? typeof(object);
        ConstructorDescriptor[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Select(c => CompileConstructor(c, name)).ToArray();
        if (constructors.Length == 0) { diagnostics.Add(new("FLU-LANG-014", $"Verb '{type.FullName}' has no public constructor.", LanguageDiagnosticSeverity.Error, type)); return null; }
        SentencePattern[] patterns = constructors.Select((constructor, index) => new SentencePattern(
            $"pattern:{Slug(name)}:{Slug(type.Name)}:{index}",
            constructor,
            constructor.Parameters
                .Where(x => !x.IsService && x.RoleName is not null)
                .Select(x => new RoleSlotDescriptor(
                    $"role:{Slug(name)}:{index}:{x.Position}",
                    x.RoleName!,
                    x.ParameterType,
                    x.TypeShape,
                    x.Direction,
                    x.Cardinality,
                    x.Position,
                    x.Name,
                    !x.IsOptional,
                    x.SurfaceNames))
                .ToArray()))
            .Where(x => x.Roles.Count > 0)
            .ToArray();
        if (patterns.Length == 0) diagnostics.Add(new("FLU-LANG-015", $"Verb '{type.FullName}' has no constructor with language roles.", LanguageDiagnosticSeverity.Error, type));
        string[] capabilities = type.GetCustomAttributes<RequiresCapabilityAttribute>(true).Select(x => x.Capability).Concat(InferCapabilities(type, patterns)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ExecutionTrait[] traits = type.GetCustomAttributes<ExecutionTraitAttribute>(true).Select(x => x.Trait).Concat(InferTraits(type)).Distinct().ToArray();
        return new($"verb:{Slug(name)}:{Slug(type.FullName ?? type.Name)}", type, name, aliases, implementationQualifiers, constructors, patterns, resultType, capabilities, traits, CompileInvoker(resultType));
    }

    private ConstructorDescriptor CompileConstructor(ConstructorInfo constructor, string verbName)
    {
        ParameterDescriptor[] parameters = constructor.GetParameters().Select(p => CompileParameter(p, verbName)).ToArray();
        return new($"ctor:{Slug(constructor.DeclaringType?.FullName ?? "unknown")}:{constructor.MetadataToken}", constructor, parameters, CompileActivator(constructor));
    }

    private ParameterDescriptor CompileParameter(ParameterInfo parameter, string verbName)
    {
        RoleAttribute? role = parameter.GetCustomAttributes().OfType<RoleAttribute>().SingleOrDefault();
        bool service = parameter.IsDefined(typeof(FromServicesAttribute), false);
        string? roleName = service ? null : role?.Name ?? InferRoleName(parameter.Name);
        string[] surfaceNames = service
            ? Array.Empty<string>()
            : parameter.GetCustomAttributes<RoleAliasAttribute>(false).Select(x => x.Alias.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        RoleDirection direction = parameter.GetCustomAttribute<RoleDirectionAttribute>()?.Direction ?? role?.Direction ?? InferDirection(verbName, roleName);
        bool paramArray = parameter.IsDefined(typeof(ParamArrayAttribute), false);
        RoleCardinality cardinality = paramArray ? RoleCardinality.ZeroOrMore : parameter.IsOptional ? RoleCardinality.ZeroOrOne : RoleCardinality.One;
        NullabilityInfo nullability = _nullability.Create(parameter);
        return new(parameter, parameter.Name ?? $"arg{parameter.Position}", parameter.ParameterType, ClrTypeShape.From(parameter.ParameterType, nullability.ReadState), parameter.IsOptional, parameter.HasDefaultValue ? parameter.DefaultValue : null, paramArray, service, roleName, surfaceNames, direction, cardinality, parameter.Position);
    }

    private static Func<object?[], object> CompileActivator(ConstructorInfo constructor)
    {
        ParameterExpression args = Expression.Parameter(typeof(object?[]), "args");
        NewExpression body = Expression.New(constructor, constructor.GetParameters().Select((p, i) => Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(i)), p.ParameterType)));
        return Expression.Lambda<Func<object?[], object>>(Expression.Convert(body, typeof(object)), args).Compile();
    }

    private static Func<object, VerbExecutionContext, CancellationToken, ValueTask<object?>> CompileInvoker(Type resultType)
    {
        MethodInfo bridge = typeof(LanguageCompiler).GetMethod(nameof(InvokeGeneric), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(resultType);
        ParameterExpression instance = Expression.Parameter(typeof(object), "instance");
        ParameterExpression context = Expression.Parameter(typeof(VerbExecutionContext), "context");
        ParameterExpression token = Expression.Parameter(typeof(CancellationToken), "token");
        return Expression.Lambda<Func<object, VerbExecutionContext, CancellationToken, ValueTask<object?>>>(Expression.Call(bridge, instance, context, token), instance, context, token).Compile();
    }

    private static async ValueTask<object?> InvokeGeneric<TResult>(object instance, VerbExecutionContext context, CancellationToken cancellationToken) => await ((IVerb<TResult>)instance).ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

    private static IEnumerable<string> InferCapabilities(Type type, IEnumerable<SentencePattern> patterns)
    {
        RoleSlotDescriptor[] roles = patterns.SelectMany(x => x.Roles).ToArray();
        bool fileInput = roles.Any(r => r.Direction != RoleDirection.Output && (r.ValueType == typeof(FileInfo) || r.ValueType == typeof(DirectoryInfo) || r.TypeShape.ElementType == typeof(FileInfo)));
        bool uriInput = roles.Any(r => r.ValueType == typeof(Uri) || r.TypeShape.ElementType == typeof(Uri));
        if ((typeof(IGet).IsAssignableFrom(type) || typeof(ILoad).IsAssignableFrom(type) || typeof(IListVerb).IsAssignableFrom(type) || typeof(ICheck).IsAssignableFrom(type) || typeof(ICopy).IsAssignableFrom(type) || typeof(IMove).IsAssignableFrom(type)) && fileInput) yield return StandardCapabilities.FileSystemRead;
        if ((typeof(ISave).IsAssignableFrom(type) || typeof(IDelete).IsAssignableFrom(type) || typeof(ICreate).IsAssignableFrom(type) || typeof(ICopy).IsAssignableFrom(type) || typeof(IMove).IsAssignableFrom(type) || typeof(IDownload).IsAssignableFrom(type)) && fileInput) yield return StandardCapabilities.FileSystemWrite;
        if (uriInput || typeof(IPost).IsAssignableFrom(type) || typeof(IDownload).IsAssignableFrom(type) || typeof(ISend).IsAssignableFrom(type)) yield return StandardCapabilities.Network;
        if (typeof(ISend).IsAssignableFrom(type)) yield return StandardCapabilities.EmailSend;
        if (typeof(IRun).IsAssignableFrom(type)) yield return StandardCapabilities.ProcessExecute;
        if (typeof(IStop).IsAssignableFrom(type)) yield return StandardCapabilities.ProcessTerminate;
    }

    private static IEnumerable<ExecutionTrait> InferTraits(Type type)
    {
        if (typeof(ITransform).IsAssignableFrom(type) || typeof(IParse).IsAssignableFrom(type) || typeof(IFormat).IsAssignableFrom(type) || typeof(ICheck).IsAssignableFrom(type) || typeof(IFilter).IsAssignableFrom(type)) yield return ExecutionTrait.Pure;
        if (typeof(IGet).IsAssignableFrom(type) || typeof(ILoad).IsAssignableFrom(type) || typeof(IListVerb).IsAssignableFrom(type) || typeof(ICheck).IsAssignableFrom(type)) yield return ExecutionTrait.Idempotent;
        if (typeof(ISave).IsAssignableFrom(type) || typeof(IDelete).IsAssignableFrom(type) || typeof(ICreate).IsAssignableFrom(type) || typeof(ICopy).IsAssignableFrom(type) || typeof(IMove).IsAssignableFrom(type) || typeof(IRun).IsAssignableFrom(type) || typeof(IStop).IsAssignableFrom(type) || typeof(IPost).IsAssignableFrom(type) || typeof(ISend).IsAssignableFrom(type) || typeof(IDownload).IsAssignableFrom(type)) yield return ExecutionTrait.SideEffecting;
        if (typeof(IGet).IsAssignableFrom(type) || typeof(ILoad).IsAssignableFrom(type) || typeof(IDownload).IsAssignableFrom(type)) yield return ExecutionTrait.Retryable;
    }

    private static string? ResolveVerbName(Type type)
    {
        VerbAttribute? attribute = type.GetCustomAttribute<VerbAttribute>(true);
        if (attribute is not null) return attribute.Name.ToUpperInvariant();
        (Type Marker, string Name)[] families =
        {
            (typeof(IGet), "GET"),
            (typeof(ISave), "SAVE"),
            (typeof(ILoad), "LOAD"),
            (typeof(ICreate), "CREATE"),
            (typeof(IDelete), "DELETE"),
            (typeof(IListVerb), "LIST"),
            (typeof(ICopy), "COPY"),
            (typeof(IMove), "MOVE"),
            (typeof(IRun), "RUN"),
            (typeof(IStop), "STOP"),
            (typeof(ISend), "SEND"),
            (typeof(IDownload), "DOWNLOAD"),
            (typeof(IPost), "POST"),
            (typeof(ICheck), "CHECK"),
            (typeof(IParse), "PARSE"),
            (typeof(IFormat), "FORMAT"),
            (typeof(ITransform), "TRANSFORM"),
            (typeof(IWait), "WAIT"),
            (typeof(IFilter), "FILTER"),
            (typeof(ISay), "SAY")
        };
        foreach ((Type marker, string familyName) in families) if (marker.IsAssignableFrom(type)) return familyName;
        return null;
    }

    private static Type? ResolveResultType(Type type) => type.GetInterfaces().FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IVerb<>))?.GetGenericArguments()[0];

    private static string? InferRoleName(string? name) => name?.ToLowerInvariant() switch
    {
        "what" or "value" or "message" or "body" or "content" => "WHAT",
        "from" or "source" => "FROM",
        "to" or "target" or "destination" => "TO",
        "using" or "format" or "strategy" => "USING",
        "with" or "options" => "WITH",
        "as" or "representation" => "AS",
        "in" or "container" => "IN",
        "at" or "location" => "AT",
        "for" => "FOR",
        "until" or "deadline" => "UNTIL",
        "then" => "THEN",
        _ => null
    };

    private static RoleDirection InferDirection(string verb, string? role) => role?.Equals("WHAT", StringComparison.OrdinalIgnoreCase) == true && (verb is "GET" or "LOAD" or "DOWNLOAD" or "LIST") ? RoleDirection.Output : RoleDirection.Input;

    private static void ValidateNames(IEnumerable<VerbDescriptor> verbs, ICollection<LanguageDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs) { Add(verb.Name, verb.Name); foreach (string alias in verb.Aliases) Add(alias, verb.Name); }
        void Add(string name, string owner) { if (owners.TryGetValue(name, out string? existing) && !existing.Equals(owner, StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new("FLU-LANG-002", $"Language name '{name}' is claimed by '{existing}' and '{owner}'.", LanguageDiagnosticSeverity.Error)); else owners[name] = owner; }
    }

    private static void ValidatePatterns(IEnumerable<VerbImplementationDescriptor> implementations, ICollection<LanguageDiagnostic> diagnostics)
    {
        foreach (VerbImplementationDescriptor implementation in implementations)
        foreach (SentencePattern pattern in implementation.Patterns)
        {
            RoleSlotDescriptor[] variadic = pattern.Roles.Where(x => x.Cardinality is RoleCardinality.ZeroOrMore or RoleCardinality.OneOrMore).ToArray();
            if (variadic.Length > 1) diagnostics.Add(new("FLU-LANG-016", $"Pattern '{pattern.StableId}' has more than one variadic role.", LanguageDiagnosticSeverity.Error, implementation.ImplementationType));
            if (variadic.Length == 1 && variadic[0].Position != pattern.Roles.Max(x => x.Position)) diagnostics.Add(new("FLU-LANG-017", $"Variadic role '{variadic[0].Name}' must be the last language role.", LanguageDiagnosticSeverity.Error, implementation.ImplementationType));

            var surfaceOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (RoleSlotDescriptor role in pattern.Roles)
            foreach (string surface in role.AllSurfaceNames)
            {
                if (surface.Equals("INTO", StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new("FLU-LANG-018", $"'{surface}' is reserved for result binding and cannot be a role name or alias.", LanguageDiagnosticSeverity.Error, implementation.ImplementationType));
                if (surfaceOwners.TryGetValue(surface, out string? owner) && !owner.Equals(role.Name, StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(new("FLU-LANG-019", $"Surface role '{surface}' maps to both '{owner}' and '{role.Name}' in pattern '{pattern.StableId}'.", LanguageDiagnosticSeverity.Error, implementation.ImplementationType));
                else
                    surfaceOwners[surface] = role.Name;
            }
        }
    }

    private static void ValidateModules(IReadOnlyList<ILanguageModule> modules, ICollection<LanguageDiagnostic> diagnostics)
    {
        var names = new HashSet<string>(modules.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
        if (names.Count != modules.Count) diagnostics.Add(new("FLU-LANG-029", "Duplicate module names are not allowed.", LanguageDiagnosticSeverity.Error));
        foreach (ILanguageModule module in modules)
        foreach (string dependency in module.Dependencies)
            if (!names.Contains(dependency)) diagnostics.Add(new("FLU-LANG-030", $"Module '{module.Name}' requires missing module '{dependency}'.", LanguageDiagnosticSeverity.Error));
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }

    private static string Slug(string text) => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
