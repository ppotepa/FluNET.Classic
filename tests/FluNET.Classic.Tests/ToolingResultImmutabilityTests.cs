using FluNET.Classic.Core;
using FluNET.Classic.Binding;
using FluNET.Classic.Tooling;
using FluNET.Classic.Runtime;
using FluNET.Classic.Syntax;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ToolingResultImmutabilityTests
{
    [Test]
    public void Document_analysis_snapshots_diagnostics()
    {
        var diagnostics = new List<DocumentDiagnostic>
        {
            new("test", "TEST", "message", new TextSpan(0, 1))
        };
        var analysis = new DocumentAnalysis(false, null, diagnostics, new ExecutionPlan(
            false,
            Array.Empty<ExecutionPlanDiagnostic>(),
            Array.Empty<ExecutionPlanStep>(),
            Array.Empty<string>(),
            Array.Empty<ExecutionTrait>(),
            null));

        diagnostics.Clear();

        Assert.That(analysis.Diagnostics, Has.Count.EqualTo(1));
    }

    [Test]
    public void Signature_help_snapshots_signatures()
    {
        var signatures = new List<SignatureInfo> { new("TEST") };
        var help = new SignatureHelpInfo(signatures);

        signatures.Clear();

        Assert.That(help.Signatures, Has.Count.EqualTo(1));
    }
}
