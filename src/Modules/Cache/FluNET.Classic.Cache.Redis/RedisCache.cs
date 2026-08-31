using FluNET.Classic.Core;

namespace FluNET.Classic.Cache.Redis;

public interface IRedisClient
{
    ValueTask<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class RedisCacheProvider : ICacheProvider
{
    private readonly IRedisClient _client; public RedisCacheProvider(IRedisClient client) => _client = client;
    public async ValueTask<CacheValue?> GetAsync(CacheKey key, CancellationToken cancellationToken = default) => await _client.GetAsync(key.Value, cancellationToken).ConfigureAwait(false) is { } data ? new CacheValue(data) : null;
    public ValueTask SetAsync(CacheKey key, CacheValue value, Expiration? expiration = null, CancellationToken cancellationToken = default) => _client.SetAsync(key.Value, value.Data, expiration?.Duration, cancellationToken);
    public ValueTask<bool> DeleteAsync(CacheKey key, CancellationToken cancellationToken = default) => _client.DeleteAsync(key.Value, cancellationToken);
}

public sealed class RedisCacheModule : LanguageModule { public override string Name => "cache.redis"; public override IReadOnlyCollection<string> Dependencies => new[] { "cache" }; }
