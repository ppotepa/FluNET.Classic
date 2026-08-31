using FluNET.Classic.Core;
using FluNET.Classic.SDK;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class SdkResultImmutabilityTests
{
    [Test]
    public void Compatibility_report_snapshots_changes()
    {
        var changes = new List<LanguageCompatibilityChange>
        {
            new(CompatibilitySeverity.Info, "test", "test:one", "one")
        };

        var report = new LanguageCompatibilityReport(changes);
        changes.Clear();

        Assert.That(report.Changes, Has.Count.EqualTo(1));
    }

    [Test]
    public void Module_validation_result_snapshots_diagnostic_lists()
    {
        var languageDiagnostics = new List<LanguageDiagnostic>();
        var diagnostics = new List<ModuleValidationDiagnostic>
        {
            new("TEST", "message")
        };

        var result = new ModuleValidationResult(null, languageDiagnostics, diagnostics);
        diagnostics.Clear();

        Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
    }
}
