using FluNET.Classic.Network;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class NetworkProjectionTests
{
    [Test]
    public async Task Connectivity_and_endpoint_projections_return_typed_fields()
    {
        var endpoint = new NetworkEndpoint(new DnsName("example.test"), new NetworkPort(443));
        var result = new ConnectivityResult(endpoint, true, TimeSpan.FromMilliseconds(12));

        Assert.That(await new GetConnectivityState(result).ExecuteAsync(null!), Is.True);
        Assert.That(await new GetConnectivityDuration(result).ExecuteAsync(null!), Is.EqualTo(result.Duration));
        Assert.That(await new GetConnectivityEndpoint(result).ExecuteAsync(null!), Is.EqualTo(endpoint));
        Assert.That(await new GetEndpointHost(endpoint).ExecuteAsync(null!), Is.EqualTo(endpoint.Host));
        Assert.That(await new GetEndpointPort(endpoint).ExecuteAsync(null!), Is.EqualTo(endpoint.Port));
    }
}
