using System.Diagnostics;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Process;

public sealed record ProcessSpec(string FileName);
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration) : IOkState
{
    public bool IsOk => ExitCode == 0;
}
public sealed record ProcessInfo(int Id, string Name);

public sealed class ProcessModule : LanguageModule
{
    public override string Name => "process";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:stdout", "STDOUT", typeof(string)),
        new("qualifier:stderr", "STDERR", typeof(string)),
        new("qualifier:exitcode", "EXITCODE", typeof(int)),
        new("qualifier:processes", "PROCESSES", typeof(ProcessInfo[]))
    };
}

[Verb("RUN")]
[RequiresCapability(StandardCapabilities.ProcessExecute)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class RunProcess : IVerb<ProcessResult>, IRun, IWhat<ProcessSpec>, IWith<string>, IPipelineProducer<ProcessResult>
{
    private readonly ProcessSpec _spec;
    private readonly string? _arguments;
    public RunProcess([What] ProcessSpec spec, [With] string? arguments = null) { _spec = spec; _arguments = arguments; }

    public async ValueTask<ProcessResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _spec.FileName,
            Arguments = _arguments ?? string.Empty,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        if (!process.Start()) throw new InvalidOperationException($"Could not start process '{_spec.FileName}'.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        stopwatch.Stop();
        return new ProcessResult(process.ExitCode, stdout.Result, stderr.Result, stopwatch.Elapsed);
    }
}

[Qualifier("STDOUT")]
public sealed class GetStandardOutput : Get<string, ProcessResult>
{
    public GetStandardOutput([What] string what, [From] ProcessResult from) : base(what, from) { }
    protected override ValueTask<string> ActAsync(ProcessResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.StdOut);
}

[Qualifier("STDERR")]
public sealed class GetStandardError : Get<string, ProcessResult>
{
    public GetStandardError([What] string what, [From] ProcessResult from) : base(what, from) { }
    protected override ValueTask<string> ActAsync(ProcessResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.StdErr);
}

[Qualifier("EXITCODE")]
public sealed class GetExitCode : Get<int, ProcessResult>
{
    public GetExitCode([What] int what, [From] ProcessResult from) : base(what, from) { }
    protected override ValueTask<int> ActAsync(ProcessResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.ExitCode);
}

[Verb("LIST")]
[Qualifier("PROCESSES")]
[RequiresCapability(StandardCapabilities.ProcessInspect)]
public sealed class ListProcesses : IVerb<ProcessInfo[]>, IListVerb, IWhat<ProcessInfo[]>, IPipelineProducer<ProcessInfo[]>
{
    public ListProcesses([What] ProcessInfo[] what) { }
    public ValueTask<ProcessInfo[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(System.Diagnostics.Process.GetProcesses().Select(x => new ProcessInfo(x.Id, x.ProcessName)).ToArray());
}

[Verb("STOP")]
[RequiresCapability(StandardCapabilities.ProcessTerminate)]
public sealed class StopProcess : IVerb<bool>, IStop, IWhat<ProcessInfo>, IPipelineConsumer<ProcessInfo>, IPipelineProducer<bool>
{
    private readonly ProcessInfo _process;
    public StopProcess([What] ProcessInfo process) => _process = process;
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (process.HasExited) return ValueTask.FromResult(false);
            process.Kill(entireProcessTree: true);
            return ValueTask.FromResult(true);
        }
        catch (ArgumentException)
        {
            return ValueTask.FromResult(false);
        }
    }
}

[Verb("WAIT")]
[RequiresCapability(StandardCapabilities.ProcessInspect)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class WaitForProcess : IVerb<ProcessInfo>, IWait, IFor<ProcessInfo>, IPipelineProducer<ProcessInfo>
{
    private readonly ProcessInfo _process;
    public WaitForProcess([For] ProcessInfo process) => _process = process;
    public async ValueTask<ProcessInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (!process.HasExited) await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            // A missing process is already in the desired completed state.
        }
        return _process;
    }
}
