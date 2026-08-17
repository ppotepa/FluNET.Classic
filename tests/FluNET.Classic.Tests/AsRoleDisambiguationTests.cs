using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class AsRoleDisambiguationTests
{
    [Test]
    public async Task AS_variable_is_a_semantic_role_when_the_pattern_declares_AS()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("representation", "JSON");

        RuntimeResult result = await engine.RunAsync("INTERPRET \"payload\" AS [representation] INTO [result].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["result"], Is.EqualTo("JSON:payload"));
    }

    [Test]
    public void Legacy_AS_result_alias_remains_accepted_when_AS_is_not_a_role()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("SAY \"hello\" AS [result]");

        Assert.That(formatted, Is.EqualTo("SAY \"hello\" INTO [result]."));
    }

    [Test]
    public void AS_role_survives_canonical_formatting_instead_of_becoming_INTO()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("INTERPRET \"payload\" AS [representation] INTO [result]");

        Assert.That(formatted, Is.EqualTo("INTERPRET \"payload\" AS [representation] INTO [result]."));
    }

    private static ServiceProvider CreateHost()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        return FluNetHost.Create(options);
    }
}
