namespace FluNET.Classic.Runtime;

public sealed class RuntimeState
{
    private readonly Stack<Dictionary<string, object?>> _scopes = new();

    public RuntimeState() => _scopes.Push(new(StringComparer.OrdinalIgnoreCase));

    public object? PipelineValue
    {
        get; set;
    }

    public IReadOnlyDictionary<string, object?> Variables => _scopes.Reverse().SelectMany(x => x).GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Last().Value, StringComparer.OrdinalIgnoreCase);

    public void SetVariable(string name, object? value) => _scopes.Peek()[name] = value;

    public bool TryGetVariable(string name, out object? value)
    {
        foreach (Dictionary<string, object?> scope in _scopes)
            if (scope.TryGetValue(name, out value))
                return true;
        value = null;
        return false;
    }

    public IDisposable PushScope()
    {
        _scopes.Push(new(StringComparer.OrdinalIgnoreCase));
        return new Scope(this);
    }

    private sealed class Scope(RuntimeState owner) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (owner._scopes.Count > 1)
                owner._scopes.Pop();
        }
    }
}
