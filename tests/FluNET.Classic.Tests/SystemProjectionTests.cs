using FluNET.Classic.System;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class SystemProjectionTests
{
    [Test]
    public async Task System_memory_and_runtime_projections_return_typed_fields()
    {
        var memory = new SystemMemory(100, 80);
        var runtime = new RuntimeInfo("net8.0", "x64", "Windows", 8);

        Assert.That(await new GetWorkingSetMemory(memory).ExecuteAsync(null!), Is.EqualTo(100));
        Assert.That(await new GetGcMemory(memory).ExecuteAsync(null!), Is.EqualTo(80));
        Assert.That(await new GetRuntimeFramework(runtime).ExecuteAsync(null!), Is.EqualTo("net8.0"));
        Assert.That(await new GetRuntimeArchitecture(runtime).ExecuteAsync(null!), Is.EqualTo("x64"));
        Assert.That(await new GetRuntimeOsArchitecture(runtime).ExecuteAsync(null!), Is.EqualTo("Windows"));
        Assert.That(await new GetProcessorCount(runtime).ExecuteAsync(null!), Is.EqualTo(8));
    }
}
