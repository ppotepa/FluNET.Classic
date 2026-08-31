namespace FluNET.Classic.Core;

/// <summary>Shared validation for canonical sentence-role conventions.</summary>
public static class LanguageSurfaceValidation
{
    public static IReadOnlyList<LanguageDiagnostic> Validate(IEnumerable<VerbImplementationDescriptor> implementations)
    {
        ArgumentNullException.ThrowIfNull(implementations);
        var diagnostics = new List<LanguageDiagnostic>();
        foreach (VerbImplementationDescriptor implementation in implementations)
            foreach (SentencePattern pattern in implementation.Patterns)
            {
                Type? relatedType = pattern.Constructor.Constructor.DeclaringType;
                foreach (RoleSlotDescriptor role in pattern.Roles)
                {
                    if (!LanguageRoleNames.IsContextual(role.Name))
                        diagnostics.Add(new("FLU-LANG-043", $"Role '{role.Name}' in pattern '{pattern.StableId}' is not part of the canonical contextual role vocabulary.", LanguageDiagnosticSeverity.Error, relatedType));

                    foreach (string surface in role.AllSurfaceNames)
                    {
                        if (LanguageRoleNames.StructuralOnly.Contains(surface))
                            diagnostics.Add(new("FLU-LANG-044", $"Role '{role.Name}' in pattern '{pattern.StableId}' illegally claims structural surface '{surface}'.", LanguageDiagnosticSeverity.Error, relatedType));
                    }

                    if (!implementation.Name.Equals("TRANSFORM", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (role.AllSurfaceNames.Any(surface => surface.Equals(LanguageRoleNames.As, StringComparison.OrdinalIgnoreCase)))
                        diagnostics.Add(new("FLU-LANG-045", $"TRANSFORM pattern '{pattern.StableId}' uses AS; use TO for target representation/state and USING for method/strategy.", LanguageDiagnosticSeverity.Error, relatedType));
                    if ((role.Name.Equals(LanguageRoleNames.To, StringComparison.OrdinalIgnoreCase) || role.Name.Equals(LanguageRoleNames.Using, StringComparison.OrdinalIgnoreCase))
                        && role.Direction == RoleDirection.Output)
                        diagnostics.Add(new("FLU-LANG-046", $"TRANSFORM role '{role.Name}' in pattern '{pattern.StableId}' must be an input semantic choice; produced values are bound with INTO.", LanguageDiagnosticSeverity.Error, relatedType));
                }
            }
        return diagnostics;
    }
}
