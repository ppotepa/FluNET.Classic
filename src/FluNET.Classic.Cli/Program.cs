using System.Text.Json;
using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;

var arguments = args.ToList();
var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
for (int i = arguments.Count - 1; i >= 0; i--)
{
    if (i > 0 && arguments[i - 1].Equals("--allow", StringComparison.OrdinalIgnoreCase))
    {
        allowed.Add(arguments[i]);
        arguments.RemoveAt(i);
        arguments.RemoveAt(i - 1);
        i--;
    }
}

var options = new FluNetOptions { AllowedCapabilities = allowed.Count == 0 ? null : allowed };
using ServiceProvider host = FluNetHost.Create(options);
ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
LanguageIntrospectionService introspection = host.GetRequiredService<LanguageIntrospectionService>();

if (arguments.Count == 0) return Usage();
string command = arguments[0].ToLowerInvariant();
string[] rest = arguments.Skip(1).ToArray();

switch (command)
{
    case "run":
    {
        string source = await ReadSource(rest);
        RuntimeResult result = await engine.RunAsync(source);
        foreach (RuntimeDiagnostic diagnostic in result.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        if (result.Success && result.Result is not null) Console.WriteLine(Format(result.Result));
        return result.Success ? 0 : 1;
    }
    case "check":
    {
        var result = engine.Check(await ReadSource(rest));
        foreach (var diagnostic in result.Parse.Diagnostics) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        foreach (var diagnostic in result.Bound?.Diagnostics ?? Array.Empty<FluNET.Classic.Binding.BindingDiagnostic>()) Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
        Console.WriteLine(result.Success ? "OK" : "FAILED");
        return result.Success ? 0 : 1;
    }
    case "explain": Console.WriteLine(engine.Explain(await ReadSource(rest))); return 0;
    case "verbs": foreach (VerbDescriptor verb in language.Verbs) Console.WriteLine(verb.Name); return 0;
    case "verb": if (rest.Length == 0) return Usage(); Console.WriteLine(introspection.DescribeVerb(rest[0])); return 0;
    case "qualifiers": foreach (QualifierDescriptor qualifier in language.Qualifiers) Console.WriteLine($"{qualifier.Name}\t{qualifier.TargetType?.Name ?? "-"}"); return 0;
    case "modules": foreach (ModuleDescriptor module in language.Modules) Console.WriteLine($"{module.Name}\t{module.Version}\tdeps=[{string.Join(',', module.Dependencies)}]"); return 0;
    case "language": Console.WriteLine(introspection.ToJson()); return 0;
    default: return Usage();
}

static async Task<string> ReadSource(string[] values)
{
    if (values.Length == 0) return await Console.In.ReadToEndAsync();
    if (values.Length == 1 && File.Exists(values[0])) return await File.ReadAllTextAsync(values[0]);
    return string.Join(' ', values);
}

static string Format(object value) => value is string text ? text : JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });

static int Usage()
{
    Console.Error.WriteLine("flu run|check|explain <file|script> [--allow capability]");
    Console.Error.WriteLine("flu verbs | verb GET | qualifiers | modules | language");
    return 2;
}
