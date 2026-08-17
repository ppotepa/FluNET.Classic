using FluNET.Classic.Core;

namespace FluNET.Classic.SDK;

public enum ModuleQualitySeverity { Info, Warning, Error }
public sealed record ModuleQualityIssue(ModuleQualitySeverity Severity, string Code, string Message, string? StableId = null);

public sealed class ModuleQualityAnalyzer
{
    public IReadOnlyList<ModuleQualityIssue> Analyze(LanguageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot); var issues = new List<ModuleQualityIssue>();
        foreach (VerbImplementationDescriptor implementation in snapshot.Verbs.SelectMany(x => x.Implementations))
        {
            bool sideEffect = implementation.Traits.Contains(ExecutionTrait.SideEffecting);
            if (sideEffect && implementation.Capabilities.Count == 0) issues.Add(new(ModuleQualitySeverity.Warning, "FLU-SDK-Q001", $"Side-effecting implementation '{implementation.ImplementationType.FullName}' declares no capability.", implementation.StableId));
            if (implementation.Traits.Contains(ExecutionTrait.Retryable) && sideEffect && !implementation.Traits.Contains(ExecutionTrait.Idempotent)) issues.Add(new(ModuleQualitySeverity.Warning, "FLU-SDK-Q002", $"Retryable side-effecting implementation '{implementation.ImplementationType.FullName}' is not marked Idempotent.", implementation.StableId));
            if (implementation.Traits.Contains(ExecutionTrait.Pure) && sideEffect) issues.Add(new(ModuleQualitySeverity.Error, "FLU-SDK-Q003", $"Implementation '{implementation.ImplementationType.FullName}' cannot be both Pure and SideEffecting.", implementation.StableId));
            if (implementation.Traits.Contains(ExecutionTrait.Pure) && implementation.Traits.Contains(ExecutionTrait.NonDeterministic)) issues.Add(new(ModuleQualitySeverity.Warning, "FLU-SDK-Q004", $"Pure implementation '{implementation.ImplementationType.FullName}' is also NonDeterministic.", implementation.StableId));
            foreach (SentencePattern pattern in implementation.Patterns)
            {
                if (pattern.Roles.Count == 0) issues.Add(new(ModuleQualitySeverity.Error, "FLU-SDK-Q005", $"Pattern '{pattern.StableId}' has no language roles.", pattern.StableId));
                foreach (RoleSlotDescriptor role in pattern.Roles.Where(x => x.AllSurfaceNames.Any(s => s.Equals("INTO", StringComparison.OrdinalIgnoreCase)))) issues.Add(new(ModuleQualitySeverity.Error, "FLU-SDK-Q006", $"Role '{role.Name}' illegally claims reserved surface INTO.", pattern.StableId));
            }
        }
        return issues;
    }
}
