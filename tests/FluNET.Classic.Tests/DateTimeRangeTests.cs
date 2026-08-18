using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class DateTimeRangeTests
{
    [Test]
    public async Task Range_can_be_created_projected_and_validated()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        string source = """
            PARSE DATETIME FROM "2026-08-18T08:00:00+02:00" INTO [start].
            PARSE DATETIME FROM "2026-08-18T10:00:00+02:00" INTO [end].
            CREATE RANGE FROM [start] TO [end] INTO [range].
            CHECK IF [range] IS VALID INTO [valid].
            GET DURATION FROM [range] INTO [duration].
            """;

        RuntimeResult result = await engine.RunAsync(source);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["valid"], Is.EqualTo(true));
        Assert.That(result.State.Variables["duration"], Is.EqualTo(TimeSpan.FromHours(2)));
    }
}
