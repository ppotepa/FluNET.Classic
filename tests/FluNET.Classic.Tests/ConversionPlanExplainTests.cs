using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Text;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ConversionPlanExplainTests
{
    [Test]
    public void Planner_exposes_exact_multistep_conversion_selected_by_binder()
    {
        var options = Options();
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

    [Test]
    public async Task Runtime_executes_the_conversion_plan_selected_at_bind_time()
    {
        var writer = new CaptureOutputWriter();
        using ServiceProvider host = FluNetHost.Create(Options(), services => services.AddSingleton<IOutputWriter>(writer));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        CheckResult check = engine.Check(
            "SAY [value].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["value"] = typeof(SourceValue) });
        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound!.Diagnostics.Select(x => x.Message)));

        ValueConversionRegistry registry = host.GetRequiredService<ValueConversionRegistry>();
        registry.Register(new LateDirectConversion(), priority: 100);

        var state = new RuntimeState();
        state.SetVariable("value", new SourceValue("bound-path"));
        BoundExecutor executor = host.GetRequiredService<BoundExecutor>();
        RuntimeResult result = await executor.ExecuteAsync(check.Bound!, state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(writer.Lines, Is.EqualTo(new[] { "bound-path" }));
    }

    private static FluNetOptions Options() => new()
    {
        ConfigureConverters = registry =>
        {
            registry.Register(new SourceToMiddle());
            registry.Register(new MiddleToString());
        }
    };

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

    private sealed class LateDirectConversion : ValueConverter<SourceValue, string>
    {
        public override bool TryConvert(SourceValue value, out string? result)
        {
            result = "late-direct";
            return true;
        }
    }

    private sealed class CaptureOutputWriter : IOutputWriter
    {
        public List<string> Lines { get; } = [];
        public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default)
        {
            Lines.Add(text);
            return ValueTask.CompletedTask;
        }
    }
}
