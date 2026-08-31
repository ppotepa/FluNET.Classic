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
