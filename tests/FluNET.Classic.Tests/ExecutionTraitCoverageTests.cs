using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Standard.Http;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ExecutionTraitCoverageTests
{
    [Test]
    public void Http_operations_with_network_capability_are_long_running()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();

        string[] missing = snapshot.Verbs
            .SelectMany(verb => verb.Implementations)
            .Where(implementation => implementation.ImplementationType.Namespace == typeof(HttpModule).Namespace)
            .Where(implementation => implementation.Capabilities.Contains(StandardCapabilities.Network, StringComparer.OrdinalIgnoreCase)
                || implementation.Capabilities.Contains(StandardCapabilities.NetworkHttp, StringComparer.OrdinalIgnoreCase))
            .Where(implementation => !implementation.Traits.Contains(ExecutionTrait.LongRunning))
            .Select(implementation => implementation.ImplementationType.Name)
            .ToArray();

        Assert.That(missing, Is.Empty, "Network-backed HTTP operations must opt into LongRunning timeout policy.");
    }

    [Test]
    public void Non_idempotent_http_operations_are_not_retryable()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();
        Type[] nonIdempotent = { typeof(SendHttpRequest), typeof(PostJsonResponse) };

        foreach (Type type in nonIdempotent)
        {
            VerbImplementationDescriptor implementation = snapshot.Verbs
                .SelectMany(verb => verb.Implementations)
                .Single(item => item.ImplementationType == type);
            Assert.That(implementation.Traits, Does.Not.Contain(ExecutionTrait.Retryable), type.Name);
        }
    }
}
