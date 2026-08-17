namespace FluNET.Runtime;

public sealed class RuntimeState
{
    private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);

    public object? PipelineValue { get; internal set; }

    public IReadOnlyDictionary<string, object?> Variables => _variables;

    public bool TryGetVariable(string name, out object? value) =>
        _variables.TryGetValue(name, out value);

    public void SetVariable(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _variables[name] = value;
    }
}
