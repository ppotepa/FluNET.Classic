using FluNET.Classic.Core;
using System.Text.Json.Nodes;

namespace FluNET.Classic.Standard.Json;

public enum JsonRepresentation { JSON, TEXT }

public sealed class JsonModule : LanguageModule
{
    public override string Name => "json";
    public override IReadOnlyCollection<string> Dependencies => new[] { "files" };
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:json-properties", "PROPERTIES", typeof(JsonProperty[])),
        new("qualifier:json-items", "ITEMS", typeof(JsonItem[]))
    };
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

[Verb("PARSE")]
[Qualifier("JSON")]
public sealed class ParseJsonFromText : IVerb<JsonNode>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<JsonNode>
{
    private readonly string _text;
    public ParseJsonFromText([From] string text) => _text = text;
    public ValueTask<JsonNode> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(JsonNode.Parse(_text) ?? new JsonObject());
}

[Verb("PARSE")]
public sealed class ParseJsonAs : IVerb<JsonNode>, IParse, IWhat<string>, IAs<JsonRepresentation>, IPipelineConsumer<string>, IPipelineProducer<JsonNode>
{
    private readonly string _text;
    private readonly JsonRepresentation _representation;
    public ParseJsonAs([What] string text, [As] JsonRepresentation representation) { _text = text; _representation = representation; }
    public ValueTask<JsonNode> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (_representation != JsonRepresentation.JSON) throw new InvalidOperationException($"Cannot PARSE text AS {_representation}.");
        return ValueTask.FromResult(JsonNode.Parse(_text) ?? new JsonObject());
    }
}

[Verb("FORMAT")]
[Qualifier("JSON")]
public sealed class FormatJsonFromNode : IVerb<string>, IFormat, IFrom<JsonNode>, IPipelineConsumer<JsonNode>, IPipelineProducer<string>
{
    private readonly JsonNode _node;
    public FormatJsonFromNode([From] JsonNode node) => _node = node;
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_node.ToJsonString());
}

public sealed class FormatJsonAs : Format<string, JsonNode, JsonRepresentation>
{
    public FormatJsonAs([What] JsonNode what, [As] JsonRepresentation @as) : base(what, @as) { }
    protected override ValueTask<string> FormatAsync(JsonNode what, JsonRepresentation @as, CancellationToken cancellationToken)
    {
        if (@as != JsonRepresentation.JSON) throw new InvalidOperationException($"Cannot FORMAT JSON AS {@as}.");
        return ValueTask.FromResult(what.ToJsonString());
    }
}

public sealed class TransformTextToJson : TransformTo<JsonNode, string, JsonRepresentation>
{
    public TransformTextToJson([What] string what, [To] JsonRepresentation to) : base(what, to) { }
    protected override ValueTask<JsonNode> TransformAsync(string what, JsonRepresentation to, CancellationToken cancellationToken)
    {
        if (to != JsonRepresentation.JSON) throw new InvalidOperationException($"Cannot TRANSFORM text TO {to}.");
        return ValueTask.FromResult(JsonNode.Parse(what) ?? new JsonObject());
    }
}

public sealed class TransformJsonToText : TransformTo<string, JsonNode, JsonRepresentation>
{
    public TransformJsonToText([What] JsonNode what, [To] JsonRepresentation to) : base(what, to) { }
    protected override ValueTask<string> TransformAsync(JsonNode what, JsonRepresentation to, CancellationToken cancellationToken)
    {
        if (to != JsonRepresentation.TEXT) throw new InvalidOperationException($"Cannot TRANSFORM JSON TO {to}.");
        return ValueTask.FromResult(what.ToJsonString());
    }
}
