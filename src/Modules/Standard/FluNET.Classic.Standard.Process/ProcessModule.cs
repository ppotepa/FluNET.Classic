using FluNET.Classic.Core;
using System.ComponentModel;
using System.Diagnostics;

namespace FluNET.Classic.Standard.Process;

public sealed record CommandLine(string Value)
{
    public static bool TryParse(string value, out CommandLine? result)
    {
        result = new(value ?? string.Empty);
        return true;
    }
    public override string ToString() => Value;
}

public sealed record ProcessSpec(string FileName, string? Arguments = null, DirectoryInfo? WorkingDirectory = null, IReadOnlyDictionary<string, string?>? Environment = null, TimeSpan? Timeout = null)
{
    public static bool TryParse(string value, out ProcessSpec? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return false;
        }
        result = new(value.Trim());
        return true;
    }
}
public sealed record ProcessResult(int ExitCode, string StdOut, string StdErr, TimeSpan Duration) : IOkState
{
    public bool IsOk => ExitCode == 0;
}
public sealed record ProcessInfo(int Id, string Name);
public enum ProcessRunMode
{
    BACKGROUND
}
public enum ProcessStopMode
{
    GRACEFUL, FORCE
}
public sealed record ProcessHandle(int Id, ProcessSpec Spec, DateTimeOffset StartedAt) : IExistenceState
{
    public bool Exists
    {
        get
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(Id);
                return !process.HasExited;
            }
            catch (ArgumentException) { return false; }
        }
    }
}

public sealed class ProcessModule : LanguageModule
{
    public override string Name => "process";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:stdout", "STDOUT", typeof(string)), new("qualifier:stderr", "STDERR", typeof(string)), new("qualifier:exitcode", "EXITCODE", typeof(int)),
        new("qualifier:processes", "PROCESSES", typeof(ProcessInfo[])), new("qualifier:process-handle", "PROCESS", null)
    };
}

[Verb("CREATE")]
[Qualifier("PROCESS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class CreateProcessSpec : IVerb<ProcessSpec>, ICreate, IFrom<string>, IWith<CommandLine>, IPipelineProducer<ProcessSpec>
{
    private readonly string _fileName; private readonly CommandLine? _arguments;
    public CreateProcessSpec([From] string fileName, [With] CommandLine? arguments = null)
    {
        _fileName = fileName;
        _arguments = arguments;
    }
    public ValueTask<ProcessSpec> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new ProcessSpec(_fileName, _arguments?.Value));
}

[Verb("RUN")]
[RequiresCapability(StandardCapabilities.ProcessExecute)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class RunProcess : IVerb<ProcessResult>, IRun, IWhat<ProcessSpec>, IWith<string>, IPipelineProducer<ProcessResult>
{
    private readonly ProcessSpec _spec;
    private readonly string? _arguments;

    public RunProcess([What] ProcessSpec spec, [With, RoleAlias("ARGUMENTS")] string? arguments = null)
    {
        _spec = spec;
        _arguments = arguments;
    }

    public async ValueTask<ProcessResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var startInfo = BuildStartInfo(_spec, _arguments, redirect: true);
        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        Start(process, _spec.FileName);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource? timeout = _spec.Timeout is { } duration && duration > TimeSpan.Zero ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken) : null;
        if (timeout is not null)
            timeout.CancelAfter(_spec.Timeout!.Value);
        CancellationToken token = timeout?.Token ?? cancellationToken;
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout is not null && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
                process.Kill(true);
            throw new ExecutionFailureException("FLU-PROC-003", $"Process '{_spec.FileName}' exceeded its timeout.");
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);
            throw;
        }
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        stopwatch.Stop();
        return new(process.ExitCode, stdout.Result, stderr.Result, stopwatch.Elapsed);
    }
    internal static void Start(System.Diagnostics.Process process, string fileName)
    {
        try
        {
            if (!process.Start())
                throw new ExecutionFailureException("FLU-PROC-002", $"Could not start process '{fileName}'.");
        }
        catch (FileNotFoundException)
        {
            throw new ExecutionFailureException("FLU-PROC-002", $"Executable '{fileName}' was not found.");
        }
        catch (Win32Exception)
        {
            throw new ExecutionFailureException("FLU-PROC-002", $"Could not start executable '{fileName}'.");
        }
    }
    internal static ProcessStartInfo BuildStartInfo(ProcessSpec spec, string? arguments, bool redirect)
    {
        var info = new ProcessStartInfo { FileName = spec.FileName, Arguments = arguments ?? spec.Arguments ?? string.Empty, UseShellExecute = false, RedirectStandardOutput = redirect, RedirectStandardError = redirect, CreateNoWindow = true };
        if (spec.WorkingDirectory is not null)
            info.WorkingDirectory = spec.WorkingDirectory.FullName;
        if (spec.Environment is not null)
            foreach ((string key, string? value) in spec.Environment)
            {
                if (value is null)
                    info.Environment.Remove(key);
                else
                    info.Environment[key] = value;
            }
        return info;
    }
}

