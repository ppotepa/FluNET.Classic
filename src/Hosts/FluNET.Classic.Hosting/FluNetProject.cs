using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluNET.Classic.Hosting;

/// <summary>Project configuration for a FluNET application.</summary>
public sealed class FluNetProjectManifest
{
    public string? Entry { get; set; }
    public IList<string> Sources { get; set; } = [];
    public IDictionary<string, string> Modules { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IList<string> Capabilities { get; set; } = [];
    public FluNetProjectExecutionOptions Execution { get; set; } = new();
}

public sealed class FluNetProjectExecutionOptions
{
    public TimeSpan? Timeout { get; set; }
    public int? Parallelism { get; set; }
}

public sealed record FluNetProject(string RootDirectory, string ManifestPath, FluNetProjectManifest Manifest, IReadOnlyList<string> SourceFiles)
{
    public string EntryFile => Path.Combine(RootDirectory, Manifest.Entry!);
}

public static class FluNetProjectLoader
{
    public const string ManifestFileName = "flu.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static FluNetProject Load(string path)
    {
        string manifestPath = ResolveManifestPath(path);
        string root = Path.GetDirectoryName(manifestPath)!;
        FluNetProjectManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<FluNetProjectManifest>(File.ReadAllText(manifestPath), JsonOptions)
                ?? throw new InvalidDataException("The manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid FluNET manifest '{manifestPath}': {exception.Message}", exception);
        }

        manifest.Execution ??= new FluNetProjectExecutionOptions();
        manifest.Modules ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(manifest.Entry)) throw new InvalidDataException("flu.json requires an 'entry' file.");
        if (manifest.Execution.Parallelism is <= 0) throw new InvalidDataException("execution.parallelism must be greater than zero.");
        if (manifest.Execution.Timeout is { } timeout && timeout <= TimeSpan.Zero) throw new InvalidDataException("execution.timeout must be greater than zero.");

        string entry = ResolveProjectPath(root, manifest.Entry, "entry");
        var sources = new List<string> { entry };
        foreach (string source in manifest.Sources ?? [])
        {
            string resolved = ResolveProjectPath(root, source, "source");
            if (!sources.Contains(resolved, StringComparer.OrdinalIgnoreCase)) sources.Add(resolved);
        }

        foreach ((string name, string version) in manifest.Modules ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Module names cannot be empty.");
            if (string.IsNullOrWhiteSpace(version)) throw new InvalidDataException($"Module '{name}' must specify a package version.");
        }

        return new(root, manifestPath, manifest, sources);
    }

    public static string Find(string? path = null)
    {
        string candidate = Path.GetFullPath(path ?? Directory.GetCurrentDirectory());
        if (File.Exists(candidate) && Path.GetFileName(candidate).Equals(ManifestFileName, StringComparison.OrdinalIgnoreCase)) return candidate;
        string current = Directory.Exists(candidate) ? candidate : Path.GetDirectoryName(candidate)!;
        while (!string.IsNullOrEmpty(current))
        {
            string manifest = Path.Combine(current, ManifestFileName);
            if (File.Exists(manifest)) return manifest;
            string? parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
            current = parent;
        }
        throw new FileNotFoundException("Could not find flu.json.", path ?? Directory.GetCurrentDirectory());
    }

    private static string ResolveManifestPath(string path)
    {
        string found = Find(path);
        return Path.GetFullPath(File.Exists(found) ? found : Path.Combine(found, ManifestFileName));
    }

    private static string ResolveProjectPath(string root, string relativePath, string kind)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"{kind} path must stay inside the project: '{relativePath}'.");
        string resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && !string.Equals(resolved, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{kind} path must stay inside the project: '{relativePath}'.");
        if (!File.Exists(resolved)) throw new FileNotFoundException($"FluNET {kind} file was not found.", resolved);
        return resolved;
    }
}
