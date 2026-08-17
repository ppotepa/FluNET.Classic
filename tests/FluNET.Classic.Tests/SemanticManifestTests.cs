using System.Text.Json;
using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.SDK;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class SemanticManifestTests
{
    [Test]
    public void Module_manifest_exposes_intrinsic_syntax_semantic_and_execution()
    {
        var options = new FluNetOptions();
        var module = new ProjectionFixtureModule();
        options.Modules.Add(module);
        using ServiceProvider host = FluNetHost.Create(options);
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();
        var generator = new ModuleArtifactGenerator();

        using JsonDocument json = JsonDocument.Parse(generator.GenerateManifest(snapshot, module));
        JsonElement top = json.RootElement.GetProperty("intrinsics").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "TOP");

        Assert.That(top.GetProperty("syntax").GetString(), Is.EqualTo("CollectionAmountFrom"));
        Assert.That(top.GetProperty("semantic").GetString(), Is.EqualTo("Take"));
        Assert.That(top.GetProperty("execution").GetString(), Is.EqualTo("Streaming"));
    }

    [Test]
    public void Global_introspection_exposes_operator_and_intrinsic_execution_contracts()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageIntrospectionService introspection = host.GetRequiredService<LanguageIntrospectionService>();

        using JsonDocument json = JsonDocument.Parse(introspection.ToJson());
        JsonElement sort = json.RootElement.GetProperty("intrinsics").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "SORT");
        JsonElement equality = json.RootElement.GetProperty("operators").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "=");

        Assert.That(sort.GetProperty("semantic").GetString(), Is.EqualTo("Sort"));
        Assert.That(sort.GetProperty("execution").GetString(), Is.EqualTo("Materializing"));
        Assert.That(sort.GetProperty("strategyType").GetString(), Does.Contain(nameof(CollectionSortDirection)));
        Assert.That(equality.GetProperty("compatibility").GetString(), Is.EqualTo("ComparablePair"));
        Assert.That(equality.GetProperty("evaluation").GetString(), Is.EqualTo("Equal"));
    }
}
