using System.Collections.Concurrent;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ExecutionObserverTests
{
    [Test]
    public async Task RunPublishesLifecycleAndStageEvents()
    {
        var observer = new CapturingObserver();
        using ServiceProvider host = FluNetHost.Create(new FluNetOptions { ExecutionObserver = observer });
        RuntimeResult result = await host.GetRequiredService<ClassicEngine>().RunAsync("SAY \"observer\".");

        Assert.That(result.Success, Is.True);
        Assert.That(observer.Events.Select(x => x.Kind), Does.Contain(ExecutionEventKind.RunStarted));
        Assert.That(observer.Events.Select(x => x.Kind), Does.Contain(ExecutionEventKind.StageStarted));
        Assert.That(observer.Events.Select(x => x.Kind), Does.Contain(ExecutionEventKind.StageCompleted));
        Assert.That(observer.Events.Last().Kind, Is.EqualTo(ExecutionEventKind.RunCompleted));
    }

    [Test]
    public async Task ObserverFailureDoesNotFailProgram()
    {
        using ServiceProvider host = FluNetHost.Create(new FluNetOptions { ExecutionObserver = new ThrowingObserver() });
        RuntimeResult result = await host.GetRequiredService<ClassicEngine>().RunAsync("SAY \"observer\".");
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Parallel_stages_publish_unique_trace_sequences()
    {
        var observer = new CapturingObserver();
        using ServiceProvider host = FluNetHost.Create(new FluNetOptions { ExecutionObserver = observer });
        var state = new RuntimeState();
        state.SetVariable("items", Enumerable.Range(1, 32).ToArray());

        RuntimeResult result = await host.GetRequiredService<ClassicEngine>().RunAsync("FOR EACH [item] IN [items], PARALLEL 4, DO\nCHECK IF true.\nEND FOR.", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        IReadOnlyList<ExecutionTraceEntry> trace = result.Trace ?? Array.Empty<ExecutionTraceEntry>();
        Assert.That(trace.Select(x => x.Sequence).Distinct().Count(), Is.EqualTo(trace.Count));
        Assert.That(observer.Events.Select(x => x.Sequence).Distinct().Count(), Is.EqualTo(observer.Events.Count));
    }

    [Test]
    public async Task Concurrent_runs_on_one_executor_keep_trace_state_isolated()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        CheckResult check = engine.Check("SAY \"concurrent\".");
        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));

        BoundExecutor executor = host.GetRequiredService<BoundExecutor>();
        RuntimeResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => executor.ExecuteAsync(check.Bound!).AsTask()).ToArray());

        foreach (RuntimeResult result in results)
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Trace, Has.Count.EqualTo(1));
            Assert.That(result.Trace!.Select(x => x.Sequence), Is.EqualTo(new[] { 3 }));
        }
    }

    [Test]
    public async Task Concurrent_checks_on_one_engine_keep_binding_state_isolated()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult[] checks = await Task.WhenAll(
            Enumerable.Range(0, 16).Select(_ => Task.Run(() => engine.Check("SAY \"concurrent\"."))).ToArray());

        Assert.That(checks, Has.All.Matches<CheckResult>(check => check.Success));
    }

    private sealed class CapturingObserver : IExecutionObserver
    {
        public ConcurrentQueue<ExecutionEvent> Events { get; } = new();
        public ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
        {
            Events.Enqueue(executionEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : IExecutionObserver
    {
        public ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("observer failure");
    }
}
