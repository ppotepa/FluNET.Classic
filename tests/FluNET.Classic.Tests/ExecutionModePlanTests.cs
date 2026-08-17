using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ExecutionModePlanTests
{
    [TestCase("TAKE 2 FROM [items] INTO [result].", "Streaming")]
    [TestCase("SKIP 2 FROM [items] INTO [result].", "Streaming")]
    [TestCase("DISTINCT [items] INTO [result].", "Streaming")]
    [TestCase("SORT [items] BY Value INTO [result].", "Materializing")]
    [TestCase("GROUP [items] BY Value INTO [result].", "Materializing")]
    [TestCase("COUNT [items] INTO [result].", "Scalar")]
    public void Planner_uses_intrinsic_execution_metadata(string source, string expectedMode)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan(source, new Dictionary<string, Type> { ["items"] = typeof(Item[]) });

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        ExecutionPlanStep collection = plan.Steps.Single().Children.Single();
        Assert.That(collection.ExecutionMode, Is.EqualTo(expectedMode));
    }

    private sealed record Item(int Value);
}
