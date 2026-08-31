using FluNET.Classic.Core;

namespace FluNET.Classic.Storage;

public sealed record StorageKey(string Value)
{
    public override string ToString() => Value;
}
public sealed record StorageContainer(string Value)
{
    public override string ToString() => Value;
}
public sealed record StorageMetadata(long Length, DateTimeOffset? LastModified = null, string? ContentType = null, string? ETag = null);
public sealed record StorageObject(StorageKey Key, StorageMetadata Metadata) : IExistenceState
{
    public bool Exists => true;
}

public interface IStorageProvider
{
    ValueTask<byte[]?> GetAsync(StorageKey key, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(StorageKey key, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(StorageKey key, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<StorageObject>> ListAsync(StorageContainer container, CancellationToken cancellationToken = default);
    ValueTask CopyAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default);
    ValueTask MoveAsync(StorageKey source, StorageKey destination, CancellationToken cancellationToken = default);
}

public sealed class StorageModule : LanguageModule
{
    public override string Name => "storage";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:storage-object", "OBJECT", typeof(StorageObject)),
        new("qualifier:storage-objects", "OBJECTS", typeof(StorageObject[])),
        new("qualifier:storage-metadata", "METADATA", typeof(StorageMetadata))
    };
}

[Verb("GET")]
[Qualifier("BINARY")]
[RequiresCapability(StandardCapabilities.StorageRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
[ExecutionTrait(ExecutionTrait.Retryable)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class GetStorageBinary : IVerb<byte[]>, IGet, IFrom<StorageKey>, IPipelineProducer<byte[]>
{
    private readonly StorageKey _key; private readonly IStorageProvider _provider;
    public GetStorageBinary([From] StorageKey key, [FromServices] IStorageProvider provider)
    {
        _key = key;
        _provider = provider;
    }
    public async ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => await _provider.GetAsync(_key, cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException($"Storage object '{_key}' was not found.");
}

[Verb("SAVE")]
[Qualifier("BINARY")]
[RequiresCapability(StandardCapabilities.StorageWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class SaveStorageBinary : IVerb<byte[]>, ISave, IWhat<byte[]>, ITo<StorageKey>, IPipelineConsumer<byte[]>, IPipelineProducer<byte[]>
{
    private readonly byte[] _data; private readonly StorageKey _key; private readonly IStorageProvider _provider;
    public SaveStorageBinary([What] byte[] data, [To] StorageKey key, [FromServices] IStorageProvider provider)
    {
        _data = data;
        _key = key;
        _provider = provider;
    }
    public async ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _provider.SaveAsync(_key, _data, cancellationToken).ConfigureAwait(false);
        return _data;
    }
}

[Verb("DELETE")]
[Qualifier("OBJECT")]
[RequiresCapability(StandardCapabilities.StorageWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class DeleteStorageObject : IVerb<bool>, IDelete, IAt<StorageKey>, IPipelineProducer<bool>
{
    private readonly StorageKey _key; private readonly IStorageProvider _provider;
    public DeleteStorageObject([At] StorageKey key, [FromServices] IStorageProvider provider)
    {
        _key = key;
        _provider = provider;
    }
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _provider.DeleteAsync(_key, cancellationToken);
}

[Verb("LIST")]
[Qualifier("OBJECTS")]
[RequiresCapability(StandardCapabilities.StorageRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class ListStorageObjects : IVerb<StorageObject[]>, IListVerb, IIn<StorageContainer>, IPipelineProducer<StorageObject[]>
{
    private readonly StorageContainer _container; private readonly IStorageProvider _provider;
    public ListStorageObjects([In] StorageContainer container, [FromServices] IStorageProvider provider)
    {
        _container = container;
        _provider = provider;
    }
    public async ValueTask<StorageObject[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => (await _provider.ListAsync(_container, cancellationToken).ConfigureAwait(false)).ToArray();
}

[Verb("COPY")]
[Qualifier("OBJECT")]
[RequiresCapability(StandardCapabilities.StorageRead)]
[RequiresCapability(StandardCapabilities.StorageWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class CopyStorageObject : IVerb<StorageKey>, ICopy, IWhat<StorageKey>, ITo<StorageKey>, IPipelineConsumer<StorageKey>, IPipelineProducer<StorageKey>
{
    private readonly StorageKey _source; private readonly StorageKey _destination; private readonly IStorageProvider _provider;
    public CopyStorageObject([What] StorageKey source, [To] StorageKey destination, [FromServices] IStorageProvider provider)
    {
        _source = source;
        _destination = destination;
        _provider = provider;
    }
    public async ValueTask<StorageKey> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _provider.CopyAsync(_source, _destination, cancellationToken).ConfigureAwait(false);
        return _destination;
    }
}

[Verb("MOVE")]
[Qualifier("OBJECT")]
[RequiresCapability(StandardCapabilities.StorageRead)]
[RequiresCapability(StandardCapabilities.StorageWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class MoveStorageObject : IVerb<StorageKey>, IMove, IWhat<StorageKey>, ITo<StorageKey>, IPipelineConsumer<StorageKey>, IPipelineProducer<StorageKey>
{
    private readonly StorageKey _source; private readonly StorageKey _destination; private readonly IStorageProvider _provider;
    public MoveStorageObject([What] StorageKey source, [To] StorageKey destination, [FromServices] IStorageProvider provider)
    {
        _source = source;
        _destination = destination;
        _provider = provider;
    }
    public async ValueTask<StorageKey> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _provider.MoveAsync(_source, _destination, cancellationToken).ConfigureAwait(false);
        return _destination;
    }
}
