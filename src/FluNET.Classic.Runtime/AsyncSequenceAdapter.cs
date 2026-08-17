using System.Reflection;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

internal static class AsyncSequenceAdapter
{
    private static readonly MethodInfo ToListCoreMethod = Method(nameof(ToListCore));
    private static readonly MethodInfo ForEachCoreMethod = Method(nameof(ForEachCore));
    private static readonly MethodInfo WhereCoreMethod = Method(nameof(WhereCore));
    private static readonly MethodInfo TakeCoreMethod = Method(nameof(TakeCore));
    private static readonly MethodInfo SkipCoreMethod = Method(nameof(SkipCore));
    private static readonly MethodInfo DistinctCoreMethod = Method(nameof(DistinctCore));
    private static readonly MethodInfo CountCoreMethod = Method(nameof(CountCore));

    public static bool CanEnumerate(object? source) => source is not null && ClrTypeShape.IsAsyncEnumerableType(source.GetType());

    public static ValueTask<List<object?>> ToListAsync(object source, CancellationToken cancellationToken) =>
        InvokeValueTask<List<object?>>(ToListCoreMethod, source, cancellationToken);

    public static ValueTask ForEachAsync(object source, Func<object?, ValueTask> action, CancellationToken cancellationToken) =>
        InvokeValueTask(ForEachCoreMethod, source, action, cancellationToken);

    public static object Where(object source, Func<object?, bool> predicate) =>
        InvokeSequence(WhereCoreMethod, source, predicate);

    public static object Take(object source, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "TAKE requires a non-negative amount.");
        return InvokeSequence(TakeCoreMethod, source, amount);
    }

    public static object Skip(object source, int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "SKIP requires a non-negative amount.");
        return InvokeSequence(SkipCoreMethod, source, amount);
    }

    public static object Distinct(object source, Func<object?, object?> keySelector, Func<object?, object?, bool> equals) =>
        InvokeSequence(DistinctCoreMethod, source, keySelector, equals);

    public static ValueTask<int> CountAsync(object source, CancellationToken cancellationToken) =>
        InvokeValueTask<int>(CountCoreMethod, source, cancellationToken);

    private static object InvokeSequence(MethodInfo method, object source, params object?[] arguments)
    {
        MethodInfo generic = method.MakeGenericMethod(ElementType(source));
        ParameterInfo[] parameters = generic.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = source;
        for (int index = 0; index < arguments.Length; index++) args[index + 1] = arguments[index];
        for (int index = arguments.Length + 1; index < args.Length; index++) args[index] = Type.Missing;
        return generic.Invoke(null, args)
            ?? throw new InvalidOperationException("Async sequence operator returned null.");
    }

    private static ValueTask<TResult> InvokeValueTask<TResult>(MethodInfo method, object source, params object?[] arguments)
    {
        Type elementType = ElementType(source);
        object?[] args = new object?[arguments.Length + 1];
        args[0] = source;
        Array.Copy(arguments, 0, args, 1, arguments.Length);
        return (ValueTask<TResult>)method.MakeGenericMethod(elementType).Invoke(null, args)!;
    }

    private static ValueTask InvokeValueTask(MethodInfo method, object source, params object?[] arguments)
    {
        Type elementType = ElementType(source);
        object?[] args = new object?[arguments.Length + 1];
        args[0] = source;
        Array.Copy(arguments, 0, args, 1, arguments.Length);
        return (ValueTask)method.MakeGenericMethod(elementType).Invoke(null, args)!;
    }

    private static Type ElementType(object source) => ClrTypeShape.GetElementType(source.GetType())
        ?? throw new InvalidOperationException($"'{source.GetType().Name}' is not an async enumerable.");

    private static MethodInfo Method(string name) => typeof(AsyncSequenceAdapter)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static async ValueTask<List<object?>> ToListCore<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var result = new List<object?>();
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false)) result.Add(item);
        return result;
    }

    private static async ValueTask ForEachCore<T>(IAsyncEnumerable<T> source, Func<object?, ValueTask> action, CancellationToken cancellationToken)
    {
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }
    }

    private static async IAsyncEnumerable<T> WhereCore<T>(IAsyncEnumerable<T> source, Func<object?, bool> predicate, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            if (predicate(item)) yield return item;
    }

    private static async IAsyncEnumerable<T> TakeCore<T>(IAsyncEnumerable<T> source, int amount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (amount == 0) yield break;
        int count = 0;
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
            if (++count >= amount) yield break;
        }
    }

    private static async IAsyncEnumerable<T> SkipCore<T>(IAsyncEnumerable<T> source, int amount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        int skipped = 0;
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (skipped < amount) { skipped++; continue; }
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> DistinctCore<T>(IAsyncEnumerable<T> source, Func<object?, object?> keySelector, Func<object?, object?, bool> equals, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var keys = new List<object?>();
        await foreach (T item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            object? key = keySelector(item);
            if (keys.Any(existing => equals(existing, key))) continue;
            keys.Add(key);
            yield return item;
        }
    }

    private static async ValueTask<int> CountCore<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        int count = 0;
        await foreach (T _ in source.WithCancellation(cancellationToken).ConfigureAwait(false)) checked { count++; }
        return count;
    }
}
