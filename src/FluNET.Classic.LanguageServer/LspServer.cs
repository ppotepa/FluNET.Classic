using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluNET.Classic.Tooling;

namespace FluNET.Classic.LanguageServer;

public sealed class LspServer
{
    private readonly ClassicDocumentService _documents;
    private readonly Stream _input;
    private readonly Stream _output;
    private readonly Dictionary<string, string> _open = new(StringComparer.Ordinal);
    private bool _shutdown;

    public LspServer(ClassicDocumentService documents, Stream input, Stream output) { _documents = documents; _input = input; _output = output; }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            JsonObject? request = await ReadMessageAsync(cancellationToken).ConfigureAwait(false); if (request is null) break;
            string? method = request["method"]?.GetValue<string>(); JsonNode? id = request["id"]?.DeepClone(); JsonObject? parameters = request["params"] as JsonObject;
            try
            {
                switch (method)
                {
                    case "initialize": await ReplyAsync(id, new { capabilities = new { textDocumentSync = 1, completionProvider = new { resolveProvider = false }, hoverProvider = true, documentFormattingProvider = true }, serverInfo = new { name = "FluNET.Classic", version = "0.2" } }, cancellationToken).ConfigureAwait(false); break;
                    case "initialized": break;
                    case "shutdown": _shutdown = true; await ReplyAsync(id, null, cancellationToken).ConfigureAwait(false); break;
                    case "exit": return;
                    case "textDocument/didOpen": await DidOpen(parameters, cancellationToken).ConfigureAwait(false); break;
                    case "textDocument/didChange": await DidChange(parameters, cancellationToken).ConfigureAwait(false); break;
                    case "textDocument/didClose": await DidClose(parameters, cancellationToken).ConfigureAwait(false); break;
                    case "textDocument/completion": await Completion(id, parameters, cancellationToken).ConfigureAwait(false); break;
                    case "textDocument/hover": await Hover(id, parameters, cancellationToken).ConfigureAwait(false); break;
                    case "textDocument/formatting": await Formatting(id, parameters, cancellationToken).ConfigureAwait(false); break;
                    default: if (id is not null) await ErrorAsync(id, -32601, $"Method '{method}' is not supported.", cancellationToken).ConfigureAwait(false); break;
                }
            }
            catch (Exception ex) { if (id is not null) await ErrorAsync(id, -32603, ex.Message, cancellationToken).ConfigureAwait(false); }
            if (_shutdown && method == "exit") return;
        }
    }

    private async Task DidOpen(JsonObject? parameters, CancellationToken ct)
    {
        JsonObject doc = RequiredObject(parameters, "textDocument"); string uri = RequiredString(doc, "uri"); string text = RequiredString(doc, "text"); _open[uri] = text; await PublishDiagnostics(uri, text, ct).ConfigureAwait(false);
    }

    private async Task DidChange(JsonObject? parameters, CancellationToken ct)
    {
        JsonObject doc = RequiredObject(parameters, "textDocument"); string uri = RequiredString(doc, "uri"); JsonArray changes = parameters?["contentChanges"] as JsonArray ?? throw new InvalidOperationException("contentChanges is required."); string text = changes.LastOrDefault()?["text"]?.GetValue<string>() ?? string.Empty; _open[uri] = text; await PublishDiagnostics(uri, text, ct).ConfigureAwait(false);
    }

    private async Task DidClose(JsonObject? parameters, CancellationToken ct)
    {
        string uri = RequiredString(RequiredObject(parameters, "textDocument"), "uri"); _open.Remove(uri); await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics = Array.Empty<object>() }, ct).ConfigureAwait(false);
    }

    private async Task Completion(JsonNode? id, JsonObject? parameters, CancellationToken ct)
    {
        (string text, int offset) = ResolvePosition(parameters); var items = _documents.Complete(text, offset).Select(x => new { label = x.Label, kind = CompletionKind(x.Kind), detail = x.Detail }).ToArray(); await ReplyAsync(id, new { isIncomplete = false, items }, ct).ConfigureAwait(false);
    }

    private async Task Hover(JsonNode? id, JsonObject? parameters, CancellationToken ct)
    {
        (string text, int offset) = ResolvePosition(parameters); var hover = _documents.Hover(text, offset); object? result = hover is null ? null : new { contents = new { kind = "markdown", value = $"**{hover.Label}**\n\n{hover.Detail}" } }; await ReplyAsync(id, result, ct).ConfigureAwait(false);
    }

    private async Task Formatting(JsonNode? id, JsonObject? parameters, CancellationToken ct)
    {
        string uri = RequiredString(RequiredObject(parameters, "textDocument"), "uri"); string text = GetDocument(uri); string formatted = _documents.Format(text); var end = PositionAtEnd(text); await ReplyAsync(id, new[] { new { range = new { start = new { line = 0, character = 0 }, end }, newText = formatted } }, ct).ConfigureAwait(false);
    }

    private async Task PublishDiagnostics(string uri, string text, CancellationToken ct)
    {
        DocumentAnalysis analysis = _documents.Analyze(text); object[] diagnostics = analysis.Diagnostics.Select(x => new { range = Range(text, x.Span.Start, x.Span.Length), severity = x.Source == "syntax" ? 1 : 2, code = x.Code, source = $"flunet-{x.Source}", message = x.Message }).Cast<object>().ToArray(); await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics }, ct).ConfigureAwait(false);
    }

    private (string Text, int Offset) ResolvePosition(JsonObject? parameters)
    {
        string uri = RequiredString(RequiredObject(parameters, "textDocument"), "uri"); JsonObject position = RequiredObject(parameters, "position"); string text = GetDocument(uri); return (text, OffsetAt(text, position["line"]?.GetValue<int>() ?? 0, position["character"]?.GetValue<int>() ?? 0));
    }

    private string GetDocument(string uri) => _open.TryGetValue(uri, out string? text) ? text : throw new KeyNotFoundException($"Document '{uri}' is not open.");

    private async Task<JsonObject?> ReadMessageAsync(CancellationToken ct)
    {
        int? length = null;
        while (true)
        {
            string? line = await ReadHeaderLineAsync(ct).ConfigureAwait(false); if (line is null) return null; if (line.Length == 0) break;
            int colon = line.IndexOf(':'); if (colon <= 0) continue; if (line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase) && int.TryParse(line[(colon + 1)..].Trim(), out int parsed)) length = parsed;
        }
        if (length is null || length < 0) throw new InvalidDataException("Missing Content-Length header."); byte[] body = new byte[length.Value]; int read = 0; while (read < body.Length) { int count = await _input.ReadAsync(body.AsMemory(read), ct).ConfigureAwait(false); if (count == 0) return null; read += count; }
        return JsonNode.Parse(body) as JsonObject;
    }

    private async Task<string?> ReadHeaderLineAsync(CancellationToken ct)
    {
        var bytes = new List<byte>(); byte[] one = new byte[1];
        while (true)
        {
            int read = await _input.ReadAsync(one, ct).ConfigureAwait(false); if (read == 0) return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray()); byte value = one[0];
            if (value == (byte)'\n') { if (bytes.Count > 0 && bytes[^1] == (byte)'\r') bytes.RemoveAt(bytes.Count - 1); return Encoding.ASCII.GetString(bytes.ToArray()); } bytes.Add(value);
        }
    }

    private Task ReplyAsync(JsonNode? id, object? result, CancellationToken ct) => WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = JsonSerializer.SerializeToNode(result) }, ct);
    private Task ErrorAsync(JsonNode id, int code, string message, CancellationToken ct) => WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["error"] = JsonSerializer.SerializeToNode(new { code, message }) }, ct);
    private Task NotifyAsync(string method, object parameters, CancellationToken ct) => WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method, ["params"] = JsonSerializer.SerializeToNode(parameters) }, ct);

    private async Task WriteAsync(JsonObject message, CancellationToken ct)
    {
        byte[] body = Encoding.UTF8.GetBytes(message.ToJsonString()); byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"); await _output.WriteAsync(header, ct).ConfigureAwait(false); await _output.WriteAsync(body, ct).ConfigureAwait(false); await _output.FlushAsync(ct).ConfigureAwait(false);
    }

    private static JsonObject RequiredObject(JsonObject? source, string property) => source?[property] as JsonObject ?? throw new InvalidOperationException($"{property} is required.");
    private static string RequiredString(JsonObject source, string property) => source[property]?.GetValue<string>() ?? throw new InvalidOperationException($"{property} is required.");
    private static int CompletionKind(string kind) => kind switch { "variable" => 6, "qualifier" => 12, "verb" => 3, "role" or "syntax" => 14, _ => 1 };

    private static object Range(string text, int start, int length) => new { start = Position(text, start), end = Position(text, start + length) };
    private static object Position(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length); int line = 0, column = 0; for (int i = 0; i < offset; i++) { if (text[i] == '\n') { line++; column = 0; } else if (text[i] != '\r') column++; } return new { line, character = column };
    }
    private static object PositionAtEnd(string text) => Position(text, text.Length);
    private static int OffsetAt(string text, int targetLine, int targetCharacter)
    {
        int line = 0, character = 0; for (int i = 0; i < text.Length; i++) { if (line == targetLine && character >= targetCharacter) return i; if (text[i] == '\n') { if (line == targetLine) return i; line++; character = 0; } else if (text[i] != '\r') character++; } return text.Length;
    }
}
