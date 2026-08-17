using System.Reflection;
using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

internal static class AsyncSequenceAdapter
{
    private static readonly MethodInfo ToListCoreMethod = typeof(AsyncSequenceAdapter)
        .GetMethod(nameof(ToListCore), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo ForEachCoreMethod = typeof(AsyncSequenceAdapter)
        .GetMethod(nameof(ForEachCore), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static bool CanEnumerate(object? source) => source is not null && ClrTypeShape.IsAsyncEnumerableType(source.GetType());

    public static ValueTask<List<object?>> ToListAsync(object source, CancellationToken cancellationToken)
    {
        Type elementType = ClrTypeShape.GetElementType(source.GetType())
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' is not an async enumerable.");
        object task = ToListCoreMethod.MakeGenericMethod(elementType).Invoke(null, new[] { source, cancellationToken })!;
        return (ValueTask<List<object?>>)task;
    }

    public static ValueTask ForEachAsync(object source, Func<object?, ValueTask> action, CancellationToken cancellationToken)
    {
        Type elementType = ClrTypeShape.GetElementType(source.GetType())
            ?? throw new InvalidOperationException($"'{source.GetType().Name}' is not an async enumerable.");
        object task = ForEachCoreMethod.MakeGenericMethod(elementType).Invoke(null, new object?[] { source, action, cancellationToken })!;
        return (ValueTask)task;
    }

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
}
