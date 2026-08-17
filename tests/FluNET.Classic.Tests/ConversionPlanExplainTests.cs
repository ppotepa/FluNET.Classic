using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ConversionPlanExplainTests
{
    [Test]
    public void Planner_exposes_exact_multistep_conversion_selected_by_binder()
    {
        var options = new FluNetOptions
        {
            ConfigureConverters = registry =>
            {
                registry.Register(new SourceToMiddle());
                registry.Register(new MiddleToString());
            }
        };
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan(
            "SAY [value].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["value"] = typeof(SourceValue) });

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        ExecutionPlanStep say = plan.Steps.SelectMany(x => x.Children).Single(x => x.Verb == "SAY");
        ExecutionPlanValue value = say.Roles.Single(x => x.Name == "WHAT").Values.Single();
        Assert.That(value.Kind, Is.EqualTo("conversion"));
        Assert.That(value.ConversionSteps, Has.Count.EqualTo(2));
        Assert.That(value.ConversionSteps![0].SourceType, Does.Contain(nameof(SourceValue)));
        Assert.That(value.ConversionSteps[0].TargetType, Does.Contain(nameof(MiddleValue)));
        Assert.That(value.ConversionSteps[1].SourceType, Does.Contain(nameof(MiddleValue)));
        Assert.That(value.ConversionSteps[1].TargetType, Does.Contain(nameof(String)));
        Assert.That(value.ConversionSteps.Sum(x => x.Cost), Is.EqualTo(value.Cost));
    }

    private sealed record SourceValue(string Value);
    private sealed record MiddleValue(string Value);

    private sealed class SourceToMiddle : ValueConverter<SourceValue, MiddleValue>
    {
        public override bool TryConvert(SourceValue value, out MiddleValue? result)
        {
            result = new MiddleValue(value.Value);
            return true;
        }
    }

    private sealed class MiddleToString : ValueConverter<MiddleValue, string>
    {
        public override bool TryConvert(MiddleValue value, out string? result)
        {
            result = value.Value;
            return true;
        }
    }
}
