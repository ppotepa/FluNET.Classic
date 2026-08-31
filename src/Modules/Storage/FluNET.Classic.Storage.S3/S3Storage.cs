using FluNET.Classic.Core;

namespace FluNET.Classic.Storage.S3;

public sealed record S3ObjectItem(string Key, long Length, DateTimeOffset? LastModified = null, string? ContentType = null, string? ETag = null);
public interface IS3ObjectClient
{
    ValueTask<byte[]?> GetAsync(string bucket, string key, CancellationToken cancellationToken = default);
    ValueTask PutAsync(string bucket, string key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string bucket, string key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<S3ObjectItem>> ListAsync(string bucket, string? prefix = null, CancellationToken cancellationToken = default);
    ValueTask CopyAsync(string bucket, string source, string destination, CancellationToken cancellationToken = default);
}

public sealed class S3StorageProvider : IStorageProvider
{
    private readonly IS3ObjectClient _client; private readonly string _bucket;
    public S3StorageProvider(IS3ObjectClient client, string bucket)
    {
        _client = client;
        _bucket = bucket;
    }
    public ValueTask<byte[]?> GetAsync(StorageKey key, CancellationToken cancellationToken = default) => _client.GetAsync(_bucket, key.Value, cancellationToken);
    public ValueTask SaveAsync(StorageKey key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) => _client.PutAsync(_bucket, key.Value, data, cancellationToken);
    public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => _client.DeleteAsync(_bucket, key.Value, cancellationToken);
    public async ValueTask<IReadOnlyList<StorageObject>> ListAsync(StorageContainer container, CancellationToken cancellationToken = default) => (await _client.ListAsync(_bucket, container.Value, cancellationToken).ConfigureAwait(false)).Select(x => new StorageObject(new StorageKey(x.Key), new StorageMetadata(x.Length, x.LastModified, x.ContentType, x.ETag))).ToArray();
    public ValueTask CopyAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default) => _client.CopyAsync(_bucket, source.Value, destination.Value, cancellationToken);
    public async ValueTask MoveAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default)
    {
        await CopyAsync(source, destination, cancellationToken).ConfigureAwait(false);
        await DeleteAsync(source, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class S3StorageModule : LanguageModule
{
    public override string Name => "storage.s3"; public override IReadOnlyCollection<string> Dependencies => new[] { "storage" };
}
