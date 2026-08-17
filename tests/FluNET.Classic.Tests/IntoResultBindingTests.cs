using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class IntoResultBindingTests
{
    [Test]
    public async Task Into_binds_whole_stage_result_independently_from_explicit_output_roles()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("PROJECTPAIR WITH [projected] INTO [whole].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["projected"], Is.EqualTo(42));
        Assert.That(result.State.Variables["whole"], Is.EqualTo(new PairResult("member-value", 42)));
    }

    [Test]
    public void Into_is_not_bound_as_an_output_role_in_the_bound_tree()
    {
        using ServiceProvider host = CreateHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("PROJECTPAIR WITH [projected] INTO [whole].");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound!.Diagnostics.Select(x => x.Message)));
        var sentence = (FluNET.Classic.Binding.BoundSentence)((FluNET.Classic.Binding.BoundPipeline)check.Bound!.Statements.Single()).Stages.Single();
        Assert.That(sentence.ResultAlias, Is.EqualTo("whole"));
        Assert.That(sentence.Roles.SelectMany(x => x.Values).OfType<FluNET.Classic.Binding.BoundVariableValue>().Any(x => x.Name == "whole" && x.IsOutput), Is.False);
    }

    private static ServiceProvider CreateHost()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        return FluNetHost.Create(options);
    }
}
