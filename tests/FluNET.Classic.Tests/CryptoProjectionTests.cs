using FluNET.Classic.Crypto;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class CryptoProjectionTests
{
    [Test]
    public async Task Hash_projections_return_typed_fields()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var hash = new HashValue(bytes, HashAlgorithmKind.SHA256);

        Assert.That(await new GetHashBytes(hash).ExecuteAsync(null!), Is.SameAs(bytes));
        Assert.That(await new GetHashAlgorithm(hash).ExecuteAsync(null!), Is.EqualTo(HashAlgorithmKind.SHA256));
        Assert.That(await new GetHashValidity(hash).ExecuteAsync(null!), Is.True);
    }
}
