using FluNET.Classic.Core;

namespace FluNET.Classic.SDK;

public sealed record ModuleQualityIssue(LanguageDiagnosticSeverity Severity, string Code, string Message, string? StableId = null);

public sealed class ModuleQualityAnalyzer
{
    public IReadOnlyList<ModuleQualityIssue> Analyze(LanguageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot); var issues = new List<ModuleQualityIssue>();
        foreach (VerbImplementationDescriptor implementation in snapshot.Verbs.SelectMany(x => x.Implementations))
        {
            bool sideEffect = implementation.Traits.Contains(ExecutionTrait.SideEffecting);
            bool pure = implementation.Traits.Contains(ExecutionTrait.Pure);
            bool transactional = implementation.Traits.Contains(ExecutionTrait.Transactional);
            bool streaming = implementation.Traits.Contains(ExecutionTrait.Streaming);
            bool hasAsyncShape = ClrTypeShape.IsAsyncEnumerableType(implementation.ResultType) || implementation.Patterns.SelectMany(x => x.Roles).Any(x => ClrTypeShape.IsAsyncEnumerableType(x.ValueType));

            if (sideEffect && implementation.Capabilities.Count == 0) issues.Add(new(LanguageDiagnosticSeverity.Warning, "FLU-SDK-Q001", $"Side-effecting implementation '{implementation.ImplementationType.FullName}' declares no capability.", implementation.StableId));
            if (implementation.Traits.Contains(ExecutionTrait.Retryable) && sideEffect && !implementation.Traits.Contains(ExecutionTrait.Idempotent)) issues.Add(new(LanguageDiagnosticSeverity.Warning, "FLU-SDK-Q002", $"Retryable side-effecting implementation '{implementation.ImplementationType.FullName}' is not marked Idempotent.", implementation.StableId));
            if (pure && sideEffect) issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q003", $"Implementation '{implementation.ImplementationType.FullName}' cannot be both Pure and SideEffecting.", implementation.StableId));
            if (pure && implementation.Traits.Contains(ExecutionTrait.NonDeterministic)) issues.Add(new(LanguageDiagnosticSeverity.Warning, "FLU-SDK-Q004", $"Pure implementation '{implementation.ImplementationType.FullName}' is also NonDeterministic.", implementation.StableId));
            if (transactional && !sideEffect) issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q007", $"Transactional implementation '{implementation.ImplementationType.FullName}' must also be SideEffecting.", implementation.StableId));
            if (hasAsyncShape && !streaming) issues.Add(new(LanguageDiagnosticSeverity.Warning, "FLU-SDK-Q008", $"Implementation '{implementation.ImplementationType.FullName}' exposes IAsyncEnumerable<T> but is not marked Streaming.", implementation.StableId));
            if (streaming && !hasAsyncShape) issues.Add(new(LanguageDiagnosticSeverity.Warning, "FLU-SDK-Q009", $"Implementation '{implementation.ImplementationType.FullName}' is marked Streaming but exposes no async-stream result or role.", implementation.StableId));

            foreach (SentencePattern pattern in implementation.Patterns)
            {
                if (pattern.Roles.Count == 0) issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q005", $"Pattern '{pattern.StableId}' has no language roles.", pattern.StableId));
                foreach (RoleSlotDescriptor role in pattern.Roles)
                {
                    if (!LanguageRoleNames.IsContextual(role.Name))
                        issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q010", $"Role '{role.Name}' is not part of the canonical contextual role vocabulary.", pattern.StableId));

                    foreach (string surface in role.AllSurfaceNames)
                    {
                        if (LanguageRoleNames.StructuralOnly.Contains(surface))
                            issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q006", $"Role '{role.Name}' illegally claims structural surface '{surface}'.", pattern.StableId));
                        else if (!surface.Equals(role.Name, StringComparison.OrdinalIgnoreCase) && LanguageRoleNames.Contextual.Contains(surface))
                            issues.Add(new(LanguageDiagnosticSeverity.Info, "FLU-SDK-Q011", $"Role '{role.Name}' exposes cross-role surface alias '{surface}'; keep it only when the alternate wording is intentionally pattern-scoped.", pattern.StableId));
                    }

                    if (implementation.Name.Equals("TRANSFORM", StringComparison.OrdinalIgnoreCase)
                        && role.AllSurfaceNames.Any(surface => surface.Equals(LanguageRoleNames.As, StringComparison.OrdinalIgnoreCase)))
                        issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q012", "TRANSFORM uses TO for a target representation/state and USING for a method/strategy; AS is not a transformation role.", pattern.StableId));

                    if (implementation.Name.Equals("TRANSFORM", StringComparison.OrdinalIgnoreCase)
                        && (role.Name.Equals(LanguageRoleNames.To, StringComparison.OrdinalIgnoreCase) || role.Name.Equals(LanguageRoleNames.Using, StringComparison.OrdinalIgnoreCase))
                        && role.Direction == RoleDirection.Output)
                        issues.Add(new(LanguageDiagnosticSeverity.Error, "FLU-SDK-Q013", $"TRANSFORM role '{role.Name}' must describe an input semantic choice, not an output slot; bind produced values with INTO.", pattern.StableId));
                }
            }
        }
        return issues;
    }
}
