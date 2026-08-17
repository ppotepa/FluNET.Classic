using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Http;

public enum HttpMethodKind { GET, POST, PUT, PATCH, DELETE, HEAD }
public sealed record HttpBody(byte[] Data, string? ContentType = null)
{
    public static HttpBody Text(string text, string contentType = "text/plain") => new(Encoding.UTF8.GetBytes(text), contentType);
}
public sealed record HttpRequest(HttpMethodKind Method, HttpEndpoint Endpoint, HttpHeaders? Headers = null, HttpBody? Body = null);
public sealed record ETag(string Value) { public override string ToString() => Value; }

[Verb("SEND"), Qualifier("RESPONSE"), RequiresCapability(StandardCapabilities.NetworkHttp), ExecutionTrait(ExecutionTrait.Retryable)]
public sealed class SendHttpRequest : IVerb<HttpResponse>, ISend, IWhat<HttpRequest>, IPipelineConsumer<HttpRequest>, IPipelineProducer<HttpResponse>
{
    private readonly HttpRequest _request; private readonly HttpClient _client; public SendHttpRequest([What] HttpRequest request, [FromServices] HttpClient client) { _request = request; _client = client; }
    public async ValueTask<HttpResponse> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(new HttpMethod(_request.Method.ToString()), _request.Endpoint.Uri);
        if (_request.Body is not null) { message.Content = new ByteArrayContent(_request.Body.Data); if (!string.IsNullOrWhiteSpace(_request.Body.ContentType)) message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(_request.Body.ContentType); }
        if (_request.Headers is not null) foreach ((string name, string[] values) in _request.Headers.Values) { if (!message.Headers.TryAddWithoutValidation(name, values)) message.Content?.Headers.TryAddWithoutValidation(name, values); }
        using HttpResponseMessage response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false); return await HttpResponseFactory.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }
}

[Verb("POST"), Qualifier("RESPONSE"), RequiresCapability(StandardCapabilities.NetworkHttp), ExecutionTrait(ExecutionTrait.Retryable)]
public sealed class PostJsonResponse : IVerb<HttpResponse>, IPost, IWhat<JsonNode>, ITo<HttpEndpoint>, IPipelineProducer<HttpResponse>
{
    private readonly JsonNode _body; private readonly HttpEndpoint _endpoint; private readonly HttpClient _client; public PostJsonResponse([What] JsonNode body, [To] HttpEndpoint endpoint, [FromServices] HttpClient client) { _body = body; _endpoint = endpoint; _client = client; }
    public async ValueTask<HttpResponse> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) { using var content = new StringContent(_body.ToJsonString(), Encoding.UTF8, "application/json"); using HttpResponseMessage response = await _client.PostAsync(_endpoint.Uri, content, cancellationToken).ConfigureAwait(false); return await HttpResponseFactory.CreateAsync(response, cancellationToken).ConfigureAwait(false); }
}

[Verb("GET"), Qualifier("TEXT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHttpHeaderText : IVerb<string?>, IGet, IFrom<HttpHeaders>, IAt<string>, IPipelineProducer<string?>
{
    private readonly HttpHeaders _headers; private readonly string _name; public GetHttpHeaderText([From] HttpHeaders headers, [At] string name) { _headers = headers; _name = name; }
    public ValueTask<string?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_headers.TryGet(_name, out IReadOnlyList<string> values) ? values.FirstOrDefault() : null);
}

internal static class HttpResponseFactory
{
    public static async ValueTask<HttpResponse> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false); var values = response.Headers.Concat(response.Content.Headers).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.SelectMany(v => v.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
        return new(new HttpStatus((int)response.StatusCode, response.ReasonPhrase), new HttpHeaders(values), body, response.Content.Headers.ContentType?.MediaType);
    }
}
