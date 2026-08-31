using FluNET.Classic.Storage;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class StorageProjectionTests
{
    [Test]
    public async Task Storage_object_and_metadata_projections_return_typed_fields()
    {
        var key = new StorageKey("documents/readme.txt");
        var metadata = new StorageMetadata(42, new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero), "text/plain", "abc");
        var item = new StorageObject(key, metadata);

        Assert.That(await new GetStorageObjectKey(item).ExecuteAsync(null!), Is.EqualTo(key));
        Assert.That(await new GetStorageMetadataLength(metadata).ExecuteAsync(null!), Is.EqualTo(42));
        Assert.That(await new GetStorageMetadataLastModified(metadata).ExecuteAsync(null!), Is.EqualTo(metadata.LastModified));
        Assert.That(await new GetStorageMetadataContentType(metadata).ExecuteAsync(null!), Is.EqualTo("text/plain"));
        Assert.That(await new GetStorageMetadataETag(metadata).ExecuteAsync(null!), Is.EqualTo("abc"));
    }
}
