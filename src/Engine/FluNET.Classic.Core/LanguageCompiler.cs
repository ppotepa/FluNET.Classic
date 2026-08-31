using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace FluNET.Classic.Core;

public sealed class LanguageCompiler
{
    private static readonly HashSet<string> ForbiddenRoleSurfaces = new(new[] { "INTO", "THEN", "ELSE", "IF", "WHERE", "EACH", "DO", "END" }, StringComparer.OrdinalIgnoreCase);
    private readonly NullabilityInfoContext _nullability = new();

    public LanguageSnapshot Compile(IEnumerable<Assembly>? assemblies = null, IEnumerable<ILanguageModule>? modules = null, IEnumerable<QualifierDescriptor>? qualifiers = null, IEnumerable<PredicateDescriptor>? predicates = null, IEnumerable<OperatorDescriptor>? operators = null, IEnumerable<IntrinsicDescriptor>? intrinsics = null) => Build(assemblies, modules, qualifiers, predicates, operators, intrinsics).ThrowIfFailed();

    public LanguageBuildResult Build(IEnumerable<Assembly>? assemblies = null, IEnumerable<ILanguageModule>? modules = null, IEnumerable<QualifierDescriptor>? qualifiers = null, IEnumerable<PredicateDescriptor>? predicates = null, IEnumerable<OperatorDescriptor>? operators = null, IEnumerable<IntrinsicDescriptor>? intrinsics = null)
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

