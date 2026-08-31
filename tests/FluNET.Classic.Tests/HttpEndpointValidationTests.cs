using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class HttpEndpointValidationTests
{
    [TestCase("relative/path")]
    [TestCase("ftp://example.com/file")]
    public void Http_operations_reject_non_http_absolute_endpoints(string endpoint)
    {
        using ServiceProvider host = FluNetHost.Create();
        CheckResult result = host.GetRequiredService<ClassicEngine>().Check($"GET RESPONSE FROM {{{endpoint}}} INTO [response].");

        Assert.That(result.Success, Is.False);
    }
}
