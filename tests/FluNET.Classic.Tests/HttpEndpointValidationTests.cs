using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class HttpEndpointValidationTests
{
    [TestCase("ftp://example.com/file")]
    [TestCase("/relative/path")]
    public void Direct_uri_construction_rejects_non_http_endpoints(string endpoint)
    {
        Uri uri = new(endpoint, UriKind.RelativeOrAbsolute);

        Assert.That(() => new HttpEndpoint(uri), Throws.TypeOf<FormatException>());
    }

    [TestCase("relative/path")]
    [TestCase("ftp://example.com/file")]
    public void Http_operations_reject_non_http_absolute_endpoints(string endpoint)
    {
        using ServiceProvider host = FluNetHost.Create();
        CheckResult result = host.GetRequiredService<ClassicEngine>().Check($"GET RESPONSE FROM {{{endpoint}}} INTO [response].");

        Assert.That(result.Success, Is.False);
    }
}