        VerbDescriptor[] verbs = implementations
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new VerbDescriptor($"verb:{Slug(group.Key)}", group.Key, group.SelectMany(x => x.Aliases).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), group.ToArray()))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ValidateNames(verbs, diagnostics);
        ValidatePatterns(implementations, diagnostics);
        foreach (LanguageDiagnostic diagnostic in LanguageSurfaceValidation.Validate(implementations)) diagnostics.Add(diagnostic);

        QualifierDescriptor[] qualifierArray = StandardQualifiers.All.Concat(moduleArray.SelectMany(x => x.Qualifiers)).Concat(qualifiers ?? Array.Empty<QualifierDescriptor>()).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
        PredicateDescriptor[] predicateArray = StandardLanguageSurface.Predicates.Concat(moduleArray.SelectMany(x => x.Predicates)).Concat(predicates ?? Array.Empty<PredicateDescriptor>()).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
        OperatorDescriptor[] operatorArray = StandardLanguageSurface.Operators.Concat(moduleArray.SelectMany(x => x.Operators)).Concat(operators ?? Array.Empty<OperatorDescriptor>()).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
        IntrinsicDescriptor[] intrinsicArray = moduleArray.SelectMany(x => x.Intrinsics).Concat(intrinsics ?? Array.Empty<IntrinsicDescriptor>()).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
        ModuleDescriptor[] moduleDescriptors = moduleArray.Select(module => new ModuleDescriptor(StableId(module.GetType(), $"module:{Slug(module.Name)}", diagnostics), module.Name, module.Version, module.Dependencies.ToArray(), module.Assemblies.Select(a => a.GetName().Name ?? a.FullName ?? "unknown").ToArray())).ToArray();

        ValidateSemanticSurface(predicateArray, operatorArray, intrinsicArray, diagnostics);
        ValidateStableIds(moduleDescriptors, qualifierArray, verbs, predicateArray, operatorArray, intrinsicArray, diagnostics);
        if (diagnostics.Any(x => x.Severity == LanguageDiagnosticSeverity.Error)) return new(null, diagnostics);
        return new(new LanguageSnapshot(verbs, qualifierArray, moduleDescriptors, predicateArray, operatorArray, intrinsicArray), diagnostics);
    }

    private VerbImplementationDescriptor? CompileVerb(Type type, ICollection<LanguageDiagnostic> diagnostics)
    {
        string? name = ResolveVerbName(type);
        if (name is null)
        {
            diagnostics.Add(new("FLU-LANG-001", $"Could not infer verb family for '{type.FullName}'. Add [Verb] or a semantic verb-family interface.", LanguageDiagnosticSeverity.Error, type));
            return null;
        }

        string[] aliases = type.GetCustomAttributes<AliasAttribute>(true).Select(x => x.Alias.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string[] implementationQualifiers = type.GetCustomAttributes<QualifierAttribute>(true).Select(x => x.Name.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Type resultType = ResolveResultType(type) ?? typeof(object);
        string implementationId = StableId(type, $"verb:{Slug(name)}:{Slug(type.FullName ?? type.Name)}", diagnostics);
        ConstructorDescriptor[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Select(constructor => CompileConstructor(constructor, name, implementationId, resultType, diagnostics))
            .ToArray();
        if (constructors.Length == 0)
        {
            diagnostics.Add(new("FLU-LANG-014", $"Verb '{type.FullName}' has no public constructor.", LanguageDiagnosticSeverity.Error, type));
            return null;
        }

        SentencePattern[] patterns = constructors.Select(constructor => CompilePattern(constructor, implementationId, diagnostics)).Where(x => x.Roles.Count > 0).ToArray();
        if (patterns.Length == 0) diagnostics.Add(new("FLU-LANG-015", $"Verb '{type.FullName}' has no constructor with language roles.", LanguageDiagnosticSeverity.Error, type));
        string[] capabilities = type.GetCustomAttributes<RequiresCapabilityAttribute>(true).Select(x => x.Capability).Concat(InferCapabilities(type, patterns)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        ExecutionTrait[] traits = type.GetCustomAttributes<ExecutionTraitAttribute>(true).Select(x => x.Trait).Concat(InferTraits(type)).Distinct().ToArray();
        return new(implementationId, type, name, aliases, implementationQualifiers, constructors, patterns, resultType, capabilities, traits, CompileInvoker(resultType));
    }

    private ConstructorDescriptor CompileConstructor(ConstructorInfo constructor, string verbName, string implementationId, Type resultType, ICollection<LanguageDiagnostic> diagnostics)
    {
        ParameterDescriptor[] parameters = constructor.GetParameters().Select(parameter => CompileParameter(parameter, verbName, diagnostics)).ToArray();
        ValidateOutputProjections(constructor, parameters, resultType, diagnostics);
        string signature = string.Join("|", parameters.Select(ParameterSemanticSignature));
        string stableId = StableId(constructor, $"ctor:{implementationId}:{ShortHash(signature)}", diagnostics);
        return new(stableId, constructor, parameters, CompileActivator(constructor));
    }

    private SentencePattern CompilePattern(ConstructorDescriptor constructor, string implementationId, ICollection<LanguageDiagnostic> diagnostics)
    {
        ParameterDescriptor[] languageParameters = constructor.Parameters.Where(x => !x.IsService && x.RoleName is not null).ToArray();
        string roleSignature = string.Join("|", languageParameters.Select(RoleSemanticSignature));
        StableIdAttribute? explicitConstructorId = constructor.Constructor.GetCustomAttribute<StableIdAttribute>();
        string patternId = explicitConstructorId is not null ? PatternIdFromConstructorId(explicitConstructorId.Id) : $"pattern:{implementationId}:{ShortHash(roleSignature)}";
        ValidateStableId(patternId, constructor.Constructor.DeclaringType, diagnostics);
        RoleSlotDescriptor[] roles = languageParameters.Select(parameter => new RoleSlotDescriptor(
            parameter.Parameter.GetCustomAttribute<StableIdAttribute>() is { } explicitRole
                ? ValidateAndReturn(explicitRole.Id, parameter.Parameter.Member.DeclaringType, diagnostics)
                : $"role:{patternId}:{Slug(parameter.RoleName!)}",
            parameter.RoleName!,
            parameter.ParameterType,
            parameter.TypeShape,
            parameter.Direction,
            parameter.Cardinality,
            parameter.Position,
            parameter.Name,
            !parameter.IsOptional,
            parameter.SurfaceNames,
            parameter.OutputProjection)).ToArray();
        return new(patternId, constructor, roles);
    }

    private ParameterDescriptor CompileParameter(ParameterInfo parameter, string verbName, ICollection<LanguageDiagnostic> diagnostics)
    {
        RoleAttribute? role = parameter.GetCustomAttributes().OfType<RoleAttribute>().SingleOrDefault();
        bool service = parameter.IsDefined(typeof(FromServicesAttribute), false);
        string? roleName = service ? null : role?.Name ?? InferRoleName(parameter.Name);
        string[] surfaceNames = service ? Array.Empty<string>() : parameter.GetCustomAttributes<RoleAliasAttribute>(false).Select(x => x.Alias.ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        RoleDirection direction = parameter.GetCustomAttribute<RoleDirectionAttribute>()?.Direction ?? role?.Direction ?? InferDirection(verbName, roleName);
        bool paramArray = parameter.IsDefined(typeof(ParamArrayAttribute), false);
        RoleCardinality cardinality = paramArray ? RoleCardinality.ZeroOrMore : parameter.IsOptional ? RoleCardinality.ZeroOrOne : RoleCardinality.One;
        NullabilityInfo nullability = _nullability.Create(parameter);

        OutputMemberAttribute? member = parameter.GetCustomAttribute<OutputMemberAttribute>(false);
        OutputIndexAttribute? index = parameter.GetCustomAttribute<OutputIndexAttribute>(false);
        if (member is not null && index is not null)
            diagnostics.Add(new("FLU-LANG-023", $"Parameter '{parameter.Name}' cannot use both [OutputMember] and [OutputIndex].", LanguageDiagnosticSeverity.Error, parameter.Member.DeclaringType));
        OutputProjectionDescriptor? projection = member is not null
            ? OutputProjectionDescriptor.FromMember(member.Member)
            : index is not null
                ? OutputProjectionDescriptor.FromIndex(index.Index)
                : null;

        return new(parameter, parameter.Name ?? $"arg{parameter.Position}", parameter.ParameterType, ClrTypeShape.From(parameter.ParameterType, nullability.ReadState), parameter.IsOptional, parameter.HasDefaultValue ? parameter.DefaultValue : null, paramArray, service, roleName, surfaceNames, direction, cardinality, parameter.Position, projection);
    }

    private static void ValidateOutputProjections(ConstructorInfo constructor, IReadOnlyList<ParameterDescriptor> parameters, Type resultType, ICollection<LanguageDiagnostic> diagnostics)
    {
        ParameterDescriptor[] languageParameters = parameters.Where(x => !x.IsService && x.RoleName is not null).ToArray();
        ParameterDescriptor[] outputs = languageParameters.Where(x => x.Direction is RoleDirection.Output or RoleDirection.InputOutput).ToArray();

        foreach (ParameterDescriptor parameter in languageParameters.Where(x => x.OutputProjection is not null && x.Direction is not RoleDirection.Output and not RoleDirection.InputOutput))
            diagnostics.Add(new("FLU-LANG-024", $"Output projection on '{parameter.Name}' is only valid for Output or InputOutput roles.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));

        if (outputs.Length > 1 && outputs.Any(x => x.OutputProjection is null))
            diagnostics.Add(new("FLU-LANG-025", $"Constructor '{constructor.DeclaringType?.FullName}' has multiple output roles; every output requires [OutputMember] or [OutputIndex].", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));

        foreach (ParameterDescriptor output in outputs)
        {
            OutputProjectionDescriptor projection = output.OutputProjection ?? OutputProjectionDescriptor.WholeResult;
            switch (projection.Kind)
            {
                case OutputProjectionKind.WholeResult:
                    if (outputs.Length == 1 && !OutputTypeCompatible(output.ParameterType, resultType))
                        diagnostics.Add(new("FLU-LANG-026", $"Output role '{output.Name}' expects {output.ParameterType.Name}, but whole result type is {resultType.Name}. Add an explicit output projection.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));
                    break;
                case OutputProjectionKind.Member:
                    if (string.IsNullOrWhiteSpace(projection.Member))
                    {
                        diagnostics.Add(new("FLU-LANG-027", $"Output member projection on '{output.Name}' cannot be empty.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));
                        break;
                    }
                    MemberInfo? member = FindPublicResultMember(resultType, projection.Member!);
                    if (member is null)
                    {
                        diagnostics.Add(new("FLU-LANG-028", $"Result type '{resultType.Name}' has no public instance member '{projection.Member}' for output role '{output.Name}'.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));
                        break;
                    }
                    Type memberType = member switch { PropertyInfo property => property.PropertyType, FieldInfo field => field.FieldType, _ => typeof(object) };
                    if (!OutputTypeCompatible(output.ParameterType, memberType))
                        diagnostics.Add(new("FLU-LANG-029", $"Output role '{output.Name}' expects {output.ParameterType.Name}, but projected member '{projection.Member}' has type {memberType.Name}.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));
                    break;
                case OutputProjectionKind.Index:
                    if (projection.Index is null or < 0)
                        diagnostics.Add(new("FLU-LANG-030", $"Output index projection on '{output.Name}' must be non-negative.", LanguageDiagnosticSeverity.Error, constructor.DeclaringType));
                    break;
            }
        }
    }

    private static MemberInfo? FindPublicResultMember(Type resultType, string member) =>
        (MemberInfo?)resultType.GetProperty(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? resultType.GetField(member, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

    private static bool OutputTypeCompatible(Type expected, Type actual)
    {
        Type target = Nullable.GetUnderlyingType(expected) ?? expected;
        Type source = Nullable.GetUnderlyingType(actual) ?? actual;
        return target == source || target.IsAssignableFrom(source);
    }

    private static Func<object?[], object> CompileActivator(ConstructorInfo constructor)
    {
        ParameterExpression args = Expression.Parameter(typeof(object?[]), "args");
        NewExpression body = Expression.New(constructor, constructor.GetParameters().Select((parameter, index) => Expression.Convert(Expression.ArrayIndex(args, Expression.Constant(index)), parameter.ParameterType)));
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
        bool fileInput = roles.Any(role => role.Direction != RoleDirection.Output && (role.ValueType == typeof(FileInfo) || role.ValueType == typeof(DirectoryInfo) || role.TypeShape.ElementType == typeof(FileInfo)));
        bool uriInput = roles.Any(role => role.ValueType == typeof(Uri) || role.TypeShape.ElementType == typeof(Uri));
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
            (typeof(IGet), "GET"), (typeof(ISave), "SAVE"), (typeof(ILoad), "LOAD"), (typeof(ICreate), "CREATE"), (typeof(IDelete), "DELETE"),
            (typeof(IListVerb), "LIST"), (typeof(ICopy), "COPY"), (typeof(IMove), "MOVE"), (typeof(IRun), "RUN"), (typeof(IStop), "STOP"),
            (typeof(ISend), "SEND"), (typeof(IDownload), "DOWNLOAD"), (typeof(IPost), "POST"), (typeof(ICheck), "CHECK"), (typeof(IParse), "PARSE"),
            (typeof(IFormat), "FORMAT"), (typeof(ITransform), "TRANSFORM"), (typeof(IWait), "WAIT"), (typeof(IFilter), "FILTER"), (typeof(ISay), "SAY")
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
        "by" or "selector" or "key" => "BY",
        "then" => "THEN",
        _ => null
    };
    private static RoleDirection InferDirection(string verb, string? role) => role?.Equals("WHAT", StringComparison.OrdinalIgnoreCase) == true && (verb is "GET" or "LOAD" or "DOWNLOAD" or "LIST") ? RoleDirection.Output : RoleDirection.Input;

    private static void ValidateNames(IEnumerable<VerbDescriptor> verbs, ICollection<LanguageDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in verbs)
        foreach (string surface in new[] { verb.Name }.Concat(verb.Aliases))
        {
            if (owners.TryGetValue(surface, out string? existing) && !existing.Equals(verb.Name, StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new("FLU-LANG-020", $"Verb surface '{surface}' belongs to both '{existing}' and '{verb.Name}'.", LanguageDiagnosticSeverity.Error));
            else owners[surface] = verb.Name;
        }
    }

    private static void ValidatePatterns(IEnumerable<VerbImplementationDescriptor> implementations, ICollection<LanguageDiagnostic> diagnostics)
    {
        foreach (SentencePattern pattern in implementations.SelectMany(x => x.Patterns))
        {
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (RoleSlotDescriptor role in pattern.Roles)
            foreach (string surface in role.AllSurfaceNames)
            {
                if (ForbiddenRoleSurfaces.Contains(surface)) diagnostics.Add(new("FLU-LANG-021", $"Role surface '{surface}' in pattern '{pattern.StableId}' is reserved by language syntax.", LanguageDiagnosticSeverity.Error, pattern.Constructor.Constructor.DeclaringType));
                if (owners.TryGetValue(surface, out string? existing) && !existing.Equals(role.Name, StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new("FLU-LANG-022", $"Pattern '{pattern.StableId}' maps '{surface}' to both '{existing}' and '{role.Name}'.", LanguageDiagnosticSeverity.Error, pattern.Constructor.Constructor.DeclaringType));
                else owners[surface] = role.Name;
            }
        }
    }

    private static void ValidateModules(IEnumerable<ILanguageModule> modules, ICollection<LanguageDiagnostic> diagnostics)
    {
        foreach (LanguageDiagnostic diagnostic in ModuleGraphValidator.Validate(modules)) diagnostics.Add(diagnostic);
    }

    private static void ValidateSemanticSurface(PredicateDescriptor[] predicates, OperatorDescriptor[] operators, IntrinsicDescriptor[] intrinsics, ICollection<LanguageDiagnostic> diagnostics)
    {
        ValidateSurface("predicate", predicates.Select(x => (x.Name, x.AllSurfaceNames)), diagnostics);
        ValidateSurface("operator", operators.Select(x => (x.Name, x.AllSurfaceNames)), diagnostics);
        ValidateSurface("intrinsic", intrinsics.Select(x => (x.Name, x.AllSurfaceNames)), diagnostics);
    }

    private static void ValidateSurface(string kind, IEnumerable<(string Name, IReadOnlyList<string> Surface)> items, ICollection<LanguageDiagnostic> diagnostics)
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, IReadOnlyList<string> surfaces) in items)
        foreach (string surface in surfaces)
        {
            if (owners.TryGetValue(surface, out string? owner) && !owner.Equals(name, StringComparison.OrdinalIgnoreCase)) diagnostics.Add(new("FLU-LANG-040", $"{kind} surface '{surface}' belongs to both '{owner}' and '{name}'.", LanguageDiagnosticSeverity.Error));
            else owners[surface] = name;
        }
    }

    private static void ValidateStableIds(IEnumerable<ModuleDescriptor> modules, IEnumerable<QualifierDescriptor> qualifiers, IEnumerable<VerbDescriptor> verbs, IEnumerable<PredicateDescriptor> predicates, IEnumerable<OperatorDescriptor> operators, IEnumerable<IntrinsicDescriptor> intrinsics, ICollection<LanguageDiagnostic> diagnostics)
    {
        IEnumerable<(string Id, Type? Type)> all = modules.Select(x => (x.StableId, (Type?)null))
            .Concat(qualifiers.Select(x => (x.StableId, (Type?)null)))
            .Concat(verbs.Select(x => (x.StableId, (Type?)null)))
            .Concat(verbs.SelectMany(x => x.Implementations).Select(x => (x.StableId, (Type?)x.ImplementationType)))
            .Concat(verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Constructors).Select(x => (x.StableId, x.Constructor.DeclaringType)))
            .Concat(verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).Select(x => (x.StableId, x.Constructor.Constructor.DeclaringType)))
            .Concat(verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).SelectMany(x => x.Roles).Select(x => (x.StableId, (Type?)null)))
            .Concat(predicates.Select(x => (x.StableId, (Type?)null)))
            .Concat(operators.Select(x => (x.StableId, (Type?)null)))
            .Concat(intrinsics.Select(x => (x.StableId, (Type?)null)));
        foreach (IGrouping<string, (string Id, Type? Type)> duplicate in all.GroupBy(x => x.Id, StringComparer.Ordinal).Where(x => x.Count() > 1))
            diagnostics.Add(new("FLU-LANG-041", $"Stable ID '{duplicate.Key}' is duplicated {duplicate.Count()} times.", LanguageDiagnosticSeverity.Error, duplicate.Select(x => x.Type).FirstOrDefault(x => x is not null)));
    }

    private static string StableId(MemberInfo member, string fallback, ICollection<LanguageDiagnostic> diagnostics)
    {
        string id = member.GetCustomAttribute<StableIdAttribute>(true)?.Id ?? fallback;
        ValidateStableId(id, member.DeclaringType ?? member as Type, diagnostics);
        return id;
    }

    private static string ValidateAndReturn(string id, Type? relatedType, ICollection<LanguageDiagnostic> diagnostics)
    {
        ValidateStableId(id, relatedType, diagnostics);
        return id;
    }

    private static void ValidateStableId(string id, Type? relatedType, ICollection<LanguageDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsWhiteSpace)) diagnostics.Add(new("FLU-LANG-042", $"Stable ID '{id}' must be non-empty and contain no whitespace.", LanguageDiagnosticSeverity.Error, relatedType));
    }

    private static string PatternIdFromConstructorId(string constructorId) => constructorId.StartsWith("ctor:", StringComparison.OrdinalIgnoreCase) ? "pattern:" + constructorId[5..] : constructorId + ":pattern";
    private static string ParameterSemanticSignature(ParameterDescriptor parameter)
    {
        string signature = $"{parameter.RoleName ?? "service"}:{TypeIdentity(parameter.ParameterType)}:{parameter.Cardinality}:{parameter.Direction}:{parameter.IsService}";
        return parameter.OutputProjection is null ? signature : $"{signature}:{ProjectionSignature(parameter.OutputProjection)}";
    }
    private static string RoleSemanticSignature(ParameterDescriptor parameter)
    {
        string signature = $"{parameter.RoleName}:{TypeIdentity(parameter.ParameterType)}:{parameter.Cardinality}:{parameter.Direction}";
        return parameter.OutputProjection is null ? signature : $"{signature}:{ProjectionSignature(parameter.OutputProjection)}";
    }
    private static string ProjectionSignature(OutputProjectionDescriptor projection) => $"{projection.Kind}:{projection.Member}:{projection.Index}";
    private static string TypeIdentity(Type type) => type.IsGenericType ? $"{type.GetGenericTypeDefinition().FullName}[{string.Join(",", type.GetGenericArguments().Select(TypeIdentity))}]" : type.FullName ?? type.Name;
    private static string ShortHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly) { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { return ex.Types.Where(x => x is not null).Cast<Type>(); } }
    private static string Slug(string text) => new string(text.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
}
