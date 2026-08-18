using FluNET.Classic.Core;

namespace FluNET.Classic.SDK;

public enum CompatibilitySeverity { Info, Warning, Breaking }
public sealed record LanguageCompatibilityChange(CompatibilitySeverity Severity, string Kind, string StableId, string Message);
public sealed record LanguageCompatibilityReport(IReadOnlyList<LanguageCompatibilityChange> Changes)
{
    public bool IsCompatible => Changes.All(x => x.Severity != CompatibilitySeverity.Breaking);
    public IReadOnlyList<LanguageCompatibilityChange> BreakingChanges => Changes.Where(x => x.Severity == CompatibilitySeverity.Breaking).ToArray();
}

public sealed class LanguageCompatibilityAnalyzer
{
    public LanguageCompatibilityReport Compare(LanguageSnapshot previous, LanguageSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous); ArgumentNullException.ThrowIfNull(current); var changes = new List<LanguageCompatibilityChange>();
        if (!previous.LanguageVersion.IsCompatibleWith(current.LanguageVersion))
            changes.Add(new(CompatibilitySeverity.Breaking, "language-version", previous.LanguageVersion.GrammarId, $"Language contract changed from {previous.LanguageVersion.Name} ({previous.LanguageVersion.GrammarId}) to {current.LanguageVersion.Name} ({current.LanguageVersion.GrammarId})."));
        else if (!Equals(previous.LanguageVersion, current.LanguageVersion))
            changes.Add(new(CompatibilitySeverity.Info, "language-version", previous.LanguageVersion.GrammarId, $"Language patch contract changed from {previous.LanguageVersion.Name} to {current.LanguageVersion.Name}."));

