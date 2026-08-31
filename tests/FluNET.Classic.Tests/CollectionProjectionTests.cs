using FluNET.Classic.Archive;
using FluNET.Classic.Storage;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class CollectionProjectionTests
{
    [Test]
    public async Task Storage_object_collections_project_keys()
    {
        var objects = new[]
        {
            new StorageObject(new StorageKey("one"), new StorageMetadata(1)),
            new StorageObject(new StorageKey("two"), new StorageMetadata(2))
        };

        Assert.That(await new GetStorageObjectKeys(objects).ExecuteAsync(null!), Is.EqualTo(new[] { new StorageKey("one"), new StorageKey("two") }));
    }

    [Test]
    public async Task Archive_entry_collections_project_names_and_lengths()
    {
        var entries = new[]
        {
            new ArchiveEntry("one", new byte[] { 1 }, 1),
            new ArchiveEntry("two", new byte[] { 1, 2 }, 2)
        };

        Assert.That(await new GetArchiveEntryNames(entries).ExecuteAsync(null!), Is.EqualTo(new[] { "one", "two" }));
        Assert.That(await new GetArchiveEntryLengths(entries).ExecuteAsync(null!), Is.EqualTo(new long[] { 1, 2 }));
    }
}
