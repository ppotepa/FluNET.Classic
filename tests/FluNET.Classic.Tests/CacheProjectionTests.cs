using FluNET.Classic.Cache;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class CacheProjectionTests
{
    [Test]
    public async Task Cache_value_and_expiration_projections_return_typed_fields()
    {
        var value = new CacheValue(new byte[] { 1, 2, 3 }, "application/octet-stream");
        var expiration = new Expiration(TimeSpan.FromMinutes(5));

        Assert.That(await new GetCacheContentType(value).ExecuteAsync(null!), Is.EqualTo("application/octet-stream"));
        Assert.That(await new GetCacheExistence(value).ExecuteAsync(null!), Is.True);
        Assert.That(await new GetCacheData(value).ExecuteAsync(null!), Is.EqualTo(new byte[] { 1, 2, 3 }));
        Assert.That(await new GetExpirationDuration(expiration).ExecuteAsync(null!), Is.EqualTo(expiration.Duration));
    }
}
