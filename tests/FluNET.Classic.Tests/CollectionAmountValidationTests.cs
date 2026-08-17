using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class CollectionAmountValidationTests
{
    [TestCase("TAKE -1 FROM [items] INTO [result].")]
    [TestCase("SKIP -2 FROM [items] INTO [result].")]
    public void Negative_literal_amount_is_rejected_during_binding(string source)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check(
            source,
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["items"] = typeof(int[]) });

        Assert.That(check.Parse.Success, Is.True);
        Assert.That(check.Success, Is.False);
        Assert.That(check.Bound!.Diagnostics.Any(x => x.Code == "FLU-BIND-166"), Is.True);
    }

    [TestCase("TAKE [amount] FROM [items] INTO [result].")]
    [TestCase("SKIP [amount] FROM [items] INTO [result].")]
    public async Task Negative_dynamic_amount_is_rejected_at_runtime(string source)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { 1, 2, 3 });
        state.SetVariable("amount", -1);

        RuntimeResult result = await engine.RunAsync(source, state);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Message.Contains("non-negative amount", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public void Lexer_and_formatter_preserve_signed_numeric_literal()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("take -1 from [items] into [result]");

        Assert.That(formatted, Is.EqualTo("TAKE -1 FROM [items] INTO [result]."));
    }
}
