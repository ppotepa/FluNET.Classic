using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluNET.Classic.Hosting;
using FluNET.Classic.LanguageServer;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class LanguageServerProtocolTests
{
    [Test]
    public async Task ServerUsesDocumentServiceForTheBasicEditorLifecycle()
    {
        const string uri = "file:///workspace/example.flu";
        const string source = "GET TEXT FROM {hello} INTO [text].";
        string input = string.Join("", new[]
        {
            Frame(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } }),
            Frame(new { jsonrpc = "2.0", method = "initialized", @params = new { } }),
            Frame(new { jsonrpc = "2.0", method = "textDocument/didOpen", @params = new { textDocument = new { uri, languageId = "flunet", version = 1, text = source } } }),
            Frame(new { jsonrpc = "2.0", id = 2, method = "textDocument/completion", @params = new { textDocument = new { uri }, position = new { line = 0, character = 4 } } }),
            Frame(new { jsonrpc = "2.0", id = 3, method = "shutdown", @params = (object?)null }),
            Frame(new { jsonrpc = "2.0", method = "exit", @params = (object?)null })
        });

        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService documents = host.GetRequiredService<ClassicDocumentService>();
        using var inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        using var outputStream = new MemoryStream();
        await new LspServer(documents, inputStream, outputStream).RunAsync();

        JsonObject[] messages = ReadFrames(outputStream.ToArray());
        JsonObject initialize = messages.Single(message => message["id"]?.GetValue<int>() == 1);
        JsonObject completion = messages.Single(message => message["id"]?.GetValue<int>() == 2);
        JsonObject shutdown = messages.Single(message => message["id"]?.GetValue<int>() == 3);

        Assert.That(initialize["result"]?["capabilities"]?["completionProvider"], Is.Not.Null);
        Assert.That(completion["result"]?["items"]?.AsArray().Any(item => item?["label"]?.GetValue<string>() == "TEXT"), Is.True);
        Assert.That(messages.Any(message => message["method"]?.GetValue<string>() == "textDocument/publishDiagnostics"), Is.True);
        Assert.That(shutdown["result"], Is.Null);
    }

    private static string Frame(object message)
    {
        string body = JsonSerializer.Serialize(message);
        return $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}";
    }

    private static JsonObject[] ReadFrames(byte[] bytes)
    {
        var messages = new List<JsonObject>();
        int offset = 0;
        while (offset < bytes.Length)
        {
            int separator = Array.IndexOf(bytes, (byte)'\r', offset);
            Assert.That(separator, Is.GreaterThanOrEqualTo(offset));
            string header = Encoding.ASCII.GetString(bytes, offset, separator - offset);
            int length = int.Parse(header["Content-Length: ".Length..]);
            offset = separator + 4;
            Assert.That(offset + length, Is.LessThanOrEqualTo(bytes.Length));
            messages.Add(JsonNode.Parse(Encoding.UTF8.GetString(bytes, offset, length))!.AsObject());
            offset += length;
        }
        return messages.ToArray();
    }
}
