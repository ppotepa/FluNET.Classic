namespace FluNET.Classic.Core;

public enum CollectionSortDirection
{
    ASCENDING,
    DESCENDING
}

public enum CollectionEquality
{
    DEFAULT,
    ORDINAL,
    ORDINAL_IGNORE_CASE
}

/// <summary>A statically typed grouping produced by GROUP ... BY ... .</summary>
public sealed record CollectionGroup<TKey, TElement>(TKey? Key, IReadOnlyList<TElement> Items)
{
    public int Count => Items.Count;
}
