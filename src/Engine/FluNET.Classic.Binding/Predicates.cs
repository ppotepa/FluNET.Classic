using System.Collections;
using FluNET.Classic.Core;

namespace FluNET.Classic.Binding;

public sealed record PredicateContext(IServiceProvider? Services = null);
public interface IValuePredicate { string Name { get; } bool CanEvaluate(Type valueType); bool Evaluate(object? value, PredicateContext context); }

public sealed class PredicateRegistry
{
    private readonly Dictionary<string, List<IValuePredicate>> _predicates = new(StringComparer.OrdinalIgnoreCase);
    public PredicateRegistry() { Register(new ExistsPredicate()); Register(new OkPredicate()); Register(new ValidPredicate()); Register(new EmptyPredicate()); }
    public void Register(IValuePredicate predicate) { ArgumentNullException.ThrowIfNull(predicate); if (!_predicates.TryGetValue(predicate.Name, out List<IValuePredicate>? items)) _predicates[predicate.Name] = items = []; items.Add(predicate); }
    public bool CanEvaluate(string name, Type valueType) => _predicates.TryGetValue(name, out List<IValuePredicate>? items) && items.Any(x => x.CanEvaluate(valueType));
    public bool Evaluate(string name, object? value, PredicateContext context)
    {
        Type runtimeType = value?.GetType() ?? typeof(object);
        if (_predicates.TryGetValue(name, out List<IValuePredicate>? items)) { IValuePredicate? predicate = items.FirstOrDefault(x => x.CanEvaluate(runtimeType)); if (predicate is not null) return predicate.Evaluate(value, context); if (value is null && name.Equals("EXISTS", StringComparison.OrdinalIgnoreCase)) return false; if (value is null && name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase)) return true; }
        throw new InvalidOperationException($"Predicate '{name}' cannot evaluate {runtimeType.Name}.");
    }
    private sealed class ExistsPredicate : IValuePredicate { public string Name => "EXISTS"; public bool CanEvaluate(Type valueType) => typeof(FileSystemInfo).IsAssignableFrom(valueType) || typeof(IExistenceState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { null => false, FileSystemInfo fileSystemInfo => fileSystemInfo.Exists, IExistenceState state => state.Exists, _ => false }; }
    private sealed class OkPredicate : IValuePredicate { public string Name => "OK"; public bool CanEvaluate(Type valueType) => valueType == typeof(bool) || typeof(IOkState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { bool boolean => boolean, IOkState state => state.IsOk, _ => false }; }
    private sealed class ValidPredicate : IValuePredicate { public string Name => "VALID"; public bool CanEvaluate(Type valueType) => valueType == typeof(bool) || typeof(IValidState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { bool boolean => boolean, IValidState state => state.IsValid, _ => false }; }
    private sealed class EmptyPredicate : IValuePredicate
    {
        public string Name => "EMPTY";
        public bool CanEvaluate(Type valueType) => valueType == typeof(string) || typeof(IEnumerable).IsAssignableFrom(valueType);
        public bool Evaluate(object? value, PredicateContext context) => value switch { null => true, string text => text.Length == 0, ICollection collection => collection.Count == 0, IEnumerable enumerable => !enumerable.GetEnumerator().MoveNext(), _ => false };
    }
}
