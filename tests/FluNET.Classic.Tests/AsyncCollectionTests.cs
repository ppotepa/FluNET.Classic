using System.Runtime.CompilerServices;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class AsyncCollectionTests
{
    [Test]
    public async Task Count_accepts_async_enumerable_source()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers());

        RuntimeResult result = await engine.RunAsync("COUNT [items] INTO [count].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("count", out object? count), Is.True);
        Assert.That(count, Is.EqualTo(4));
    }

    [Test]
    public async Task Filter_accepts_async_enumerable_source_and_preserves_element_type()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers());

        RuntimeResult result = await engine.RunAsync("FILTER [items] WHERE Value > 2 INTO [filtered].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("filtered", out object? filtered), Is.True);
        Assert.That(filtered, Is.TypeOf<NumberItem[]>());
        Assert.That(((NumberItem[])filtered!).Select(x => x.Value), Is.EqualTo(new[] { 3, 4 }));
    }

    [Test]
    public async Task For_each_streams_async_enumerable_without_exposing_iterator()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers());

        RuntimeResult result = await engine.RunAsync("""
            FOR EACH [item] IN [items] THEN {
                CHECK IF [item.Value] > 0.
            }
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("item", out _), Is.False);
    }

    private static async IAsyncEnumerable<NumberItem> Numbers([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (int value in new[] { 1, 2, 3, 4 })
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new NumberItem(value);
        }
    }

    private sealed record NumberItem(int Value);
}
