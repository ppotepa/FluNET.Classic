using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class JsonProjectionTests
{
    [Test]
    public async Task Json_properties_are_typed_and_composable_with_collection_intrinsics()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        string source = """
            PARSE JSON FROM "{\"b\":2,\"a\":1}" INTO [data].
            LIST PROPERTIES FROM [data] INTO [properties],
            THEN SORT BY Name INTO [sorted].
            """;

        RuntimeResult result = await engine.RunAsync(source);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["sorted"], Is.TypeOf<JsonProperty[]>());
        Assert.That(((JsonProperty[])result.State.Variables["sorted"]!).Select(x => x.Name), Is.EqualTo(new[] { "a", "b" }));
    }
}
