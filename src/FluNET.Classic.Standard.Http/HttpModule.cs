using System.Text;
using System.Text.Json.Nodes;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Http;

public enum HttpJsonRepresentation { JSON }

public sealed record HttpEndpoint(Uri Uri)
{
    public HttpEndpoint(string value) : this(new Uri(value, UriKind.RelativeOrAbsolute)) { }
    public override string ToString() => Uri.ToString();
}

public sealed record HttpStatus(int Code, string? ReasonPhrase) : IOkState
{
    public bool IsOk => Code is >= 200 and <= 299;
    public override string ToString() => ReasonPhrase is { Length: > 0 } ? $"{Code} {ReasonPhrase}" : Code.ToString();
}

public sealed record HttpHeaders(IReadOnlyDictionary<string, string[]> Values)
{
    public bool TryGet(string name, out IReadOnlyList<string> values)
    {
        if (Values.TryGetValue(name, out string[]? found)) { values = found; return true; }
        values = Array.Empty<string>();
        return false;
    }
}

public sealed record HttpResponse(HttpStatus Status, HttpHeaders Headers, byte[] Body, string? ContentType) : IOkState
{
    public bool IsOk => Status.IsOk;
    public string Text => Encoding.UTF8.GetString(Body);
}

public interface IEmailSender
{
    ValueTask SendAsync(string to, string message, CancellationToken cancellationToken = default);
}

public sealed class HttpModule : LanguageModule
{
    public override string Name => "http";
    public override IReadOnlyCollection<string> Dependencies => new[] { "json" };
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:http-request", "REQUEST", typeof(HttpRequest)),
        new("qualifier:http-response", "RESPONSE", typeof(HttpResponse)),
        new("qualifier:http-status", "STATUS", typeof(HttpStatus)),
        new("qualifier:http-headers", "HEADERS", typeof(HttpHeaders)),
        new("qualifier:http-etag", "ETAG", typeof(ETag))
    };
}

[Qualifier("JSON")]
public sealed class GetJsonHttp : Get<JsonNode, Uri>, IAs<HttpJsonRepresentation>
{
    private readonly HttpClient _client;
    public GetJsonHttp([What] JsonNode what, [From, RoleAlias("AT")] Uri from, [As] HttpJsonRepresentation @as = HttpJsonRepresentation.JSON, [FromServices] HttpClient client = null!) : base(what, from) => _client = client;
    protected override async ValueTask<JsonNode> ActAsync(Uri from, CancellationToken cancellationToken)
    {
        string text = await _client.GetStringAsync(from, cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(text) ?? new JsonObject();
    }
}

[Qualifier("RESPONSE")]
[RequiresCapability(StandardCapabilities.Network)]
public sealed class GetHttpResponse : Get<HttpResponse, HttpEndpoint>
{
    private readonly HttpClient _client;
    public GetHttpResponse([What] HttpResponse what, [From, RoleAlias("AT")] HttpEndpoint from, [FromServices] HttpClient client = null!) : base(what, from) => _client = client;
    protected override async ValueTask<HttpResponse> ActAsync(HttpEndpoint from, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _client.GetAsync(from.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var values = response.Headers.Concat(response.Content.Headers)
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.SelectMany(v => v.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
        return new(
            new HttpStatus((int)response.StatusCode, response.ReasonPhrase),
            new HttpHeaders(values),
            body,
            response.Content.Headers.ContentType?.MediaType);
    }
}

[Qualifier("STATUS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpStatus : Get<HttpStatus, HttpResponse>
{
    public GetHttpStatus([What] HttpStatus what, [From] HttpResponse from) : base(what, from) { }
    protected override ValueTask<HttpStatus> ActAsync(HttpResponse from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Status);
}

[Qualifier("HEADERS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpHeaders : Get<HttpHeaders, HttpResponse>
{
    public GetHttpHeaders([What] HttpHeaders what, [From] HttpResponse from) : base(what, from) { }
    protected override ValueTask<HttpHeaders> ActAsync(HttpResponse from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Headers);
}

[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpText : Get<string, HttpResponse>
{
    public GetHttpText([What] string what, [From] HttpResponse from) : base(what, from) { }
    protected override ValueTask<string> ActAsync(HttpResponse from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Text);
}

[Qualifier("JSON")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpJson : Get<JsonNode, HttpResponse>
{
    public GetHttpJson([What] JsonNode what, [From] HttpResponse from) : base(what, from) { }
    protected override ValueTask<JsonNode> ActAsync(HttpResponse from, CancellationToken cancellationToken) =>
        ValueTask.FromResult(JsonNode.Parse(from.Text) ?? new JsonObject());
}

[Verb("DOWNLOAD")]
[Qualifier("BINARY")]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class DownloadFile : IVerb<byte[]>, IDownload, IWhat<byte[]>, IFrom<Uri>, ITo<FileInfo>
{
    private readonly Uri _from;
    private readonly FileInfo? _to;
    private readonly HttpClient _client;
    public DownloadFile([What] byte[] what, [From] Uri from, [To] FileInfo? to = null, [FromServices] HttpClient client = null!) { _from = from; _to = to; _client = client; }
    public async ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        byte[] data = await _client.GetByteArrayAsync(_from, cancellationToken).ConfigureAwait(false);
        if (_to is not null) await File.WriteAllBytesAsync(_to.FullName, data, cancellationToken).ConfigureAwait(false);
        return data;
    }
}

[Verb("POST")]
[Qualifier("JSON")]
[RequiresCapability(StandardCapabilities.Network)]
public sealed class PostJson : IVerb<JsonNode>, IPost, IWhat<JsonNode>, ITo<Uri>
{
    private readonly JsonNode _body;
    private readonly Uri _to;
    private readonly HttpClient _client;
    public PostJson([What] JsonNode body, [To] Uri to, [FromServices] HttpClient client) { _body = body; _to = to; _client = client; }
    public async ValueTask<JsonNode> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(_body.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _client.PostAsync(_to, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(text) ?? new JsonObject { ["status"] = (int)response.StatusCode };
    }
}

[Verb("SEND")]
[Qualifier("TEXT")]
[RequiresCapability(StandardCapabilities.EmailSend)]
public sealed class SendEmail : IVerb<string>, ISend, IWhat<string>, ITo<string>
{
    private readonly string _message;
    private readonly string _to;
    private readonly IEmailSender _sender;
    public SendEmail([What] string message, [To] string to, [FromServices] IEmailSender sender) { _message = message; _to = to; _sender = sender; }
    public async ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _sender.SendAsync(_to, _message, cancellationToken).ConfigureAwait(false);
        return _message;
    }
}
