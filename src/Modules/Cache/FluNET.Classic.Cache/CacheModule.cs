using FluNET.Classic.Core;
using System.Collections.Concurrent;

namespace FluNET.Classic.Cache;

public sealed record CacheKey(string Value)
{
    public override string ToString() => Value;
}
public sealed record Expiration(TimeSpan Duration);
public sealed record CacheValue(byte[] Data, string? ContentType = null) : IExistenceState
{
    public bool Exists => true;
}

public interface ICacheProvider
{
    ValueTask<CacheValue?> GetAsync(CacheKey key, CancellationToken cancellationToken = default);
    ValueTask SetAsync(CacheKey key, CacheValue value, Expiration? expiration = null, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(CacheKey key, CancellationToken cancellationToken = default);
}

public sealed class MemoryCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, Item> _items = new(StringComparer.Ordinal);
    public ValueTask<CacheValue?> GetAsync(CacheKey key, CancellationToken cancellationToken = default)
    {
        if (!_items.TryGetValue(key.Value, out Item? item))
            return ValueTask.FromResult<CacheValue?>(null);
        if (item.ExpiresAt is { } expires && expires <= DateTimeOffset.UtcNow)
        {
            _items.TryRemove(key.Value, out _);
            return ValueTask.FromResult<CacheValue?>(null);
        }
        return ValueTask.FromResult<CacheValue?>(item.Value);
    }
    public ValueTask SetAsync(CacheKey key, CacheValue value, Expiration? expiration = null, CancellationToken cancellationToken = default)
    {
        _items[key.Value] = new(value, expiration is null ? null : DateTimeOffset.UtcNow + expiration.Duration);
        return ValueTask.CompletedTask;
    }
    public ValueTask<bool> DeleteAsync(CacheKey key, CancellationToken cancellationToken = default) => ValueTask.FromResult(_items.TryRemove(key.Value, out _));
    private sealed record Item(CacheValue Value, DateTimeOffset? ExpiresAt);
}

public sealed class CacheModule : LanguageModule
{
    public override string Name => "cache";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:cache", "CACHE", typeof(CacheValue)),
        new("qualifier:cache-content-type", "CONTENTTYPE", typeof(string)),
        new("qualifier:cache-exists", "EXISTS", typeof(bool)),
        new("qualifier:cache-data", "DATA", typeof(byte[])),
        new("qualifier:cache-duration", "DURATION", typeof(TimeSpan))
    };
}

[Verb("GET")]
[Qualifier("CONTENTTYPE")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCacheContentType : Get<string?, CacheValue>
{
    public GetCacheContentType([From] CacheValue from) : base(from) { }

    protected override ValueTask<string?> ActAsync(CacheValue from, CancellationToken cancellationToken) => ValueTask.FromResult(from.ContentType);
}

[Verb("GET")]
[Qualifier("EXISTS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCacheExistence : Get<bool, CacheValue>
{
    public GetCacheExistence([From] CacheValue from) : base(from) { }

    protected override ValueTask<bool> ActAsync(CacheValue from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Exists);
}

[Verb("GET")]
[Qualifier("DATA")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCacheData : Get<byte[], CacheValue>
{
    public GetCacheData([From] CacheValue from) : base(from) { }

    protected override ValueTask<byte[]> ActAsync(CacheValue from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Data);
}

[Verb("GET")]
[Qualifier("DURATION")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetExpirationDuration : Get<TimeSpan, Expiration>
{
    public GetExpirationDuration([From] Expiration from) : base(from) { }

    protected override ValueTask<TimeSpan> ActAsync(Expiration from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Duration);
}

[Verb("GET")]
[Qualifier("CACHE")]
[RequiresCapability(StandardCapabilities.CacheRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetCache : IVerb<CacheValue?>, IGet, IFrom<CacheKey>, IPipelineProducer<CacheValue?>
{
    private readonly CacheKey _key; private readonly ICacheProvider _provider;
    public GetCache([From] CacheKey key, [FromServices] ICacheProvider provider)
    {
        _key = key;
        _provider = provider;
    }
    public ValueTask<CacheValue?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _provider.GetAsync(_key, cancellationToken);
}

[Verb("SAVE")]
[Qualifier("CACHE")]
[RequiresCapability(StandardCapabilities.CacheWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveCache : IVerb<CacheValue>, ISave, IWhat<CacheValue>, ITo<CacheKey>, IWith<Expiration>, IPipelineConsumer<CacheValue>, IPipelineProducer<CacheValue>
{
    private readonly CacheValue _value; private readonly CacheKey _key; private readonly Expiration? _expiration; private readonly ICacheProvider _provider;
    public SaveCache([What] CacheValue value, [To] CacheKey key, [With] Expiration? expiration = null, [FromServices] ICacheProvider provider = null!)
    {
        _value = value;
        _key = key;
        _expiration = expiration;
        _provider = provider;
    }
    public async ValueTask<CacheValue> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _provider.SetAsync(_key, _value, _expiration, cancellationToken).ConfigureAwait(false);
        return _value;
    }
}

[Verb("DELETE")]
[Qualifier("CACHE")]
[RequiresCapability(StandardCapabilities.CacheWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class DeleteCache : IVerb<bool>, IDelete, IAt<CacheKey>, IPipelineProducer<bool>
{
    private readonly CacheKey _key; private readonly ICacheProvider _provider;
    public DeleteCache([At] CacheKey key, [FromServices] ICacheProvider provider)
    {
        _key = key;
        _provider = provider;
    }
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _provider.DeleteAsync(_key, cancellationToken);
}
