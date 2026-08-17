using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class OutputProjectionTests
{
    [Test]
    public void Compiler_carries_member_and_index_projection_metadata()
    {
        using ServiceProvider host = CreateHost();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();

        SentencePattern memberPattern = language.GetVerb("PROJECTPAIR").Implementations.Single().Patterns.Single();
        Assert.That(memberPattern.Roles.Single(x => x.Name == "WHAT").OutputProjection, Is.EqualTo(OutputProjectionDescriptor.FromMember("First")));
        Assert.That(memberPattern.Roles.Single(x => x.Name == "WITH").OutputProjection, Is.EqualTo(OutputProjectionDescriptor.FromMember("Second")));

        SentencePattern tuplePattern = language.GetVerb("PROJECTTUPLE").Implementations.Single().Patterns.Single();
        Assert.That(tuplePattern.Roles.Single(x => x.Name == "WHAT").OutputProjection, Is.EqualTo(OutputProjectionDescriptor.FromIndex(0)));
        Assert.That(tuplePattern.Roles.Single(x => x.Name == "WITH").OutputProjection, Is.EqualTo(OutputProjectionDescriptor.FromIndex(1)));
    }

    [Test]
    public async Task Runtime_projects_member_without_using_variable_name_as_member_name()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("PROJECTPAIR WITH [completelyDifferentName].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["completelyDifferentName"], Is.EqualTo(42));
    }

    [Test]
    public async Task Runtime_projects_tuple_index_from_metadata()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("PROJECTTUPLE WITH [renamedNumber].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["renamedNumber"], Is.EqualTo(7));
    }

    [Test]
    public void Planner_exposes_projection_contract()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan("PROJECTPAIR WITH [renamed].");

        ExecutionPlanStep stage = plan.Steps.SelectMany(x => x.Children).Single(x => x.Verb == "PROJECTPAIR");
        ExecutionPlanRole output = stage.Roles.Single(x => x.Name == "WITH");
        Assert.That(output.Projection, Is.EqualTo("member:Second"));
    }

    private static ServiceProvider CreateHost()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        return FluNetHost.Create(options);
    }
}
