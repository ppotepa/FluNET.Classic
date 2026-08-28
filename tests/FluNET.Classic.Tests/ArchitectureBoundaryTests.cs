using System.Xml.Linq;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly HashSet<string> Layers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Engine", "Modules", "Tooling", "Hosts"
    };

    [Test]
    public void Every_source_project_belongs_to_one_architectural_layer()
    {
        string root = RepositoryRoot();
        string src = Path.Combine(root, "src");

        foreach (string project in Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(src, Path.GetDirectoryName(project)!);
            string layer = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            Assert.That(Layers.Contains(layer), Is.True, $"Project is outside a recognized layer: {project}");
        }
    }

    [Test]
    public void Project_references_follow_the_layer_dependency_direction()
    {
        string root = RepositoryRoot();
        string src = Path.Combine(root, "src");
        string[] projects = Directory.GetFiles(src, "*.csproj", SearchOption.AllDirectories);

        foreach (string project in projects)
        {
            string layer = LayerOf(src, project);
            XElement document = XDocument.Load(project).Root!;
            foreach (XElement reference in document.Descendants("ProjectReference"))
            {
                string include = reference.Attribute("Include")?.Value
                    ?? throw new AssertionException($"Project reference has no Include: {project}");
                string target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, include));
                Assert.That(File.Exists(target), Is.True, $"Broken project reference from {project}: {include}");

                string targetLayer = LayerOf(src, target);
                bool permitted = layer switch
                {
                    "Engine" => targetLayer == "Engine",
                    "Modules" => targetLayer is "Engine" or "Modules",
                    "Tooling" => targetLayer == "Engine",
                    "Hosts" => targetLayer is "Engine" or "Modules" or "Tooling" or "Hosts",
                    _ => false
                };

                Assert.That(permitted, Is.True, $"Forbidden dependency: {layer} project {project} references {targetLayer} project {target}");
            }
        }
    }

    private static string LayerOf(string src, string project)
    {
        string relative = Path.GetRelativePath(src, project);
        string layer = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        Assert.That(Layers.Contains(layer), Is.True, $"Project is outside a recognized layer: {project}");
        return layer;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FluNET.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new AssertionException("Could not locate FluNET.sln from the test directory.");
    }
}
