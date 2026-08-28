using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class IntrinsicFormattingTests
{
    [Test]
    public void Formatter_uses_custom_intrinsic_syntax_metadata_without_name_specific_logic()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("top 3 from [items] into [result].");

        Assert.That(formatted, Is.EqualTo("TOP 3 FROM [items] INTO [result]."));
    }

    [Test]
    public async Task Different_intrinsic_surface_executes_the_declared_Take_semantics()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("items", new[] { 1, 2, 3, 4 });

        RuntimeResult result = await engine.RunAsync("TOP 2 FROM [items] INTO [top].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["top"], Is.TypeOf<int[]>());
        Assert.That((int[])result.State.Variables["top"]!, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Bound_collection_retains_intrinsic_descriptor_and_semantic_identity()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check(
            "TOP 2 FROM [items] INTO [top].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["items"] = typeof(int[]) });

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound!.Diagnostics.Select(x => x.Message)));
        var collection = (FluNET.Classic.Binding.BoundCollection)((FluNET.Classic.Binding.BoundPipeline)check.Bound!.Statements.Single()).Stages.Single();
        Assert.That(collection.Descriptor?.StableId, Is.EqualTo("intrinsic:test:top"));
        Assert.That(collection.Semantic, Is.EqualTo(FluNET.Classic.Core.IntrinsicSemanticKind.Take));
    }
}
