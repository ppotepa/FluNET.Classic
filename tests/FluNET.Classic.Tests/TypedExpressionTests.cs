using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class TypedExpressionTests
{
    [Test]
    public void Between_is_preserved_as_a_dedicated_syntax_node()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ParseResult parse = engine.Parse("CHECK IF 5 BETWEEN 1 AND 10 INTO [inside].");

        Assert.That(parse.Success, Is.True, string.Join("; ", parse.Diagnostics.Select(x => x.Message)));
        var check = (CheckStageNode)((PipelineNode)parse.Script.Statements.Single()).Stages.Single();
        Assert.That(check.Condition, Is.TypeOf<BetweenExpression>());
        Assert.That(engine.Format("CHECK IF 5 BETWEEN 1 AND 10 INTO [inside]."), Does.Contain("5 BETWEEN 1 AND 10"));
    }

    [Test]
    public async Task Between_executes_with_typed_comparable_values()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("value", 5);

        RuntimeResult result = await engine.RunAsync("CHECK IF [value] BETWEEN 1 AND 10 INTO [inside].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("inside", out object? inside), Is.True);
        Assert.That(inside, Is.EqualTo(true));
    }

    [Test]
    public void Binder_rejects_operator_with_incompatible_operand_types()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult result = engine.Check("CHECK IF 5 BEFORE \"later\" INTO [invalid].");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Bound?.Diagnostics.Any(x => x.Code == "FLU-BIND-158"), Is.True);
    }

    [Test]
    public void Binder_rejects_text_operator_for_non_text_operand()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult result = engine.Check("CHECK IF 5 STARTS WITH \"5\" INTO [invalid].");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Bound?.Diagnostics.Any(x => x.Code == "FLU-BIND-158"), Is.True);
    }

    [Test]
    public async Task Exists_reference_inference_is_driven_by_predicate_metadata()
    {
        string file = Path.GetTempFileName();
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();
            PredicateDescriptor exists = snapshot.Predicates.Single(x => x.Name == "EXISTS");
            Assert.That(exists.ReferenceOperandType, Is.EqualTo(typeof(FileInfo)));

            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"CHECK IF {{{file}}} EXISTS INTO [exists].");

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("exists", out object? value), Is.True);
            Assert.That(value, Is.EqualTo(true));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
