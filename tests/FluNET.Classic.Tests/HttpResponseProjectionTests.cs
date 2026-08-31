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
}
