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

    [Test]
    public void Execution_plan_snapshots_top_level_collections()
    {
        var diagnostics = new List<ExecutionPlanDiagnostic>
        {
            new("test", "TEST", "message")
        };
        var plan = new ExecutionPlan(
            false,
            diagnostics,
            new List<ExecutionPlanStep>(),
            new List<string> { "test.capability" },
            new List<ExecutionTrait> { ExecutionTrait.Pure },
            null);

        diagnostics.Clear();

        Assert.That(plan.Diagnostics, Has.Count.EqualTo(1));
    }

    [Test]
    public void Execution_plan_snapshots_nested_step_collections()
    {
        var capabilities = new List<string> { "test.capability" };
        var step = new ExecutionPlanStep(
            "sentence",
            "TEST",
            "TestVerb",
            "test-pattern",
            typeof(string).FullName,
            null,
            0,
            null,
            capabilities,
            Array.Empty<ExecutionTrait>(),
            Array.Empty<ExecutionPlanRole>(),
            Array.Empty<ExecutionPlanStep>());
        var steps = new List<ExecutionPlanStep> { step };
        var plan = new ExecutionPlan(
            true,
            Array.Empty<ExecutionPlanDiagnostic>(),
            steps,
            Array.Empty<string>(),
            Array.Empty<ExecutionTrait>(),
            typeof(string).FullName);

        capabilities[0] = "mutated";

        Assert.That(plan.Steps[0].Capabilities, Is.EqualTo(new[] { "test.capability" }));
    }
}
