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
    public async Task Host_can_register_new_operator_evaluator_by_stable_id()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new TestOperatorModule());
        options.ConfigureOperatorEvaluators = registry => registry.Register(
            "operator:test:same-length-as",
            (operands, _) => (operands[0]?.ToString()?.Length ?? 0) == (operands[1]?.ToString()?.Length ?? 0));
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CHECK IF \"abc\" SAME LENGTH AS \"xyz\" INTO [sameLength].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["sameLength"], Is.EqualTo(true));
    }

    [Test]
    public async Task Missing_custom_operator_evaluator_fails_explicitly()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new TestOperatorModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CHECK IF \"abc\" SAME LENGTH AS \"xyz\" INTO [sameLength].");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Message.Contains("no registered evaluator", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void Formatter_uses_module_operator_precedence_from_snapshot()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new TestOperatorModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("CHECK IF false OR \"alpha\" SAME AS \"alpha\" INTO [same]");

        Assert.That(formatted, Is.EqualTo("CHECK IF false OR \"alpha\" SAME AS \"alpha\" INTO [same]."));
    }

    [Test]
    public void Formatter_uses_module_predicate_syntax_from_snapshot()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new TestOperatorModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("CHECK IF [value] PRESENT INTO [ok]");

        Assert.That(formatted, Is.EqualTo("CHECK IF [value] PRESENT INTO [ok]."));
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
                Evaluation: OperatorEvaluationKind.Equal),
            new OperatorDescriptor(
                "operator:test:same-length-as",
                "SAME LENGTH AS",
                4,
                Compatibility: OperatorCompatibilityRule.StringPair,
                Evaluation: OperatorEvaluationKind.Custom)
        };
        public override IReadOnlyCollection<PredicateDescriptor> Predicates => new[]
        {
            new PredicateDescriptor("predicate:test:present", "PRESENT", PredicateSyntaxKind.Postfix)
        };
    }
}
