using System.Collections.ObjectModel;
using System.Collections.Frozen;

namespace FluNET.Classic.Core;

public sealed class LanguageSnapshot
{
    private readonly IReadOnlyDictionary<string, VerbDescriptor> _verbs;
    private readonly IReadOnlyDictionary<string, QualifierDescriptor> _qualifiers;
    private readonly IReadOnlyDictionary<string, PredicateDescriptor> _predicates;
    private readonly IReadOnlyDictionary<string, OperatorDescriptor> _operators;
    private readonly IReadOnlyDictionary<string, IntrinsicDescriptor> _intrinsics;

    public LanguageSnapshot(IEnumerable<VerbDescriptor> verbs, IEnumerable<QualifierDescriptor> qualifiers, IEnumerable<ModuleDescriptor> modules)
        : this(verbs, qualifiers, modules, StandardLanguageSurface.Predicates, StandardLanguageSurface.Operators, Array.Empty<IntrinsicDescriptor>())
    {
    }

    public LanguageSnapshot(
        IEnumerable<VerbDescriptor> verbs,
        IEnumerable<QualifierDescriptor> qualifiers,
        IEnumerable<ModuleDescriptor> modules,
        IEnumerable<PredicateDescriptor> predicates,
        IEnumerable<OperatorDescriptor> operators,
        IEnumerable<IntrinsicDescriptor> intrinsics)
    {
        VerbDescriptor[] frozenVerbs = verbs.Select(Freeze).ToArray();
        QualifierDescriptor[] frozenQualifiers = qualifiers.Select(Freeze).ToArray();
        ModuleDescriptor[] frozenModules = modules.Select(Freeze).ToArray();
        PredicateDescriptor[] frozenPredicates = predicates.Select(Freeze).ToArray();
        OperatorDescriptor[] frozenOperators = operators.Select(Freeze).ToArray();
        IntrinsicDescriptor[] frozenIntrinsics = intrinsics.Select(Freeze).ToArray();
        Dictionary<string, VerbDescriptor> verbLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (VerbDescriptor verb in frozenVerbs)
        {
            verbLookup.Add(verb.Name, verb);
            foreach (string alias in verb.Aliases)
                verbLookup.Add(alias, verb);
        }

        Dictionary<string, QualifierDescriptor> qualifierLookup = new(StringComparer.OrdinalIgnoreCase);
        foreach (QualifierDescriptor qualifier in frozenQualifiers)
        {
            qualifierLookup[qualifier.Name] = qualifier;
            foreach (string alias in qualifier.AllAliases)
                qualifierLookup[alias] = qualifier;
        }

        _verbs = new ReadOnlyDictionary<string, VerbDescriptor>(verbLookup);
        _qualifiers = new ReadOnlyDictionary<string, QualifierDescriptor>(qualifierLookup);
        _predicates = new ReadOnlyDictionary<string, PredicateDescriptor>(BuildSurfaceLookup(frozenPredicates, x => x.AllSurfaceNames));
        _operators = new ReadOnlyDictionary<string, OperatorDescriptor>(BuildSurfaceLookup(frozenOperators, x => x.AllSurfaceNames));
        _intrinsics = new ReadOnlyDictionary<string, IntrinsicDescriptor>(BuildSurfaceLookup(frozenIntrinsics, x => x.AllSurfaceNames));

        Verbs = ReadOnlyList(verbLookup.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        Qualifiers = ReadOnlyList(qualifierLookup.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        Predicates = ReadOnlyList(_predicates.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        Operators = ReadOnlyList(_operators.Values.Distinct().OrderBy(x => x.Precedence).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        Intrinsics = ReadOnlyList(_intrinsics.Values.Distinct().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        Modules = ReadOnlyList(frozenModules.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase));
        StructuralSyntax = ReadOnlyList(StandardLanguageSurface.StructuralSyntax);
        LiteralWords = StandardLanguageSurface.LiteralWords.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        Capabilities = ReadOnlyList(Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Capabilities)
            .Concat(Predicates.SelectMany(x => x.RequiredCapabilities))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        ExecutionTraits = ReadOnlyList(Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Traits)
            .Distinct()
            .OrderBy(x => x));
        ReservedWords = StandardLanguageSurface.ReservedWords
            .Concat(Predicates.SelectMany(x => x.AllSurfaceNames).SelectMany(SplitSurface))
            .Concat(Operators.SelectMany(x => x.AllSurfaceNames).SelectMany(SplitSurface))
            .Concat(Intrinsics.SelectMany(x => x.AllSurfaceNames).SelectMany(SplitSurface))
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<VerbDescriptor> Verbs
    {
        get;
    }
    public IReadOnlyList<QualifierDescriptor> Qualifiers
    {
        get;
    }
    public IReadOnlyList<PredicateDescriptor> Predicates
    {
        get;
    }
    public IReadOnlyList<OperatorDescriptor> Operators
    {
        get;
    }
    public IReadOnlyList<IntrinsicDescriptor> Intrinsics
    {
        get;
    }
    public IReadOnlyList<ModuleDescriptor> Modules
    {
        get;
    }
    public IReadOnlyList<string> StructuralSyntax
    {
        get;
    }
    public IReadOnlySet<string> LiteralWords
    {
        get;
    }
    public IReadOnlyList<string> Capabilities
    {
        get;
    }
    public IReadOnlyList<ExecutionTrait> ExecutionTraits
    {
        get;
    }
    public IReadOnlySet<string> ReservedWords
    {
        get;
    }

    public bool TryGetVerb(string name, out VerbDescriptor descriptor) => _verbs.TryGetValue(name, out descriptor!);
    public VerbDescriptor GetVerb(string name) => TryGetVerb(name, out VerbDescriptor result) ? result : throw new KeyNotFoundException($"Unknown verb '{name}'.");
    public bool TryGetQualifier(string name, out QualifierDescriptor descriptor) => _qualifiers.TryGetValue(name, out descriptor!);
    public bool TryGetPredicate(string name, out PredicateDescriptor descriptor) => _predicates.TryGetValue(name, out descriptor!);
    public bool TryGetOperator(string name, out OperatorDescriptor descriptor) => _operators.TryGetValue(name, out descriptor!);
    public bool TryGetIntrinsic(string name, out IntrinsicDescriptor descriptor) => _intrinsics.TryGetValue(name, out descriptor!);
    public IReadOnlyList<VerbImplementationDescriptor> GetOverloads(string verb) => GetVerb(verb).Implementations;

    private static Dictionary<string, T> BuildSurfaceLookup<T>(IEnumerable<T> items, Func<T, IReadOnlyList<string>> surfaces)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (T item in items)
            foreach (string surface in surfaces(item))
                result[surface] = item;
        return result;
    }

    private static IReadOnlyList<T> ReadOnlyList<T>(IEnumerable<T> items) => new ReadOnlyCollection<T>(items.ToArray());

    private static VerbDescriptor Freeze(VerbDescriptor value) => new(
        value.StableId,
        value.Name,
        ReadOnlyList(value.Aliases),
        ReadOnlyList(value.Implementations.Select(Freeze)));

    private static VerbImplementationDescriptor Freeze(VerbImplementationDescriptor value) => new(
        value.StableId,
        value.ImplementationType,
        value.Name,
        ReadOnlyList(value.Aliases),
        ReadOnlyList(value.Qualifiers),
        ReadOnlyList(value.Constructors.Select(Freeze)),
        ReadOnlyList(value.Patterns.Select(Freeze)),
        value.ResultType,
        ReadOnlyList(value.Capabilities),
        ReadOnlyList(value.Traits),
        value.Invoker);

    private static ConstructorDescriptor Freeze(ConstructorDescriptor value) => new(
        value.StableId,
        value.Constructor,
        ReadOnlyList(value.Parameters.Select(Freeze)),
        value.Activator);

    private static ParameterDescriptor Freeze(ParameterDescriptor value) => new(
        value.Parameter,
        value.Name,
        value.ParameterType,
        value.TypeShape,
        value.IsOptional,
        value.DefaultValue,
        value.IsParamArray,
        value.IsService,
        value.RoleName,
        ReadOnlyList(value.SurfaceNames),
        value.Direction,
        value.Cardinality,
        value.Position,
        value.OutputProjection);

    private static SentencePattern Freeze(SentencePattern value) => new(
        value.StableId,
        Freeze(value.Constructor),
        ReadOnlyList(value.Roles.Select(Freeze)));

    private static RoleSlotDescriptor Freeze(RoleSlotDescriptor value) => new(
        value.StableId,
        value.Name,
        value.ValueType,
        value.TypeShape,
        value.Direction,
        value.Cardinality,
        value.Position,
        value.ParameterName,
        value.Required,
        ReadOnlyList(value.SurfaceNames),
        value.OutputProjection);

    private static QualifierDescriptor Freeze(QualifierDescriptor value) => new(value.StableId, value.Name, value.TargetType, ReadOnlyList(value.AllAliases));
    private static ModuleDescriptor Freeze(ModuleDescriptor value) => new(value.StableId, value.Name, value.Version, ReadOnlyList(value.Dependencies), ReadOnlyList(value.Assemblies));
    private static PredicateDescriptor Freeze(PredicateDescriptor value) => new(value.StableId, value.Name, value.Syntax, ReadOnlyList(value.Aliases ?? Array.Empty<string>()), ReadOnlyList(value.SupportedOperandTypes), ReadOnlyList(value.CapabilityRequirements), value.ReferenceOperandType, value.Precedence);
    private static OperatorDescriptor Freeze(OperatorDescriptor value) => new(value.StableId, value.Name, value.Precedence, value.Arity, value.Associativity, ReadOnlyList(value.Aliases ?? Array.Empty<string>()), value.Semantic, value.Compatibility, value.Evaluation, value.ResultType);
    private static IntrinsicDescriptor Freeze(IntrinsicDescriptor value) => new(value.StableId, value.Name, value.Syntax, ReadOnlyList(value.Aliases ?? Array.Empty<string>()), value.Execution, value.StrategyType, value.StrategyRole, value.Semantic);

    private static IEnumerable<string> SplitSurface(string surface) => surface.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
