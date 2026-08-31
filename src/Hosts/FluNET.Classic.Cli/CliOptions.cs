using FluNET.Classic.Runtime;

internal enum VerbosityLevel
{
    Normal, Summary, Stages, Trace
}
internal enum ColorMode
{
    Auto, Always, Never
}

internal sealed record CliOptions(
    IReadOnlyList<string> Arguments,
    VerbosityLevel Verbosity,
    ColorMode Color,
    bool DenyByDefault,
    IReadOnlySet<string> AllowedCapabilities);

internal static class CliArgumentParser
{
    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        var arguments = new List<string>();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verbosity = VerbosityLevel.Normal;
        var color = ColorMode.Auto;
        bool deny = false;
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            string value = args[i];
            if (value.Equals("--deny-by-default", StringComparison.OrdinalIgnoreCase))
            {
                deny = true;
                continue;
            }
            if (value.Equals("--allow", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length)
                {
                    error = "--allow requires a capability name.";
                    break;
                }
                allowed.Add(args[i]);
                continue;
            }
            if (value.Equals("-v", StringComparison.OrdinalIgnoreCase) || value.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = verbosity < VerbosityLevel.Summary ? VerbosityLevel.Summary : verbosity;
                continue;
            }
            if (value.Equals("-vv", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = verbosity < VerbosityLevel.Stages ? VerbosityLevel.Stages : verbosity;
                continue;
            }
            if (value.Equals("-vvv", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = VerbosityLevel.Trace;
                continue;
            }
            if (value.Equals("--color", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length)
                {
                    error = "--color requires auto, always, or never.";
                    break;
                }
                if (!Enum.TryParse(args[i], true, out color))
                {
                    error = $"Unknown color mode '{args[i]}'. Use auto, always, or never.";
                    break;
                }
                continue;
            }
            if (value.StartsWith("-", StringComparison.Ordinal) && value is not "-")
            {
                error = $"Unknown option '{value}'.";
                break;
            }
            arguments.Add(value);
        }

        options = new(arguments, verbosity, color, deny, allowed);
        return error is null;
    }
}

internal sealed class ConsoleExecutionObserver : IExecutionObserver
{
    private readonly TextWriter _writer;
    private readonly VerbosityLevel _verbosity;
    private readonly bool _color;
    private readonly object _gate = new();

    public ConsoleExecutionObserver(TextWriter writer, VerbosityLevel verbosity, ColorMode color, bool redirected)
    {
        _writer = writer;
        _verbosity = verbosity;
        _color = color switch
        {
            ColorMode.Always => true,
            ColorMode.Never => false,
            _ => !redirected && Environment.GetEnvironmentVariable("NO_COLOR") is null
        };
    }

    public ValueTask OnEventAsync(ExecutionEvent item, CancellationToken cancellationToken = default)
    {
        if (_verbosity == VerbosityLevel.Normal)
            return ValueTask.CompletedTask;
        if (_verbosity == VerbosityLevel.Summary && item.Kind is not (ExecutionEventKind.RunStarted or ExecutionEventKind.RunCompleted or ExecutionEventKind.Diagnostic))
            return ValueTask.CompletedTask;
        if (_verbosity == VerbosityLevel.Stages && item.Kind is ExecutionEventKind.LoopItem)
            return ValueTask.CompletedTask;
        string line = item.Kind switch
        {
            ExecutionEventKind.RunStarted => $"[run] started{Suffix(item.Detail)}",
            ExecutionEventKind.RunCompleted when item.Success == true => $"[ok] run completed in {item.Duration.TotalMilliseconds:0} ms",
            ExecutionEventKind.RunCompleted => $"[error] run failed{Suffix(item.Error)}",
            ExecutionEventKind.StageStarted => $"-> {item.Verb ?? item.Detail ?? "stage"}{(_verbosity == VerbosityLevel.Trace && item.Implementation is not null ? $" [{item.Implementation}]" : string.Empty)}",
            ExecutionEventKind.StageCompleted when item.Success == true => $"  [ok] {item.Detail ?? "stage"} ({item.Duration.TotalMilliseconds:0} ms){(_verbosity == VerbosityLevel.Trace && item.Attempts > 1 ? $", attempts={item.Attempts}" : string.Empty)}",
            ExecutionEventKind.StageCompleted => $"  [error] {item.Detail ?? "stage"}: {item.Error}",
            ExecutionEventKind.LoopStarted => $"[loop] {item.Detail ?? "loop"}",
            ExecutionEventKind.LoopCompleted => $"  [ok] loop completed",
            ExecutionEventKind.LoopItem => $"  .. iteration{Suffix(item.Detail)}",
            ExecutionEventKind.BranchSelected => $"[branch] {item.Detail}",
            ExecutionEventKind.TryStarted => "[try] block",
            ExecutionEventKind.FailureHandled => $"[warning] failure handled{Suffix(item.Detail)}",
            ExecutionEventKind.FinallyCompleted => "  [ok] finally block",
            ExecutionEventKind.Diagnostic => $"[error] {item.Error ?? item.Detail}",
            _ => item.Detail ?? item.Kind.ToString()
        };
        lock (_gate)
            _writer.WriteLine(Paint(line, item));
        return ValueTask.CompletedTask;
    }

    private string Paint(string line, ExecutionEvent item)
    {
        if (!_color)
            return line;
        string code = item.Kind switch
        {
            ExecutionEventKind.RunCompleted when item.Success == true => "32",
            ExecutionEventKind.StageCompleted when item.Success == true => "32",
            ExecutionEventKind.FinallyCompleted => "32",
            ExecutionEventKind.StageCompleted or ExecutionEventKind.RunCompleted or ExecutionEventKind.Diagnostic => "31",
            ExecutionEventKind.FailureHandled => "33",
            ExecutionEventKind.StageStarted or ExecutionEventKind.RunStarted => "36",
            _ => "90"
        };
        return $"\u001b[{code}m{line}\u001b[0m";
    }

    private static string Suffix(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $": {value}";

    public void WriteDiagnostic(string code, string message, bool warning = false)
    {
        string line = $"[{(warning ? "warning" : "error")}] {code}: {message}";
        lock (_gate)
            _writer.WriteLine(_color ? $"\u001b[{(warning ? "33" : "31")}m{line}\u001b[0m" : line);
    }
}