[Verb("RUN")]
[Qualifier("PROCESS")]
[RequiresCapability(StandardCapabilities.ProcessExecute)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class RunBackgroundProcess : IVerb<ProcessHandle>, IRun, IWhat<ProcessSpec>, IUsing<ProcessRunMode>, IWith<string>, IPipelineProducer<ProcessHandle>
{
    private readonly ProcessSpec _spec;
    private readonly ProcessRunMode _mode;
    private readonly string? _arguments;

    public RunBackgroundProcess([What] ProcessSpec spec, [Using] ProcessRunMode mode, [With, RoleAlias("ARGUMENTS")] string? arguments = null)
    {
        _spec = spec;
        _mode = mode;
        _arguments = arguments;
    }
    public ValueTask<ProcessHandle> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (_mode != ProcessRunMode.BACKGROUND)
            throw new NotSupportedException(_mode.ToString());
        var process = new System.Diagnostics.Process { StartInfo = RunProcess.BuildStartInfo(_spec, _arguments, redirect: false), EnableRaisingEvents = false };
        RunProcess.Start(process, _spec.FileName);
        int id = process.Id;
        process.Dispose();
        return ValueTask.FromResult(new ProcessHandle(id, _spec, DateTimeOffset.UtcNow));
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
    public ListProcesses([What] ProcessInfo[] what)
    {
    }
    public ValueTask<ProcessInfo[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(System.Diagnostics.Process.GetProcesses().Select(x => { try { return new ProcessInfo(x.Id, x.ProcessName); } finally { x.Dispose(); } }).ToArray());
}

[Verb("STOP")]
[RequiresCapability(StandardCapabilities.ProcessTerminate)]
public sealed class StopProcess : IVerb<bool>, IStop, IWhat<ProcessInfo>, IPipelineConsumer<ProcessInfo>, IPipelineProducer<bool>
{
    private readonly ProcessInfo _process; public StopProcess([What] ProcessInfo process) => _process = process;
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (process.HasExited)
                return ValueTask.FromResult(false);
            process.Kill(true);
            return ValueTask.FromResult(true);
        }
        catch (ArgumentException) { return ValueTask.FromResult(false); }
    }
}

[Verb("STOP")]
[Qualifier("PROCESS")]
[RequiresCapability(StandardCapabilities.ProcessTerminate)]
public sealed class StopProcessHandle : IVerb<bool>, IStop, IWhat<ProcessHandle>, IUsing<ProcessStopMode>, IPipelineConsumer<ProcessHandle>, IPipelineProducer<bool>
{
    private readonly ProcessHandle _process; private readonly ProcessStopMode _mode;
    public StopProcessHandle([What] ProcessHandle process, [Using] ProcessStopMode mode = ProcessStopMode.FORCE)
    {
        _process = process;
        _mode = mode;
    }
    public async ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (process.HasExited)
                return false;
            if (_mode == ProcessStopMode.GRACEFUL && process.CloseMainWindow())
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
            process.Kill(true);
            return true;
        }
        catch (ArgumentException) { return false; }
    }
}

[Verb("WAIT")]
[RequiresCapability(StandardCapabilities.ProcessInspect)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class WaitForProcess : IVerb<ProcessInfo>, IWait, IFor<ProcessInfo>, IPipelineProducer<ProcessInfo>
{
    private readonly ProcessInfo _process; public WaitForProcess([For] ProcessInfo process) => _process = process;
    public async ValueTask<ProcessInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (!process.HasExited)
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException) { }
        return _process;
    }
}

[Verb("WAIT")]
[Qualifier("PROCESS")]
[RequiresCapability(StandardCapabilities.ProcessInspect)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class WaitForProcessHandle : IVerb<ProcessHandle>, IWait, IFor<ProcessHandle>, IPipelineProducer<ProcessHandle>
{
    private readonly ProcessHandle _process; public WaitForProcessHandle([For] ProcessHandle process) => _process = process;
    public async ValueTask<ProcessHandle> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.GetProcessById(_process.Id);
            if (!process.HasExited)
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException) { }
        return _process;
    }
}
