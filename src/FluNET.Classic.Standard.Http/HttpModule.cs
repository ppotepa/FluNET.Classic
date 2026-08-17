using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Http;

public interface IEmailSender
{
    ValueTask SendAsync(string to, string message, CancellationToken cancellationToken = default);
}

public sealed class HttpModule : LanguageModule
{
    public override string Name => "http";
    public override IReadOnlyCollection<string> Dependencies => new[] { "json" };
}

[Qualifier("JSON")]
public sealed class GetJsonHttp : Get<JsonNode, Uri>
{
    private readonly HttpClient _client;
    public GetJsonHttp([What] JsonNode what, [From] Uri from, [FromServices] HttpClient client) : base(what, from) => _client = client;
    protected override async ValueTask<JsonNode> ActAsync(Uri from, CancellationToken cancellationToken)
    {
        string text = await _client.GetStringAsync(from, cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(text) ?? new JsonObject();
    }
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
