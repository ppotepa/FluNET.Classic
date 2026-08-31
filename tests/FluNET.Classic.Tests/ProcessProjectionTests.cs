using FluNET.Classic.Core;
using FluNET.Classic.Standard.Process;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ProcessProjectionTests
{
    [Test]
    public async Task Duration_projection_returns_process_duration()
    {
        var expected = TimeSpan.FromMilliseconds(125);
        var result = await new GetProcessDuration(new ProcessResult(0, "out", string.Empty, expected))
            .ExecuteAsync(new VerbExecutionContext(null, new Dictionary<string, object?>(), null));

        Assert.That(result, Is.EqualTo(expected));
    }
}
