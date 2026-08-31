using System.Security.Claims;
using FluNET.Classic.Identity;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class IdentityQueryTests
{
    [Test]
    public async Task Context_query_uses_a_service_only_constructor_and_produces_a_pipeline_value()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new IdentityModule());

        using ServiceProvider host = FluNetHost.Create(options, services =>
            services.AddSingleton<IPrincipalProvider>(new TestPrincipalProvider()));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("GET PRINCIPAL INTO [principal].");
        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));

        RuntimeResult result = await engine.RunAsync("GET PRINCIPAL INTO [principal].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["principal"], Is.TypeOf<PrincipalInfo>());
        Assert.That(((PrincipalInfo)result.State.Variables["principal"]!).Name, Is.EqualTo("ada"));
    }

    private sealed class TestPrincipalProvider : IPrincipalProvider
    {
        public ClaimsPrincipal Current { get; } = new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ada") }, "test"));
    }
}
