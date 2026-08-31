using System.Reflection;
using FluNET.Classic.Core;
using FluNET.Classic.Csv;
using FluNET.Classic.Storage;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ModuleDiscoveryTests
{
    [Test]
    public void Discovery_is_deterministic_and_returns_a_read_only_snapshot()
    {
        Assembly[] assemblies = { typeof(CsvModule).Assembly, typeof(StorageModule).Assembly };

        IReadOnlyList<ILanguageModule> first = ModuleDiscovery.Discover(assemblies);
        IReadOnlyList<ILanguageModule> second = ModuleDiscovery.Discover(assemblies.AsEnumerable().Reverse());

        Assert.That(first.Select(module => module.Name), Is.EqualTo(second.Select(module => module.Name)));
        Assert.That(() => ((IList<ILanguageModule>)first).Clear(), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Discovery_rejects_a_null_assembly_sequence()
    {
        Assert.That(() => ModuleDiscovery.Discover(null!), Throws.TypeOf<ArgumentNullException>());
    }
}
