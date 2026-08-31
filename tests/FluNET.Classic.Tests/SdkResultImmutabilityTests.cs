using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.SDK;
using Microsoft.Extensions.DependencyInjection;
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
        Assert.That(() => ((IList<LanguageCompatibilityChange>)report.Changes).Add(new(CompatibilitySeverity.Info, "test", "test:two", "two")), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Module_quality_results_are_read_only()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();
        IReadOnlyList<ModuleQualityIssue> issues = new ModuleQualityAnalyzer().Analyze(snapshot);

        Assert.That(() => ((IList<ModuleQualityIssue>)issues).Add(new(LanguageDiagnosticSeverity.Info, "TEST", "test")), Throws.TypeOf<NotSupportedException>());
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
