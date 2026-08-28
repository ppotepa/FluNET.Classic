using FluNET.Classic.Hosting;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ProjectManifestTests
{
    [Test]
    public void Loader_resolves_entry_and_validates_execution_contract()
    {
        string root = Path.Combine(Path.GetTempPath(), "flunet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "main.flu"), "SAY \"hello\".");
            File.WriteAllText(Path.Combine(root, "flu.json"), """
                {
                  "entry": "main.flu",
                  "sources": ["main.flu"],
                  "modules": { "standard": "1.0.0" },
                  "capabilities": ["filesystem.read"],
                  "execution": { "timeout": "00:00:05", "parallelism": 2 }
                }
                """);

            FluNetProject project = FluNetProjectLoader.Load(root);

            Assert.That(project.EntryFile, Is.EqualTo(Path.Combine(root, "main.flu")));
            Assert.That(project.SourceFiles, Has.Count.EqualTo(1));
            Assert.That(project.Manifest.Execution.Parallelism, Is.EqualTo(2));
            Assert.That(project.Manifest.Capabilities, Does.Contain("filesystem.read"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
