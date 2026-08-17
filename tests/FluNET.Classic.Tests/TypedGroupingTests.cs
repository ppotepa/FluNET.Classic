using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class TypedGroupingTests
{
    [Test]
    public async Task Group_preserves_key_and_element_types()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { new Item(2, "b"), new Item(1, "a"), new Item(2, "c") });

        ExecutionPlan plan = engine.Plan("GROUP [items] BY Category INTO [groups].", new Dictionary<string, Type> { ["items"] = typeof(Item[]) });
        RuntimeResult result = await engine.RunAsync("GROUP [items] BY Category INTO [groups].", state);

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        Assert.That(plan.ResultType, Does.Contain("CollectionGroup`2"));
        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("groups", out object? groups), Is.True);
        Assert.That(groups, Is.TypeOf<CollectionGroup<int, Item>[]>());

        CollectionGroup<int, Item>[] typed = (CollectionGroup<int, Item>[])groups!;
        Assert.That(typed.Select(x => x.Key), Is.EqualTo(new[] { 2, 1 }));
        Assert.That(typed[0].Items.Select(x => x.Name), Is.EqualTo(new[] { "b", "c" }));
        Assert.That(typed[0].Count, Is.EqualTo(2));
    }

    private sealed record Item(int Category, string Name);
}
