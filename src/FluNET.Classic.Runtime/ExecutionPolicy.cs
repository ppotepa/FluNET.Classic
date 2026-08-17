using FluNET.Classic.Core;

namespace FluNET.Classic.Runtime;

public sealed class ExecutionPolicy
{
    public int RetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan? DefaultTimeout { get; set; }
    public TimeSpan? LongRunningTimeout { get; set; }
    public bool RequireTransactionCoordinatorForTransactional { get; set; }

    public int AttemptsFor(IReadOnlyList<ExecutionTrait> traits) => traits.Contains(ExecutionTrait.Retryable) ? Math.Max(1, RetryAttempts) : 1;
    public TimeSpan? TimeoutFor(IReadOnlyList<ExecutionTrait> traits) => traits.Contains(ExecutionTrait.LongRunning) ? LongRunningTimeout ?? DefaultTimeout : DefaultTimeout;
}

public sealed record ExecutionTraceEntry(
    int Sequence,
    string Kind,
    string? Verb,
    string? Implementation,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    bool Success,
    int Attempts,
    string? ResultType,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<ExecutionTrait> Traits,
    string? Error = null);

public static class SensitiveValueFormatter
{
    public static string Format(object? value) => value switch
    {
        null => "null",
        ISensitiveValue sensitive => sensitive.RedactedText,
        _ => value.ToString() ?? string.Empty
    };
}
