using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ControlFlowPipelineTests
{
    [Test]
    public async Task If_does_not_leak_branch_pipeline_value_but_keeps_explicit_outputs()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("flag", true);

        RuntimeResult result = await engine.RunAsync("""
            IF [flag] IS true THEN {
                CHECK IF true INTO [inside].
            } ELSE {
                CHECK IF false INTO [inside].
            }
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("inside", out object? inside), Is.True);
        Assert.That(inside, Is.EqualTo(true));
    }

    [Test]
    public async Task For_each_does_not_leak_last_iteration_pipeline_value()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { "a", "b" });

        RuntimeResult result = await engine.RunAsync("""
            FOR EACH [item] IN [items] THEN {
                SAY [item].
            }
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("item", out _), Is.False);
    }
}
