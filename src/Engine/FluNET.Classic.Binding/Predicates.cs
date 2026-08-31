using FluNET.Classic.Core;
using System.Collections;

namespace FluNET.Classic.Binding;

public sealed record PredicateContext(IServiceProvider? Services = null);
public interface IValuePredicate
{
    string Name
    {
        get;
    }
    bool CanEvaluate(Type valueType); bool Evaluate(object? value, PredicateContext context);
}

public sealed class PredicateRegistry
{
    private readonly Dictionary<string, List<PredicateEntry>> _predicates = new(StringComparer.OrdinalIgnoreCase);
    public PredicateRegistry()
    {
        Register(new ExistsPredicate(), id: "builtin.exists");
        Register(new OkPredicate(), id: "builtin.ok");
        Register(new ValidPredicate(), id: "builtin.valid");
        Register(new EmptyPredicate(), id: "builtin.empty");
    }
    public void Register(IValuePredicate predicate, int priority = 0, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (!_predicates.TryGetValue(predicate.Name, out List<PredicateEntry>? items))
            _predicates[predicate.Name] = items = [];
        string registrationId = RegistrationId(predicate, priority, id, items.Select(x => x.Id));
        items.Add(new(registrationId, predicate, priority));
        items.Sort((a, b) => b.Priority != a.Priority ? b.Priority.CompareTo(a.Priority) : string.Compare(a.Id, b.Id, StringComparison.Ordinal));
    }
    public bool CanEvaluate(string name, Type valueType) => _predicates.TryGetValue(name, out List<PredicateEntry>? items) && items.Any(x => x.Predicate.CanEvaluate(valueType));
    public bool Evaluate(string name, object? value, PredicateContext context)
    {
        Type runtimeType = value?.GetType() ?? typeof(object);
        if (_predicates.TryGetValue(name, out List<PredicateEntry>? items))
        {
            PredicateEntry[] candidates = items.Where(x => x.Predicate.CanEvaluate(runtimeType)).ToArray();
            if (candidates.Length > 0)
            {
                int priority = candidates[0].Priority;
                PredicateEntry[] winners = candidates.Where(x => x.Priority == priority).ToArray();
                if (winners.Length > 1)
                    throw new InvalidOperationException($"Predicate '{name}' is ambiguous for {runtimeType.Name}: {string.Join(", ", winners.Select(x => x.Id))}.");
                return winners[0].Predicate.Evaluate(value, context);
            }
            if (value is null && name.Equals("EXISTS", StringComparison.OrdinalIgnoreCase))
                return false;
            if (value is null && name.Equals("EMPTY", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        throw new InvalidOperationException($"Predicate '{name}' cannot evaluate {runtimeType.Name}.");
    }

    private static string RegistrationId(IValuePredicate predicate, int priority, string? requestedId, IEnumerable<string> existingIds)
    {
        string baseId = string.IsNullOrWhiteSpace(requestedId)
            ? $"predicate:{predicate.GetType().FullName ?? predicate.GetType().Name}:priority:{priority}"
            : requestedId.Trim();
        if (!string.IsNullOrWhiteSpace(requestedId) && existingIds.Contains(baseId, StringComparer.Ordinal))
            throw new ArgumentException($"A predicate with ID '{baseId}' is already registered.", nameof(requestedId));
        if (!string.IsNullOrWhiteSpace(requestedId))
            return baseId;
        int suffix = 1;
        string candidate = baseId;
        while (existingIds.Contains(candidate, StringComparer.Ordinal))
            candidate = $"{baseId}:{++suffix}";
        return candidate;
    }

    private sealed record PredicateEntry(string Id, IValuePredicate Predicate, int Priority);

    private sealed class ExistsPredicate : IValuePredicate
    {
        public string Name => "EXISTS"; public bool CanEvaluate(Type valueType) => typeof(FileSystemInfo).IsAssignableFrom(valueType) || typeof(IExistenceState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { null => false, FileSystemInfo fileSystemInfo => fileSystemInfo.Exists, IExistenceState state => state.Exists, _ => false };
    }
    private sealed class OkPredicate : IValuePredicate
    {
        public string Name => "OK"; public bool CanEvaluate(Type valueType) => valueType == typeof(bool) || typeof(IOkState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { bool boolean => boolean, IOkState state => state.IsOk, _ => false };
    }
    private sealed class ValidPredicate : IValuePredicate
    {
        public string Name => "VALID"; public bool CanEvaluate(Type valueType) => valueType == typeof(bool) || typeof(IValidState).IsAssignableFrom(valueType); public bool Evaluate(object? value, PredicateContext context) => value switch { bool boolean => boolean, IValidState state => state.IsValid, _ => false };
    }
    private sealed class EmptyPredicate : IValuePredicate
    {
        public string Name => "EMPTY";
        public bool CanEvaluate(Type valueType) => valueType == typeof(string) || typeof(IEnumerable).IsAssignableFrom(valueType);
        public bool Evaluate(object? value, PredicateContext context) => value switch { null => true, string text => text.Length == 0, ICollection collection => collection.Count == 0, IEnumerable enumerable => !enumerable.GetEnumerator().MoveNext(), _ => false };
    }
}
