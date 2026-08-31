using FluNET.Classic.Binding;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class BindingResultImmutabilityTests
{
    [Test]
    public void Resolution_result_snapshots_candidates()
    {
        var candidates = new List<ResolutionCandidate> { new("test", 1, "value") };
        var result = new ResolutionResult(ResolutionStatus.Success, "value", "test", 1, candidates);

        candidates.Clear();

        Assert.That(result.Candidates, Has.Count.EqualTo(1));
        Assert.That(() => ((IList<ResolutionCandidate>)result.Candidates).Clear(), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Conversion_results_snapshot_plans()
    {
        var steps = new List<ConversionStep>();
        var plan = new ConversionPlan(typeof(string), typeof(object), steps, 1);
        var alternatives = new List<ConversionPlan> { plan };
        var result = new ConversionPlanningResult(ConversionPlanningStatus.Success, plan, alternatives);

        steps.Add(new ConversionStep(typeof(string), typeof(object), ConversionKind.Assignable, 1));
        alternatives.Clear();

        Assert.That(plan.Steps, Is.Empty);
        Assert.That(result.Alternatives, Has.Count.EqualTo(1));
    }
}
