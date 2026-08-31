using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class DemoValidationTests
{
    [Test]
    public async Task Every_demo_is_valid_against_the_production_engine()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "demo");
        string[] files = Directory.GetFiles(directory, "*.flu").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.That(files, Is.Not.Empty, $"No demo programs were copied to {directory}.");

        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        foreach (string file in files)
        {
            string source = await File.ReadAllTextAsync(file);
            CheckResult check = engine.Check(source);
            Assert.That(check.Success, Is.True, $"{Path.GetFileName(file)}: {string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>())}");

            string formatted = engine.Format(source);
            Assert.That(engine.Format(formatted), Is.EqualTo(formatted), $"Formatter is not idempotent for {Path.GetFileName(file)}.");
        }
    }
}
