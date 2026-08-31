using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.SDK;

public sealed record ModuleValidationDiagnostic(
    string Code,
    string Message,
    string? Example = null,
    LanguageDiagnosticSeverity Severity = LanguageDiagnosticSeverity.Error);

public sealed record ModuleValidationResult(
    LanguageSnapshot? Snapshot,
    IReadOnlyList<LanguageDiagnostic> LanguageDiagnostics,
    IReadOnlyList<ModuleValidationDiagnostic> Diagnostics)
{
    public bool Success => Snapshot is not null
        && LanguageDiagnostics.All(x => x.Severity != LanguageDiagnosticSeverity.Error)
        && Diagnostics.All(x => x.Severity != LanguageDiagnosticSeverity.Error);
}

public sealed class ModuleTestOptions
{
    public IList<ILanguageModule> Dependencies { get; } = new List<ILanguageModule>();
    public IList<string> Examples { get; } = new List<string>();
    public Action<ValueResolverRegistry>? ConfigureResolvers { get; set; }
    public Action<ValueConversionRegistry>? ConfigureConverters { get; set; }
    public Action<PredicateRegistry>? ConfigurePredicates { get; set; }
}

public static class FluNetModuleTestHarness
{
    public static ModuleValidationResult Validate(ILanguageModule module, Action<ModuleTestOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(module);
        var options = new ModuleTestOptions();
        configure?.Invoke(options);

        ILanguageModule[] modules = options.Dependencies
            .Append(module)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .ToArray();

        var languageDiagnostics = new List<LanguageDiagnostic>(ModuleGraphValidator.Validate(modules));
        var diagnostics = new List<ModuleValidationDiagnostic>();
        if (languageDiagnostics.Any(x => x.Severity == LanguageDiagnosticSeverity.Error))
            return new(null, languageDiagnostics, diagnostics);

        LanguageBuildResult build = new LanguageCompiler().Build(modules: modules);
        languageDiagnostics.AddRange(build.Diagnostics);
        if (!build.Success || build.Snapshot is null)
            return new(null, languageDiagnostics, diagnostics);

        LanguageSnapshot snapshot = build.Snapshot;
        ValidateStableIds(snapshot, diagnostics);
        ValidateSurfaceNames(snapshot, diagnostics);
        foreach (ModuleQualityIssue issue in new ModuleQualityAnalyzer().Analyze(snapshot))
            diagnostics.Add(new(issue.Code, issue.Message, Severity: issue.Severity));

        var resolvers = new ValueResolverRegistry();
        var converters = new ValueConversionRegistry();
        var predicates = new PredicateRegistry();
        options.ConfigureResolvers?.Invoke(resolvers);
        options.ConfigureConverters?.Invoke(converters);
        options.ConfigurePredicates?.Invoke(predicates);

        var lexer = new ClassicLexer();
        var parser = new ClassicParser(snapshot, lexer);
        var binder = new SemanticBinder(snapshot, resolvers, converters, predicates);
        var formatter = new ClassicFormatter(snapshot);

        foreach (string example in options.Examples)
        {
            ParseResult parse = parser.Parse(example);
            foreach (SyntaxDiagnostic diagnostic in parse.Diagnostics)
                diagnostics.Add(new(diagnostic.Code, diagnostic.Message, example));
            if (!parse.Success) continue;

            BoundScript bound = binder.Bind(parse.Script);
            foreach (BindingDiagnostic diagnostic in bound.AllDiagnostics)
                diagnostics.Add(new(diagnostic.Code, diagnostic.Message, example, ToLanguageSeverity(diagnostic.Severity)));
            if (bound.HasErrors) continue;

            string canonical = formatter.Format(parse.Script);
            ParseResult roundTrip = parser.Parse(canonical);
            foreach (SyntaxDiagnostic diagnostic in roundTrip.Diagnostics)
                diagnostics.Add(new("FLU-SDK-003", $"Canonical formatter round-trip failed: {diagnostic.Message}", example));
            if (roundTrip.Success)
            {
                string secondCanonical = formatter.Format(roundTrip.Script);
                if (!canonical.Equals(secondCanonical, StringComparison.Ordinal))
                    diagnostics.Add(new("FLU-SDK-004", "Canonical formatter is not idempotent after a parse/format round-trip.", example));
            }
        }

        return new(snapshot, languageDiagnostics, diagnostics);
    }

    private static void ValidateStableIds(LanguageSnapshot snapshot, ICollection<ModuleValidationDiagnostic> diagnostics)
    {
        IEnumerable<string> ids = snapshot.Modules.Select(x => x.StableId)
            .Concat(snapshot.Qualifiers.Select(x => x.StableId))
            .Concat(snapshot.Verbs.Select(x => x.StableId))
            .Concat(snapshot.Verbs.SelectMany(x => x.Implementations).Select(x => x.StableId))
            .Concat(snapshot.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Constructors).Select(x => x.StableId))
            .Concat(snapshot.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).Select(x => x.StableId))
            .Concat(snapshot.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).SelectMany(x => x.Roles).Select(x => x.StableId));

        foreach (IGrouping<string, string> duplicate in ids.GroupBy(x => x, StringComparer.Ordinal).Where(x => x.Count() > 1))
            diagnostics.Add(new("FLU-SDK-001", $"Stable ID '{duplicate.Key}' is duplicated {duplicate.Count()} times."));
    }

    private static void ValidateSurfaceNames(LanguageSnapshot snapshot, ICollection<ModuleValidationDiagnostic> diagnostics)
    {
        foreach (SentencePattern pattern in snapshot.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns))
        {
            var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (RoleSlotDescriptor role in pattern.Roles)
                foreach (string surface in role.AllSurfaceNames)
                {
                    if (owners.TryGetValue(surface, out string? existing) && !existing.Equals(role.Name, StringComparison.OrdinalIgnoreCase))
                        diagnostics.Add(new("FLU-SDK-002", $"Pattern '{pattern.StableId}' maps surface word '{surface}' to both '{existing}' and '{role.Name}'."));
                    else owners[surface] = role.Name;
                }
        }
    }

    private static LanguageDiagnosticSeverity ToLanguageSeverity(BindingDiagnosticSeverity severity) => severity switch
    {
        BindingDiagnosticSeverity.Info => LanguageDiagnosticSeverity.Info,
        BindingDiagnosticSeverity.Warning => LanguageDiagnosticSeverity.Warning,
        _ => LanguageDiagnosticSeverity.Error
    };
}
