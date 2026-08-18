using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class HttpRequestBuilderTests
{
    [Test]
    public async Task Request_can_be_created_without_performing_network_io()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("CREATE REQUEST FROM {https://example.com/api} USING GET INTO [request].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["request"], Is.TypeOf<HttpRequest>());
        HttpRequest request = (HttpRequest)result.State.Variables["request"]!;
        Assert.That(request.Method, Is.EqualTo(HttpMethodKind.GET));
        Assert.That(request.Endpoint.Uri.ToString(), Is.EqualTo("https://example.com/api"));
    }
}
