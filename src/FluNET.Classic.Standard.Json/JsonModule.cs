using System.Text.Json.Nodes;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Json;

public sealed class JsonModule : LanguageModule
{
    public override string Name => "json";
    public override IReadOnlyCollection<string> Dependencies => new[] { "files" };
}

[Qualifier("JSON")]
public sealed class LoadJson : Load<JsonNode, FileInfo>
{
    public LoadJson([What] JsonNode what, [From] FileInfo from) : base(what, from) { }
    protected override async ValueTask<JsonNode> ActAsync(FileInfo from, CancellationToken cancellationToken)
    {
        string text = await File.ReadAllTextAsync(from.FullName, cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(text) ?? new JsonObject();
    }
}

[Qualifier("JSON")]
public sealed class SaveJson : Save<JsonNode, FileInfo>
{
    public SaveJson([What] JsonNode what, [To] FileInfo to) : base(what, to) { }
    protected override ValueTask SaveAsync(JsonNode what, FileInfo to, CancellationToken cancellationToken) => new(File.WriteAllTextAsync(to.FullName, what.ToJsonString(), cancellationToken));
}
