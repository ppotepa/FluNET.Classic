using System.Text.Json;

string mode = args.FirstOrDefault() ?? "arguments";
switch (mode.ToLowerInvariant())
{
    case "arguments":
        Console.WriteLine(JsonSerializer.Serialize(args.Skip(1).ToArray()));
        return 0;
    case "streams":
        Console.WriteLine("fixture stdout");
        Console.Error.WriteLine("fixture stderr");
        return 0;
    case "exit":
        return args.Length > 1 && int.TryParse(args[1], out int exitCode) ? exitCode : 64;
    case "sleep":
        await Task.Delay(TimeSpan.FromMilliseconds(args.Length > 1 && int.TryParse(args[1], out int milliseconds) ? milliseconds : 1000));
        return 0;
    case "environment":
        Console.WriteLine(Environment.GetEnvironmentVariable(args.Length > 1 ? args[1] : "FLU_FIXTURE_VALUE") ?? "<missing>");
        return 0;
    case "working-directory":
        Console.WriteLine(Environment.CurrentDirectory);
        return 0;
    default:
        Console.Error.WriteLine($"Unknown fixture mode '{mode}'.");
        return 64;
}
