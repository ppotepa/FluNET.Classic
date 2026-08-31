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

    private sealed class CapturingObserver : IExecutionObserver
    {
        public List<ExecutionEvent> Events { get; } = [];
        public ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(executionEvent);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingObserver : IExecutionObserver
    {
        public ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default) => throw new InvalidOperationException("observer failure");
    }
}
