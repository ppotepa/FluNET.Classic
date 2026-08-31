using FluNET.Classic.Standard.Process;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ProcessSpecValidationTests
{
    [Test]
    public void Constructor_rejects_empty_file_names()
    {
        Assert.That(() => new ProcessSpec("  "), Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void Constructor_rejects_negative_timeouts()
    {
        Assert.That(() => new ProcessSpec("dotnet", Timeout: TimeSpan.FromSeconds(-1)), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Constructor_snapshots_argument_and_environment_collections()
    {
        var arguments = new List<string> { "--version" };
        var environment = new Dictionary<string, string?> { ["MODE"] = "test" };
        var spec = new ProcessSpec("dotnet", Environment: environment, ArgumentList: arguments);

        arguments[0] = "mutated";
        environment["MODE"] = "mutated";

        Assert.That(spec.ArgumentList, Is.EqualTo(new[] { "--version" }));
        Assert.That(spec.Environment!["MODE"], Is.EqualTo("test"));
    }
}
