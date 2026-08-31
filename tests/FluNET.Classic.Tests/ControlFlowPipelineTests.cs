using FluNET.Classic.Core;
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
            IF [flag] IS true, THEN
                CHECK IF true INTO [inside].
            ELSE
                CHECK IF false INTO [inside].
            END IF.
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("inside", out object? inside), Is.True);
        Assert.That(inside, Is.EqualTo(true));
    }

    [Test]
    public async Task Variable_created_in_only_one_if_branch_is_removed_at_runtime()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("flag", true);

        RuntimeResult result = await engine.RunAsync("""
            IF [flag] IS true, THEN
                CHECK IF true INTO [temporary].
            ELSE
                CHECK IF false.
            END IF.
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("temporary", out _), Is.False);
    }

    [Test]
    public async Task For_each_does_not_leak_last_iteration_pipeline_value()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { "a", "b" });

        RuntimeResult result = await engine.RunAsync("""
            FOR EACH [item] IN [items], DO
                SAY [item].
            END FOR.
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("item", out _), Is.False);
    }

    [Test]
    public async Task Parallel_loop_cancels_sibling_iterations_after_failure()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new FailFastFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        var state = new RuntimeState();
        state.SetVariable("items", new[] { 0, 1, 2, 3, 4, 5 });
        DateTimeOffset started = DateTimeOffset.UtcNow;

        RuntimeResult result = await host.GetRequiredService<ClassicEngine>().RunAsync("FOR EACH [item] IN [items], PARALLEL 2, DO\nPROBE [item].\nEND FOR.", state);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Select(x => x.Code), Does.Contain("TEST-FAIL"));
        Assert.That(DateTimeOffset.UtcNow - started, Is.LessThan(TimeSpan.FromSeconds(2)));
    }

    public sealed class FailFastFixtureModule : LanguageModule
    {
        public override string Name => "fail-fast-fixture";
        public override IReadOnlyCollection<global::System.Reflection.Assembly> Assemblies => new[] { typeof(FailFastFixtureModule).Assembly };
    }

    [Verb("PROBE")]
    public sealed class Probe : IVerb<int>, ITransform, IWhat<int>, IPipelineConsumer<int>, IPipelineProducer<int>
    {
        private readonly int _value;
        public Probe([What] int value) => _value = value;

        public async ValueTask<int> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (_value == 0)
                throw new ExecutionFailureException("TEST-FAIL", "Probe failed.");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return _value;
        }
    }
}
