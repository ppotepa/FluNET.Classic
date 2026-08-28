using System.Runtime.CompilerServices;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class AsyncCollectionTests
{
    [Test]
    public async Task Count_consumes_async_enumerable_without_materialized_result()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var counter = new EnumerationCounter();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers(counter));

        RuntimeResult result = await engine.RunAsync("COUNT [items] INTO [count].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("count", out object? count), Is.True);
        Assert.That(count, Is.EqualTo(4));
        Assert.That(counter.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task Filter_over_async_source_is_lazy_and_preserves_stream_type()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var counter = new EnumerationCounter();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers(counter));

        RuntimeResult result = await engine.RunAsync("FILTER [items] WHERE Value > 2 INTO [filtered].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(counter.Count, Is.EqualTo(0), "FILTER should not enumerate an async source until the result is consumed.");
        Assert.That(result.State.TryGetVariable("filtered", out object? filtered), Is.True);
        Assert.That(filtered, Is.AssignableTo<IAsyncEnumerable<NumberItem>>());

        List<int> values = await Values((IAsyncEnumerable<NumberItem>)filtered!);
        Assert.That(values, Is.EqualTo(new[] { 3, 4 }));
        Assert.That(counter.Count, Is.EqualTo(4));
    }

    [Test]
    public async Task Take_over_async_source_stops_upstream_early()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var counter = new EnumerationCounter();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers(counter));

        RuntimeResult result = await engine.RunAsync("TAKE 2 FROM [items] INTO [first].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(counter.Count, Is.EqualTo(0));
        Assert.That(result.State.TryGetVariable("first", out object? first), Is.True);
        List<int> values = await Values((IAsyncEnumerable<NumberItem>)first!);
        Assert.That(values, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(counter.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Sort_is_an_intentional_materialization_boundary_for_async_sources()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var counter = new EnumerationCounter();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers(counter, 4, 1, 3, 2));

        RuntimeResult result = await engine.RunAsync("SORT [items] BY Value INTO [sorted].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(counter.Count, Is.EqualTo(4));
        Assert.That(result.State.TryGetVariable("sorted", out object? sorted), Is.True);
        Assert.That(((NumberItem[])sorted!).Select(x => x.Value), Is.EqualTo(new[] { 1, 2, 3, 4 }));
    }

    [Test]
    public async Task For_each_streams_async_enumerable_without_exposing_iterator()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", Numbers(new EnumerationCounter()));

        RuntimeResult result = await engine.RunAsync("""
            FOR EACH [item] IN [items], DO
                CHECK IF [item.Value] > 0.
            END FOR.
            """, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.Result, Is.Null);
        Assert.That(result.State.TryGetVariable("item", out _), Is.False);
    }

    private static async Task<List<int>> Values(IAsyncEnumerable<NumberItem> source)
    {
        var result = new List<int>();
        await foreach (NumberItem item in source) result.Add(item.Value);
        return result;
    }

    private static async IAsyncEnumerable<NumberItem> Numbers(EnumerationCounter counter, params int[] values)
    {
        int[] source = values.Length == 0 ? new[] { 1, 2, 3, 4 } : values;
        foreach (int value in source)
        {
            await Task.Yield();
            counter.Count++;
            yield return new NumberItem(value);
        }
    }

    private sealed class EnumerationCounter { public int Count { get; set; } }
    private sealed record NumberItem(int Value);
}
