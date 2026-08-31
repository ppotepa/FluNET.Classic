using FluNET.Classic.Core;
using System.Net;
using System.Net.Sockets;

namespace FluNET.Classic.Network;

public sealed record DnsName(string Value)
{
    public override string ToString() => Value;
}
public sealed record NetworkPort(int Value)
{
    public NetworkPort(string value) : this(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)) { }
}
public sealed record NetworkEndpoint(DnsName Host, NetworkPort Port);
public sealed record ConnectivityResult(NetworkEndpoint Endpoint, bool Connected, TimeSpan Duration) : IOkState
{
    public bool IsOk => Connected;
}

public sealed class NetworkModule : LanguageModule
{
    public override string Name => "network";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[] { new("qualifier:addresses", "ADDRESSES", typeof(IPAddress[])), new("qualifier:connectivity", "CONNECTIVITY", typeof(ConnectivityResult)) };
}

[Verb("GET")]
[Qualifier("ADDRESSES")]
[RequiresCapability(StandardCapabilities.NetworkDns)]
[ExecutionTrait(ExecutionTrait.Retryable)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class ResolveDns : IVerb<IPAddress[]>, IGet, IFrom<DnsName>, IPipelineProducer<IPAddress[]>
{
    private readonly DnsName _name; public ResolveDns([From] DnsName name) => _name = name;
    public async ValueTask<IPAddress[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => await Dns.GetHostAddressesAsync(_name.Value, cancellationToken).ConfigureAwait(false);
}

[Verb("GET")]
[Qualifier("CONNECTIVITY")]
[RequiresCapability(StandardCapabilities.NetworkConnect)]
[ExecutionTrait(ExecutionTrait.Retryable)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class GetConnectivity : IVerb<ConnectivityResult>, IGet, IFrom<NetworkEndpoint>, IPipelineProducer<ConnectivityResult>
{
    private readonly NetworkEndpoint _endpoint; public GetConnectivity([From] NetworkEndpoint endpoint) => _endpoint = endpoint;
    public async ValueTask<ConnectivityResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        using var client = new TcpClient();
        bool connected;
        try
        {
            await client.ConnectAsync(_endpoint.Host.Value, _endpoint.Port.Value, cancellationToken).ConfigureAwait(false);
            connected = true;
        }
        catch (SocketException) { connected = false; }
        started.Stop();
        return new(_endpoint, connected, started.Elapsed);
    }
}
