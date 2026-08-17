using System.Reflection;
using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class OperatorDescriptorTests
{
    [Test]
    public async Task Operator_alias_uses_canonical_descriptor_evaluation()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CHECK IF 5 == 5 INTO [ok].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["ok"], Is.EqualTo(true));
        Assert.That(engine.Format("CHECK IF 5 == 5 INTO [ok]"), Is.EqualTo("CHECK IF 5 = 5 INTO [ok]."));
    }

    [TestCase("CHECK IF \"abc\" STARTS WITH 1 INTO [ok].")]
    [TestCase("CHECK IF 5 BEFORE 6 INTO [ok].")]
    public void Compatibility_rule_rejects_invalid_operand_types(string source)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check(source);

        Assert.That(check.Success, Is.False);
        Assert.That(check.Bound!.Diagnostics.Any(x => x.Code == "FLU-BIND-158"), Is.True);
    }

    [Test]
    public async Task Module_can_add_new_operator_surface_without_runtime_name_switch()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new TestOperatorModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CHECK IF \"alpha\" SAME AS \"alpha\" INTO [same].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["same"], Is.EqualTo(true));
    }

    [Test]
    public async Task Between_keeps_descriptor_identity_and_runtime_semantics()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CHECK IF 5 BETWEEN 1 AND 10 INTO [inside].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["inside"], Is.EqualTo(true));
        Assert.That(engine.Format("CHECK IF 5 BETWEEN 1 AND 10 INTO [inside]"), Is.EqualTo("CHECK IF 5 BETWEEN 1 AND 10 INTO [inside]."));
    }

    private sealed class TestOperatorModule : LanguageModule
    {
        public override string Name => "test-operators";
        public override IReadOnlyCollection<Assembly> Assemblies => Array.Empty<Assembly>();
        public override IReadOnlyCollection<OperatorDescriptor> Operators => new[]
        {
            new OperatorDescriptor(
                "operator:test:same-as",
                "SAME AS",
                4,
                Compatibility: OperatorCompatibilityRule.ComparablePair,
                Evaluation: OperatorEvaluationKind.Equal)
        };
    }
}
