using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

var original = args.ToList();
var arguments = new List<string>();
var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
bool denyByDefault = false;

for (int i = 0; i < original.Count; i++)
{
    if (original[i].Equals("--deny-by-default", StringComparison.OrdinalIgnoreCase))
    {
        denyByDefault = true;
        continue;
    }

    if (original[i].Equals("--allow", StringComparison.OrdinalIgnoreCase))
    {
        if (i + 1 >= original.Count)
        {
            Console.Error.WriteLine("--allow requires a capability name.");
            return 2;
        }
        allowed.Add(original[++i]);
        continue;
    }

    arguments.Add(original[i]);
}

if (arguments.Count == 0) return Usage();
string command = arguments[0].ToLowerInvariant();
string[] rest = arguments.Skip(1).ToArray();
FluNetProject? project = TryLoadProject(command, rest);
if (project is not null) foreach (string capability in project.Manifest.Capabilities) allowed.Add(capability);

var options = new FluNetOptions
{
    AllowedCapabilities = denyByDefault || allowed.Count > 0 ? allowed : null
};
if (project?.Manifest.Execution is { } execution)
{
    options.ConfigureExecution = policy =>
    {
        if (execution.Timeout is { } timeout)
            policy.DefaultTimeout = timeout;
        if (execution.Parallelism is { } parallelism)
            policy.MaxParallelism = parallelism;
    };
}

using ServiceProvider host = FluNetHost.Create(options);
ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
LanguageIntrospectionService introspection = host.GetRequiredService<LanguageIntrospectionService>();
var json = new JsonSerializerOptions { WriteIndented = true };

switch (command)
{
    case "run":
        {
            string source = await ReadSource(rest, project);
            RuntimeResult result = await engine.RunAsync(source);
            foreach (RuntimeDiagnostic diagnostic in result.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            if (result.Success && result.Result is not null)
                Console.WriteLine(Format(result.Result));
            return result.Success ? 0 : 1;
        }
    case "check":
        {
            var result = engine.Check(await ReadSource(rest, project));
            foreach (var diagnostic in result.Parse.Diagnostics)
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            foreach (var diagnostic in result.Bound?.Diagnostics ?? Array.Empty<FluNET.Classic.Binding.BindingDiagnostic>())
                Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            Console.WriteLine(result.Success ? "OK" : "FAILED");
            return result.Success ? 0 : 1;
        }
    case "format":
        Console.WriteLine(engine.Format(await ReadSource(rest, project)));
        return 0;
    case "plan":
        Console.WriteLine(JsonSerializer.Serialize(engine.Plan(await ReadSource(rest, project)), json));
        return 0;
    case "explain":
        Console.WriteLine(engine.Explain(await ReadSource(rest, project)));
        return 0;
    case "verbs":
        foreach (VerbDescriptor verb in language.Verbs)
            Console.WriteLine(verb.Name);
        return 0;
    case "verb":
        if (rest.Length == 0)
            return Usage();
        Console.WriteLine(introspection.DescribeVerb(rest[0]));
        return 0;
    case "qualifiers":
        foreach (QualifierDescriptor qualifier in language.Qualifiers)
            Console.WriteLine($"{qualifier.Name}\t{qualifier.TargetType?.Name ?? "-"}");
        return 0;
    case "modules":
        foreach (ModuleDescriptor module in language.Modules)
            Console.WriteLine($"{module.Name}\t{module.Version}\tdeps=[{string.Join(',', module.Dependencies)}]");
        return 0;
    case "language":
        Console.WriteLine(introspection.ToJson());
        return 0;
    default:
        return Usage();
}

static async Task<string> ReadSource(string[] values, FluNetProject? project)
{
    if (project is not null)
        return await File.ReadAllTextAsync(project.EntryFile);
    if (values.Length == 0)
        return await Console.In.ReadToEndAsync();
    if (values.Length == 1 && File.Exists(values[0]))
        return await File.ReadAllTextAsync(values[0]);
    return string.Join(' ', values);
}

static FluNetProject? TryLoadProject(string command, string[] values)
{
    if (command is not ("run" or "check" or "format" or "plan" or "explain"))
        return null;
    string? candidate = values.Length == 1 && (Directory.Exists(values[0]) || Path.GetFileName(values[0]).Equals(FluNetProjectLoader.ManifestFileName, StringComparison.OrdinalIgnoreCase))
        ? values[0]
        : values.Length == 0 && File.Exists(Path.Combine(Directory.GetCurrentDirectory(), FluNetProjectLoader.ManifestFileName))
            ? Directory.GetCurrentDirectory()
            : null;
    return candidate is null ? null : FluNetProjectLoader.Load(candidate);
}

static string Format(object value) => value is string text ? text : JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });

static int Usage()
{
    Console.Error.WriteLine("fluc run|check|format|plan|explain <file|script> [--deny-by-default] [--allow capability]");
    Console.Error.WriteLine("fluc verbs | verb GET | qualifiers | modules | language");
    return 2;
}
