using FluNET.Classic.Core;
using FluNET.Classic.Standard.Json;
using NUnit.Framework;
using System.Text.Json.Nodes;

namespace FluNET.Classic.Tests;

public sealed class JsonItemProjectionTests
{
    [Test]
    public async Task Json_property_and_item_projections_expose_typed_fields()
    {
        var property = new JsonProperty("name", JsonValue.Create("Alice"));
        var item = new JsonItem(3, JsonValue.Create(42));
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetJsonPropertyName(property).ExecuteAsync(context), Is.EqualTo("name"));
        Assert.That(await new GetJsonPropertyValue(property).ExecuteAsync(context), Is.EqualTo(property.Value));
        Assert.That(await new GetJsonItemIndex(item).ExecuteAsync(context), Is.EqualTo(3));
        Assert.That(await new GetJsonItemValue(item).ExecuteAsync(context), Is.EqualTo(item.Value));
    }
}
