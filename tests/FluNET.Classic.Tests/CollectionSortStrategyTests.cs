using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class CollectionSortStrategyTests
{
    [Test]
    public async Task Sort_executes_descending_strategy_as_typed_value()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { new Item("beta"), new Item("alpha"), new Item("gamma") });

        RuntimeResult result = await engine.RunAsync("SORT [items] BY Name USING DESCENDING INTO [sorted].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("sorted", out object? sorted), Is.True);
        Assert.That(sorted, Is.TypeOf<Item[]>());
        Assert.That(((Item[])sorted!).Select(x => x.Name), Is.EqualTo(new[] { "gamma", "beta", "alpha" }));
    }

    [Test]
    public async Task Sort_defaults_to_ascending_when_strategy_is_omitted()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { new Item("beta"), new Item("alpha") });

        RuntimeResult result = await engine.RunAsync("SORT [items] BY Name INTO [sorted].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(((Item[])result.State.Variables["sorted"]!).Select(x => x.Name), Is.EqualTo(new[] { "alpha", "beta" }));
    }

    [Test]
    public void Formatter_preserves_typed_sort_strategy()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("sort [items] by Name using descending into [sorted]");

        Assert.That(formatted, Is.EqualTo("SORT [items] BY Name USING DESCENDING INTO [sorted]."));
    }

    [Test]
    public void Planner_exposes_using_role_and_strategy_type()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan(
            "SORT [items] BY Name USING DESCENDING INTO [sorted].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["items"] = typeof(Item[]) });

        ExecutionPlanStep sort = plan.Steps.SelectMany(x => x.Children).Single(x => x.Verb == "SORT");
        ExecutionPlanRole usingRole = sort.Roles.Single(x => x.Name == "USING");
        Assert.That(usingRole.ValueType, Does.Contain(nameof(CollectionSortDirection)));
        Assert.That(usingRole.Values.Single().Detail, Is.EqualTo("DESCENDING"));
    }

    [Test]
    public void Invalid_sort_strategy_is_rejected_by_binder()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check(
            "SORT [items] BY Name USING SIDEWAYS INTO [sorted].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["items"] = typeof(Item[]) });

        Assert.That(check.Success, Is.False);
        Assert.That(check.Bound!.Diagnostics.Any(x => x.Code == "FLU-BIND-165"), Is.True);
    }

    private sealed record Item(string Name);
}
