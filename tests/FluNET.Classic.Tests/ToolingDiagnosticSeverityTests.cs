using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ToolingDiagnosticSeverityTests
{
    [Test]
    public void Syntax_diagnostics_are_reported_as_errors()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService documents = host.GetRequiredService<ClassicDocumentService>();

        DocumentAnalysis analysis = documents.Analyze("SAY \"hello\";");

        Assert.That(analysis.Success, Is.False);
        Assert.That(analysis.Diagnostics, Is.Not.Empty);
        Assert.That(analysis.Diagnostics.All(x => x.Severity == LanguageDiagnosticSeverity.Error), Is.True);
    }

    [Test]
    public void Binding_diagnostics_preserve_error_severity()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService documents = host.GetRequiredService<ClassicDocumentService>();

        DocumentAnalysis analysis = documents.Analyze("BOGUS.");

        DocumentDiagnostic diagnostic = analysis.Diagnostics.Single(x => x.Source == "binding");
        Assert.That(diagnostic.Code, Is.EqualTo("FLU-BIND-001"));
        Assert.That(diagnostic.Severity, Is.EqualTo(LanguageDiagnosticSeverity.Error));
    }
}
