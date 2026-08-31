namespace FluNET.Classic.Runtime;

public enum ExecutionEventKind
{
    RunStarted,
    RunCompleted,
    StageStarted,
    StageCompleted,
    BranchSelected,
    LoopStarted,
    LoopItem,
    LoopCompleted,
    TryStarted,
    FailureHandled,
    FinallyCompleted,
    Diagnostic
}

public sealed record ExecutionEvent(
    int Sequence,
    ExecutionEventKind Kind,
    int Depth = 0,
    string? Verb = null,
    string? Implementation = null,
    bool? Success = null,
    int Attempts = 0,
    TimeSpan Duration = default,
    string? Detail = null,
    string? Error = null);

public interface IExecutionObserver
{
    ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default);
}

public sealed class NullExecutionObserver : IExecutionObserver
{
    public static NullExecutionObserver Instance { get; } = new();
    private NullExecutionObserver() { }
    public ValueTask OnEventAsync(ExecutionEvent executionEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
