using System.Text.Json.Nodes;

namespace FluNET.Language;

public static class StandardQualifiers
{
    public static IReadOnlyList<QualifierDescriptor> All { get; } =
    [
        new("TEXT", typeof(string)),
        new("JSON", typeof(JsonNode)),
        new("BINARY", typeof(byte[])),
        new("FILE", typeof(FileInfo)),
        new("URI", typeof(Uri)),
        new("DATE", typeof(DateTime)),
        new("BOOLEAN", typeof(bool)),
        new("NUMBER", typeof(decimal)),
        new("XML"),
        new("CSV"),
        new("HTML", typeof(string)),
        new("YAML"),
        new("IMAGE", typeof(byte[])),
        new("VIDEO", typeof(byte[])),
        new("AUDIO", typeof(byte[]))
    ];
}
