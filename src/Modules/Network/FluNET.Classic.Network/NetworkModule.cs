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
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:addresses", "ADDRESSES", typeof(IPAddress[])),
        new("qualifier:connectivity", "CONNECTIVITY", typeof(ConnectivityResult)),
        new("qualifier:connected", "CONNECTED", typeof(bool)),
        new("qualifier:connectivity-duration", "DURATION", typeof(TimeSpan)),
        new("qualifier:endpoint", "ENDPOINT", typeof(NetworkEndpoint)),
        new("qualifier:host", "HOST", typeof(DnsName)),
        new("qualifier:port", "PORT", typeof(NetworkPort))
    };
}

[Verb("GET")]
[Qualifier("CONNECTED")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetConnectivityState : Get<bool, ConnectivityResult>
{
    public GetConnectivityState([From] ConnectivityResult from) : base(from) { }

    protected override ValueTask<bool> ActAsync(ConnectivityResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Connected);
}

[Verb("GET")]
[Qualifier("DURATION")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetConnectivityDuration : Get<TimeSpan, ConnectivityResult>
{
    public GetConnectivityDuration([From] ConnectivityResult from) : base(from) { }

    protected override ValueTask<TimeSpan> ActAsync(ConnectivityResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Duration);
}

[Verb("GET")]
[Qualifier("ENDPOINT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetConnectivityEndpoint : Get<NetworkEndpoint, ConnectivityResult>
{
    public GetConnectivityEndpoint([From] ConnectivityResult from) : base(from) { }

    protected override ValueTask<NetworkEndpoint> ActAsync(ConnectivityResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Endpoint);
}

[Verb("GET")]
[Qualifier("HOST")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEndpointHost : Get<DnsName, NetworkEndpoint>
{
    public GetEndpointHost([From] NetworkEndpoint from) : base(from) { }

    protected override ValueTask<DnsName> ActAsync(NetworkEndpoint from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Host);
}

[Verb("GET")]
[Qualifier("PORT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEndpointPort : Get<NetworkPort, NetworkEndpoint>
{
    public GetEndpointPort([From] NetworkEndpoint from) : base(from) { }

    protected override ValueTask<NetworkPort> ActAsync(NetworkEndpoint from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Port);
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
