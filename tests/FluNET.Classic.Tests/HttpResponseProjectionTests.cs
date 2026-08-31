using FluNET.Classic.Core;
using FluNET.Classic.Standard.Http;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class HttpResponseProjectionTests
{
    [Test]
    public async Task Body_projection_returns_the_raw_response_body()
    {
        byte[] body = [0, 1, 2, 255];
        var response = new HttpResponse(
            new HttpStatus(200, "OK"),
            new HttpHeaders(new Dictionary<string, string[]>()),
            body,
            "application/octet-stream");

        byte[] result = await new GetHttpBody(response).ExecuteAsync(new VerbExecutionContext(null, new Dictionary<string, object?>(), null));

        Assert.That(result, Is.EqualTo(body));
    }

    [Test]
    public async Task Status_and_content_type_projections_return_typed_fields()
    {
        var response = new HttpResponse(
            new HttpStatus(201, "Created"),
            new HttpHeaders(new Dictionary<string, string[]>()),
            Array.Empty<byte>(),
            "application/json");
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetHttpStatusCode(response.Status).ExecuteAsync(context), Is.EqualTo(201));
        Assert.That(await new GetHttpStatusReason(response.Status).ExecuteAsync(context), Is.EqualTo("Created"));
        Assert.That(await new GetHttpContentType(response).ExecuteAsync(context), Is.EqualTo("application/json"));
    }
}
