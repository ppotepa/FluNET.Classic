namespace FluNET.Classic.Core;

/// <summary>Legacy untyped grouping shape retained for compatibility with early 0.x consumers.</summary>
public sealed record CollectionGroup(object? Key, Array Items)
{
    public int Count => Items.Length;
}

/// <summary>A statically typed grouping produced by GROUP ... BY ... in the 0.2 language.</summary>
public sealed record CollectionGroup<TKey, TElement>(TKey? Key, IReadOnlyList<TElement> Items)
{
    public int Count => Items.Count;
}
