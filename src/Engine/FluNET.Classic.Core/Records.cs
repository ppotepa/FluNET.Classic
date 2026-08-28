namespace FluNET.Classic.Core;

public sealed record FluRecordField(string Name, Type Type);

public sealed class FluRecordSchema
{
    private readonly IReadOnlyDictionary<string, FluRecordField> _fields;

    public FluRecordSchema(string name, IEnumerable<FluRecordField> fields)
    {
        Name = name;
        FluRecordField[] values = fields.ToArray();
        if (values.Length == 0) throw new ArgumentException("A record requires at least one field.", nameof(fields));
        _fields = values.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        Fields = values;
    }

    public string Name { get; }
    public IReadOnlyList<FluRecordField> Fields { get; }
    public bool TryGetField(string name, out FluRecordField field) => _fields.TryGetValue(name, out field!);
}

/// <summary>An immutable, schema-backed value produced by a FluNET record declaration.</summary>
public sealed class FluRecord
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public FluRecord(FluRecordSchema schema, IReadOnlyDictionary<string, object?> values)
    {
        Schema = schema;
        _values = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
    }

    public FluRecordSchema Schema { get; }
    public object? Get(string field) => _values.TryGetValue(field, out object? value) ? value : throw new KeyNotFoundException($"Record {Schema.Name} has no field '{field}'.");
    public IReadOnlyDictionary<string, object?> Values => _values;
    public override string ToString() => $"{Schema.Name}{{{string.Join(", ", Schema.Fields.Select(field => $"{field.Name}={_values.GetValueOrDefault(field.Name)}"))}}}";
}
