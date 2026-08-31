using FluNET.Classic.Identity;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class IdentityProjectionTests
{
    [Test]
    public async Task Principal_projections_return_typed_fields()
    {
        var claims = new Dictionary<string, string[]> { ["role"] = new[] { "admin" } };
        var principal = new PrincipalInfo("ada", true, claims);

        Assert.That(await new GetPrincipalName(principal).ExecuteAsync(null!), Is.EqualTo("ada"));
        Assert.That(await new GetPrincipalAuthentication(principal).ExecuteAsync(null!), Is.True);
        Assert.That(await new GetPrincipalClaims(principal).ExecuteAsync(null!), Is.SameAs(claims));
    }
}
