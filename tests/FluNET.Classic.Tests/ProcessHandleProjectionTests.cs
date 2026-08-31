using FluNET.Classic.Core;
using FluNET.Classic.Standard.Process;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ProcessHandleProjectionTests
{
    [Test]
    public async Task Process_handle_projections_expose_identity_and_start_time()
    {
        var spec = new ProcessSpec("dotnet");
        var started = DateTimeOffset.UtcNow;
        var handle = new ProcessHandle(42, spec, started);
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetProcessId(handle).ExecuteAsync(context), Is.EqualTo(42));
        Assert.That(await new GetProcessSpec(handle).ExecuteAsync(context), Is.EqualTo(spec));
        Assert.That(await new GetProcessStartedAt(handle).ExecuteAsync(context), Is.EqualTo(started));
    }
}
