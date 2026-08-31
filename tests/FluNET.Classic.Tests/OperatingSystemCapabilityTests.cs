using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.OS;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class OperatingSystemCapabilityTests
{
    [Test]
    public void Changing_working_directory_requires_system_write_capability()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();
        VerbImplementationDescriptor implementation = snapshot.Verbs
            .SelectMany(verb => verb.Implementations)
            .Single(item => item.ImplementationType == typeof(SaveWorkingDirectory));

        Assert.That(implementation.Capabilities, Does.Contain(StandardCapabilities.SystemWrite));
        Assert.That(implementation.Capabilities, Does.Not.Contain(StandardCapabilities.SystemRead));
    }

    [TestCase(" ")]
    [TestCase("\t")]
    public void Empty_environment_variable_names_are_rejected_during_binding(string name)
    {
        using ServiceProvider host = FluNetHost.Create();
        CheckResult result = host.GetRequiredService<ClassicEngine>().Check($"GET ENV {{{name}}} INTO [value].");

        Assert.That(result.Success, Is.False);
    }
}
