using FluNET.Classic.Core;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace FluNET.Classic.Standard.Http;

public enum HttpMethodKind
{
    GET, POST, PUT, PATCH, DELETE, HEAD
}
public sealed record HttpBody(byte[] Data, string? ContentType = null)
{
    public static HttpBody Text(string text, string contentType = "text/plain") => new(Encoding.UTF8.GetBytes(text), contentType);
}
public sealed record HttpHeader(string Name, IReadOnlyList<string> Values)
{
    public HttpHeader(string name, string value) : this(name, new[] { value }) { }
}
public enum HttpConditionKind
{
    IF_MATCH, IF_NONE_MATCH
}
public sealed record ETag
{
    public string Value
    {
        get;
    }

    public ETag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("An ETag value cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;
}
public sealed record HttpCondition(HttpConditionKind Kind, ETag Tag);
public sealed record HttpRequest(HttpMethodKind Method, HttpEndpoint Endpoint, HttpHeaders? Headers = null, HttpBody? Body = null, HttpCondition? Condition = null);

[Verb("CREATE")]
[Qualifier("REQUEST")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class CreateHttpRequest : IVerb<HttpRequest>, ICreate, IFrom<HttpEndpoint>, IUsing<HttpMethodKind>, IWith<HttpBody>, IPipelineProducer<HttpRequest>
{
    private readonly HttpEndpoint _endpoint; private readonly HttpMethodKind _method; private readonly HttpBody? _body;
    public CreateHttpRequest([From] HttpEndpoint endpoint, [Using] HttpMethodKind method, [With] HttpBody? body = null)
    {
        _endpoint = endpoint;
        _method = method;
        _body = body;
    }
    public ValueTask<HttpRequest> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new HttpRequest(_method, _endpoint, Body: _body));
}

[Verb("TRANSFORM")]
[Qualifier("REQUEST")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class AddHttpHeader : IVerb<HttpRequest>, ITransform, IWhat<HttpRequest>, IWith<HttpHeader>, IPipelineConsumer<HttpRequest>, IPipelineProducer<HttpRequest>
{
    private readonly HttpRequest _request; private readonly HttpHeader _header;
    public AddHttpHeader([What] HttpRequest request, [With] HttpHeader header)
    {
        _request = request;
        _header = header;
    }
    public ValueTask<HttpRequest> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string[]>(_request.Headers?.Values ?? new Dictionary<string, string[]>(), StringComparer.OrdinalIgnoreCase)
        {
            [_header.Name] = _header.Values.ToArray()
        };
        return ValueTask.FromResult(_request with
        {
            Headers = new HttpHeaders(values)
        });
    }
}

[Verb("TRANSFORM")]
[Qualifier("REQUEST")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class AddHttpCondition : IVerb<HttpRequest>, ITransform, IWhat<HttpRequest>, IWith<HttpCondition>, IPipelineConsumer<HttpRequest>, IPipelineProducer<HttpRequest>
{
    private readonly HttpRequest _request; private readonly HttpCondition _condition;
    public AddHttpCondition([What] HttpRequest request, [With] HttpCondition condition)
    {
        _request = request;
        _condition = condition;
    }
    public ValueTask<HttpRequest> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_request with { Condition = _condition });
}

[Verb("GET")]
[Qualifier("METHOD")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpRequestMethod : Get<HttpMethodKind, HttpRequest>
{
    public GetHttpRequestMethod([From] HttpRequest from) : base(from) { }
    protected override ValueTask<HttpMethodKind> ActAsync(HttpRequest from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Method);
}

[Verb("GET")]
[Qualifier("ENDPOINT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpRequestEndpoint : Get<HttpEndpoint, HttpRequest>
{
    public GetHttpRequestEndpoint([From] HttpRequest from) : base(from) { }
    protected override ValueTask<HttpEndpoint> ActAsync(HttpRequest from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Endpoint);
}

[Verb("GET")]
[Qualifier("HEADERS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpRequestHeaders : Get<HttpHeaders?, HttpRequest>
{
    public GetHttpRequestHeaders([From] HttpRequest from) : base(from) { }
    protected override ValueTask<HttpHeaders?> ActAsync(HttpRequest from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Headers);
}

[Verb("GET")]
[Qualifier("BODY")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpRequestBody : Get<HttpBody?, HttpRequest>
{
    public GetHttpRequestBody([From] HttpRequest from) : base(from) { }
    protected override ValueTask<HttpBody?> ActAsync(HttpRequest from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Body);
}

[Verb("GET")]
[Qualifier("CONDITION")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpRequestCondition : Get<HttpCondition?, HttpRequest>
{
    public GetHttpRequestCondition([From] HttpRequest from) : base(from) { }
    protected override ValueTask<HttpCondition?> ActAsync(HttpRequest from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Condition);
}

[Verb("SEND")]
[Qualifier("RESPONSE")]
[RequiresCapability(StandardCapabilities.NetworkHttp)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class SendHttpRequest : IVerb<HttpResponse>, ISend, IWhat<HttpRequest>, IPipelineConsumer<HttpRequest>, IPipelineProducer<HttpResponse>
{
    private readonly HttpRequest _request; private readonly HttpClient _client; public SendHttpRequest([What] HttpRequest request, [FromServices] HttpClient client)
    {
        _request = request;
        _client = client;
    }
    public async ValueTask<HttpResponse> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(new HttpMethod(_request.Method.ToString()), _request.Endpoint.Uri);
        if (_request.Body is not null)
        {
            message.Content = new ByteArrayContent(_request.Body.Data);
            if (!string.IsNullOrWhiteSpace(_request.Body.ContentType))
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(_request.Body.ContentType);
        }
        if (_request.Headers is not null)
            foreach ((string name, string[] values) in _request.Headers.Values)
            {
                if (!message.Headers.TryAddWithoutValidation(name, values))
                    message.Content?.Headers.TryAddWithoutValidation(name, values);
            }
        if (_request.Condition is not null)
        {
            string header = _request.Condition.Kind == HttpConditionKind.IF_MATCH ? "If-Match" : "If-None-Match";
            message.Headers.TryAddWithoutValidation(header, _request.Condition.Tag.Value);
        }
        using HttpResponseMessage response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        return await HttpResponseFactory.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }
}

[Verb("POST")]
[Qualifier("RESPONSE")]
[RequiresCapability(StandardCapabilities.NetworkHttp)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class PostJsonResponse : IVerb<HttpResponse>, IPost, IWhat<JsonNode>, ITo<HttpEndpoint>, IPipelineProducer<HttpResponse>
{
    private readonly JsonNode _body; private readonly HttpEndpoint _endpoint; private readonly HttpClient _client; public PostJsonResponse([What] JsonNode body, [To] HttpEndpoint endpoint, [FromServices] HttpClient client)
    {
        _body = body;
        _endpoint = endpoint;
        _client = client;
    }
    public async ValueTask<HttpResponse> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(_body.ToJsonString(), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _client.PostAsync(_endpoint.Uri, content, cancellationToken).ConfigureAwait(false);
        return await HttpResponseFactory.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }
}

[Verb("GET")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpHeaderText : IVerb<string?>, IGet, IFrom<HttpHeaders>, IAt<string>, IPipelineProducer<string?>
{
    private readonly HttpHeaders _headers; private readonly string _name; public GetHttpHeaderText([From] HttpHeaders headers, [At] string name)
    {
        _headers = headers;
        _name = name;
    }
    public ValueTask<string?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_headers.TryGet(_name, out IReadOnlyList<string> values) ? values.FirstOrDefault() : null);
}

[Verb("GET")]
[Qualifier("ETAG")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpETag : IVerb<ETag?>, IGet, IFrom<HttpHeaders>, IPipelineProducer<ETag?>
{
    private readonly HttpHeaders _headers; public GetHttpETag([From] HttpHeaders headers) => _headers = headers;
    public ValueTask<ETag?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_headers.TryGet("ETag", out IReadOnlyList<string> values) && values.FirstOrDefault() is { } value ? new ETag(value) : null);
}

internal static class HttpResponseFactory
{
    public static async ValueTask<HttpResponse> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var values = response.Headers.Concat(response.Content.Headers).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.SelectMany(v => v.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
        return new(new HttpStatus((int)response.StatusCode, response.ReasonPhrase), new HttpHeaders(values), body, response.Content.Headers.ContentType?.MediaType);
    }
}
