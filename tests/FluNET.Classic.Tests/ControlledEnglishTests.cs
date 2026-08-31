using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ControlledEnglishTests
{
    [Test]
    public void Read_surface_variants_bind_to_one_canonical_sentence()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        Assert.That(engine.Check("READ TEXT FROM {input.txt} INTO [lines].").Success, Is.True);
        Assert.That(engine.Format("GET TEXT FROM {input.txt} INTO [lines]."), Is.EqualTo("READ TEXT FROM {input.txt} INTO [lines]."));
        Assert.That(engine.Format("READ {input.txt} AS TEXT INTO [lines]."), Is.EqualTo("READ TEXT FROM {input.txt} INTO [lines]."));
    }

    [Test]
    public void Formatter_uses_natural_control_flow_canon()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        string source = "IF true, THEN\nSAY \"yes\".\nELSE\nSAY \"no\".\nEND IF. TRY TO\nSAY \"work\".\nON FAILURE,\nSAY \"failed\".\nFINALLY,\nSAY \"done\".\nEND TRY.";

        string formatted = engine.Format(source);
        Assert.That(formatted, Does.Contain("OTHERWISE,"));
        Assert.That(formatted, Does.Contain("TRY TO"));
        Assert.That(formatted, Does.Contain("ON FAILURE,"));
    }

    [Test]
    public void Try_do_is_not_a_second_legacy_grammar()
    {
        using ServiceProvider host = FluNetHost.Create();
        ParseResult parse = host.GetRequiredService<ClassicEngine>().Parse("TRY, DO\nSAY \"old\".\nEND TRY.");
        Assert.That(parse.Diagnostics.Any(x => x.Code == "FLU-SYN-116"), Is.True);
    }

    [Test]
    public async Task Require_has_dedicated_runtime_failure()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        Assert.That((await engine.RunAsync("REQUIRE 5 IS AT LEAST 3.")).Success, Is.True);
        RuntimeResult failure = await engine.RunAsync("REQUIRE 2 IS GREATER THAN 3.");
        Assert.That(failure.Diagnostics.Single().Code, Is.EqualTo("FLU-RUN-040"));
    }

    [Test]
    public async Task Pronouns_use_previous_pipeline_and_current_loop_item()
    {
        var output = new CapturingOutput();
        using ServiceProvider host = FluNetHost.Create(configure: services => services.AddSingleton<IOutputWriter>(output));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("values", new[] { "one", "two" });

        RuntimeResult result = await engine.RunAsync("COUNT [values]. CHECK WHETHER IT IS EQUAL TO 2. FOR EACH [value] IN THEM, DO SAY IT. END FOR.", state);

        Assert.That(result.Success, Is.False, "THEM must refer to a collection, not the scalar produced by COUNT.");
        Assert.That(result.Diagnostics.Any(x => x.Code == "FLU-BIND-181"), Is.True);

        result = await engine.RunAsync("FIND ITEMS IN [values] WHERE true. FOR EACH [value] IN THEM, DO SAY IT. END FOR.", state);
        Assert.That(result.Success, Is.True);
        Assert.That(output.Lines, Is.EqualTo(new[] { "one", "two" }));
    }

    [Test]
    public async Task Find_items_binds_its_to_the_collection_item()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("values", new[] { "a", "alphabet" });

        RuntimeResult result = await engine.RunAsync("FIND ITEMS IN [values] WHERE ITS Length IS GREATER THAN 3 INTO [long]. COUNT [long].", state);
        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.EqualTo(1));
    }

    private sealed class CapturingOutput : IOutputWriter
    {
        public List<string> Lines { get; } = [];
        public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default) { Lines.Add(text); return ValueTask.CompletedTask; }
    }
}
