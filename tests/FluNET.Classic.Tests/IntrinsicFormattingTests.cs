using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class IntrinsicFormattingTests
{
    [Test]
    public void Formatter_uses_custom_intrinsic_syntax_metadata_without_name_specific_logic()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("top 3 from [items] into [result]");

        Assert.That(formatted, Is.EqualTo("TOP 3 FROM [items] INTO [result]."));
    }
}
