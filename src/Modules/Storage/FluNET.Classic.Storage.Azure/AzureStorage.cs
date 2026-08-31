using FluNET.Classic.Core;

namespace FluNET.Classic.Storage.Azure;

public sealed record AzureBlobItem(string Name, long Length, DateTimeOffset? LastModified = null, string? ContentType = null, string? ETag = null);
public interface IAzureBlobClient
{
    ValueTask<byte[]?> DownloadAsync(string container, string blobName, CancellationToken cancellationToken = default);
    ValueTask UploadAsync(string container, string blobName, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string container, string blobName, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<AzureBlobItem>> ListAsync(string container, string? prefix = null, CancellationToken cancellationToken = default);
    ValueTask CopyAsync(string container, string source, string destination, CancellationToken cancellationToken = default);
}

public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private readonly IAzureBlobClient _client; private readonly string _container;
    public AzureBlobStorageProvider(IAzureBlobClient client, string container) { _client = client; _container = container; }
    public ValueTask<byte[]?> GetAsync(StorageKey key, CancellationToken cancellationToken = default) => _client.DownloadAsync(_container, key.Value, cancellationToken);
    public ValueTask SaveAsync(StorageKey key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default) => _client.UploadAsync(_container, key.Value, data, cancellationToken);
    public ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default) => _client.DeleteAsync(_container, key.Value, cancellationToken);
    public async ValueTask<IReadOnlyList<StorageObject>> ListAsync(StorageContainer container, CancellationToken cancellationToken = default) => (await _client.ListAsync(_container, container.Value, cancellationToken).ConfigureAwait(false)).Select(x => new StorageObject(new StorageKey(x.Name), new StorageMetadata(x.Length, x.LastModified, x.ContentType, x.ETag))).ToArray();
    public ValueTask CopyAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default) => _client.CopyAsync(_container, source.Value, destination.Value, cancellationToken);
    public async ValueTask MoveAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default) { await CopyAsync(source, destination, cancellationToken).ConfigureAwait(false); await DeleteAsync(source, cancellationToken).ConfigureAwait(false); }
}

public sealed class AzureBlobStorageModule : LanguageModule { public override string Name => "storage.azure"; public override IReadOnlyCollection<string> Dependencies => new[] { "storage" }; }
