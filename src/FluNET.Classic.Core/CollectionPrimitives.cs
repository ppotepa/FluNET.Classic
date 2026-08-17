namespace FluNET.Classic.Core;

public sealed record CollectionGroup(object? Key, Array Items)
{
    public int Count => Items.Length;
}
