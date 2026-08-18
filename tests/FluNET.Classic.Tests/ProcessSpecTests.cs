using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Process;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ProcessSpecTests
{
    [Test]
    public async Task Process_spec_is_created_from_typed_command_line()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CREATE PROCESS FROM \"dotnet\" WITH \"--info\" INTO [spec].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["spec"], Is.TypeOf<ProcessSpec>());
        Assert.That(((ProcessSpec)result.State.Variables["spec"]!).FileName, Is.EqualTo("dotnet"));
        Assert.That(((ProcessSpec)result.State.Variables["spec"]!).Arguments, Is.EqualTo("--info"));
    }
}
