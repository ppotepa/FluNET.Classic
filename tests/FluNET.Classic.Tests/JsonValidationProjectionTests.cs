using FluNET.Classic.Core;
using FluNET.Classic.Standard.Json;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class JsonValidationProjectionTests
{
    [Test]
    public async Task Validation_errors_are_available_as_a_typed_projection()
    {
        var validation = new JsonValidationResult(false, new[] { "Missing property 'name'." });
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        string[] errors = await new GetJsonValidationErrors(validation).ExecuteAsync(context);

        Assert.That(errors, Is.EqualTo(new[] { "Missing property 'name'." }));
    }
}