        CompareMap(previous.Verbs.ToDictionary(x => x.StableId), current.Verbs.ToDictionary(x => x.StableId), "verb", changes, CompareVerb);
        CompareMap(previous.Qualifiers.ToDictionary(x => x.StableId), current.Qualifiers.ToDictionary(x => x.StableId), "qualifier", changes, CompareQualifier);
        CompareMap(previous.Modules.ToDictionary(x => x.StableId), current.Modules.ToDictionary(x => x.StableId), "module", changes, CompareModule);
        var oldImpl = previous.Verbs.SelectMany(x => x.Implementations).ToDictionary(x => x.StableId); var newImpl = current.Verbs.SelectMany(x => x.Implementations).ToDictionary(x => x.StableId);
        CompareMap(oldImpl, newImpl, "implementation", changes, CompareImplementation);
        var oldPatterns = previous.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).ToDictionary(x => x.StableId); var newPatterns = current.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).ToDictionary(x => x.StableId);
        CompareMap(oldPatterns, newPatterns, "pattern", changes, ComparePattern);
        return new(changes.OrderByDescending(x => x.Severity).ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray());
    }

    private static void CompareMap<T>(IReadOnlyDictionary<string,T> previous, IReadOnlyDictionary<string,T> current, string kind, ICollection<LanguageCompatibilityChange> changes, Action<T,T,ICollection<LanguageCompatibilityChange>> compare)
    {
        foreach ((string id, T oldValue) in previous) { if (!current.TryGetValue(id, out T? newValue)) changes.Add(new(CompatibilitySeverity.Breaking, $"removed-{kind}", id, $"Removed {kind} '{id}'.")); else compare(oldValue, newValue, changes); }
        foreach (string id in current.Keys.Except(previous.Keys, StringComparer.Ordinal)) changes.Add(new(CompatibilitySeverity.Info, $"added-{kind}", id, $"Added {kind} '{id}'."));
    }

    private static void CompareVerb(VerbDescriptor oldValue, VerbDescriptor newValue, ICollection<LanguageCompatibilityChange> changes)
    {
        if (!oldValue.Name.Equals(newValue.Name, StringComparison.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Breaking, "verb-name", oldValue.StableId, $"Verb name changed from {oldValue.Name} to {newValue.Name}."));
        foreach (string alias in oldValue.Aliases.Except(newValue.Aliases, StringComparer.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Breaking, "removed-alias", oldValue.StableId, $"Removed verb alias '{alias}'."));
    }
    private static void CompareQualifier(QualifierDescriptor oldValue, QualifierDescriptor newValue, ICollection<LanguageCompatibilityChange> changes)
    {
        if (!oldValue.Name.Equals(newValue.Name, StringComparison.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Breaking, "qualifier-name", oldValue.StableId, $"Qualifier name changed from {oldValue.Name} to {newValue.Name}."));
        if (oldValue.TargetType != newValue.TargetType) changes.Add(new(CompatibilitySeverity.Breaking, "qualifier-type", oldValue.StableId, $"Qualifier target changed from {oldValue.TargetType?.FullName ?? "-"} to {newValue.TargetType?.FullName ?? "-"}."));
    }
    private static void CompareModule(ModuleDescriptor oldValue, ModuleDescriptor newValue, ICollection<LanguageCompatibilityChange> changes)
    {
        foreach (string dependency in oldValue.Dependencies.Except(newValue.Dependencies, StringComparer.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Warning, "removed-dependency", oldValue.StableId, $"Removed module dependency '{dependency}'."));
    }
    private static void CompareImplementation(VerbImplementationDescriptor oldValue, VerbImplementationDescriptor newValue, ICollection<LanguageCompatibilityChange> changes)
    {
        if (oldValue.ResultType != newValue.ResultType) changes.Add(new(CompatibilitySeverity.Breaking, "result-type", oldValue.StableId, $"Result type changed from {oldValue.ResultType.FullName} to {newValue.ResultType.FullName}."));
        foreach (string capability in newValue.Capabilities.Except(oldValue.Capabilities, StringComparer.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Breaking, "added-capability", oldValue.StableId, $"Implementation now requires capability '{capability}'."));
        foreach (ExecutionTrait trait in oldValue.Traits.Except(newValue.Traits)) changes.Add(new(CompatibilitySeverity.Warning, "removed-trait", oldValue.StableId, $"Execution trait '{trait}' was removed."));
        foreach (ExecutionTrait trait in newValue.Traits.Except(oldValue.Traits))
        {
            CompatibilitySeverity severity = trait switch
            {
                ExecutionTrait.SideEffecting or ExecutionTrait.Transactional => CompatibilitySeverity.Breaking,
                ExecutionTrait.NonDeterministic or ExecutionTrait.LongRunning or ExecutionTrait.Retryable => CompatibilitySeverity.Warning,
                _ => CompatibilitySeverity.Info
            };
            changes.Add(new(severity, "added-trait", oldValue.StableId, $"Execution trait '{trait}' was added."));
        }
    }
    private static void ComparePattern(SentencePattern oldValue, SentencePattern newValue, ICollection<LanguageCompatibilityChange> changes)
    {
        Dictionary<string,RoleSlotDescriptor> oldRoles = oldValue.Roles.ToDictionary(x => x.StableId); Dictionary<string,RoleSlotDescriptor> newRoles = newValue.Roles.ToDictionary(x => x.StableId);
        foreach ((string id, RoleSlotDescriptor oldRole) in oldRoles)
        {
            if (!newRoles.TryGetValue(id, out RoleSlotDescriptor? newRole)) { changes.Add(new(CompatibilitySeverity.Breaking, "removed-role", oldValue.StableId, $"Removed role '{oldRole.Name}'.")); continue; }
            if (oldRole.ValueType != newRole.ValueType || oldRole.Direction != newRole.Direction || oldRole.Cardinality != newRole.Cardinality || oldRole.Required != newRole.Required) changes.Add(new(CompatibilitySeverity.Breaking, "role-contract", oldValue.StableId, $"Role '{oldRole.Name}' contract changed."));
            if (!Equals(oldRole.OutputProjection, newRole.OutputProjection)) changes.Add(new(CompatibilitySeverity.Breaking, "output-projection", oldValue.StableId, $"Role '{oldRole.Name}' output projection changed."));
            foreach (string surface in oldRole.AllSurfaceNames.Except(newRole.AllSurfaceNames, StringComparer.OrdinalIgnoreCase)) changes.Add(new(CompatibilitySeverity.Breaking, "removed-role-surface", oldValue.StableId, $"Role '{oldRole.Name}' no longer accepts '{surface}'."));
        }
        foreach (RoleSlotDescriptor role in newValue.Roles.Where(x => !oldRoles.ContainsKey(x.StableId) && x.Required)) changes.Add(new(CompatibilitySeverity.Breaking, "added-required-role", oldValue.StableId, $"Added required role '{role.Name}'."));
    }
}
