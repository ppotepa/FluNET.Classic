using FluNET.Classic.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FluNET.Classic.Standard.Json;

public sealed record JsonPath(string Value)
{
    public IReadOnlyList<string> Segments => Value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    public override string ToString() => Value;
}
public enum JsonFormatting
{
    COMPACT, INDENTED
}
public sealed record JsonShape(IReadOnlyDictionary<string, JsonValueKind> Properties, bool RequireAll = true);
public sealed record JsonValidationResult(bool IsValid, IReadOnlyList<string> Errors) : IValidState;
public sealed record JsonProperty(string Name, JsonNode? Value);
public sealed record JsonItem(int Index, JsonNode? Value);

[Verb("GET")]
[Qualifier("JSON")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetJsonAtPath : IVerb<JsonNode?>, IGet, IFrom<JsonNode>, IAt<JsonPath>, IPipelineProducer<JsonNode?>
{
    private readonly JsonNode _node; private readonly JsonPath _path; public GetJsonAtPath([From] JsonNode node, [At] JsonPath path)
    {
        _node = node;
        _path = path;
    }
    public ValueTask<JsonNode?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        JsonNode? current = _node;
        foreach (string segment in _path.Segments)
            current = current switch
            {
                JsonObject obj when obj.TryGetPropertyValue(segment, out JsonNode? value) => value,
                JsonArray array when int.TryParse(segment, out int index) && index >= 0 && index < array.Count => array[index],
                _ => null
            };
        return ValueTask.FromResult(current);
    }
}

[Verb("GET")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetJsonTextAtPath : IVerb<string?>, IGet, IFrom<JsonNode>, IAt<JsonPath>, IPipelineProducer<string?>
{
    private readonly JsonNode _node; private readonly JsonPath _path; public GetJsonTextAtPath([From] JsonNode node, [At] JsonPath path)
    {
        _node = node;
        _path = path;
    }
    public async ValueTask<string?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        JsonNode? value = await new GetJsonAtPath(_node, _path).ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        return value is JsonValue ? value.ToString() : value?.ToJsonString();
    }
}

[Verb("LIST")]
[Qualifier("PROPERTIES")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ListJsonProperties : IVerb<JsonProperty[]>, IListVerb, IWhat<JsonProperty[]>, IFrom<JsonNode>, IPipelineConsumer<JsonNode>, IPipelineProducer<JsonProperty[]>
{
    private readonly JsonNode _node;
    public ListJsonProperties([What] JsonProperty[] what, [From] JsonNode node) => _node = node;
    public ValueTask<JsonProperty[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        JsonProperty[] result = _node is JsonObject obj ? obj.Select(x => new JsonProperty(x.Key, x.Value)).ToArray() : Array.Empty<JsonProperty>();
        return ValueTask.FromResult(result);
    }
}

[Verb("LIST")]
[Qualifier("ITEMS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ListJsonItems : IVerb<JsonItem[]>, IListVerb, IWhat<JsonItem[]>, IFrom<JsonNode>, IPipelineConsumer<JsonNode>, IPipelineProducer<JsonItem[]>
{
    private readonly JsonNode _node;
    public ListJsonItems([What] JsonItem[] what, [From] JsonNode node) => _node = node;
    public ValueTask<JsonItem[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        JsonItem[] result = _node is JsonArray array ? array.Select((value, index) => new JsonItem(index, value)).ToArray() : Array.Empty<JsonItem>();
        return ValueTask.FromResult(result);
    }
}

[Verb("CHECK")]
[Qualifier("JSON")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class CheckJsonShape : IVerb<JsonValidationResult>, ICheck, IWhat<JsonNode>, IUsing<JsonShape>, IPipelineConsumer<JsonNode>, IPipelineProducer<JsonValidationResult>
{
    private readonly JsonNode _node; private readonly JsonShape _shape; public CheckJsonShape([What] JsonNode node, [Using] JsonShape shape)
    {
        _node = node;
        _shape = shape;
    }
    public ValueTask<JsonValidationResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (_node is not JsonObject obj)
            errors.Add("Root value is not an object.");
        else
        foreach ((string name, JsonValueKind kind) in _shape.Properties)
        {
            if (!obj.TryGetPropertyValue(name, out JsonNode? value) || value is null)
            {
                if (_shape.RequireAll)
                    errors.Add($"Missing property '{name}'.");
                continue;
            }
            using JsonDocument doc = JsonDocument.Parse(value.ToJsonString());
            if (doc.RootElement.ValueKind != kind)
                errors.Add($"Property '{name}' is {doc.RootElement.ValueKind}, expected {kind}.");
        }
        return ValueTask.FromResult(new JsonValidationResult(errors.Count == 0, errors));
    }
}

[Verb("FORMAT")]
[Qualifier("JSON")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatJsonUsing : IVerb<string>, IFormat, IWhat<JsonNode>, IUsing<JsonFormatting>, IPipelineConsumer<JsonNode>, IPipelineProducer<string>
{
    private readonly JsonNode _node; private readonly JsonFormatting _formatting; public FormatJsonUsing([What] JsonNode node, [Using] JsonFormatting formatting)
    {
        _node = node;
        _formatting = formatting;
    }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_node.ToJsonString(new JsonSerializerOptions { WriteIndented = _formatting == JsonFormatting.INDENTED }));
}
