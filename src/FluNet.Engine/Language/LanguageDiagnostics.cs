namespace FluNET.Language;

public enum LanguageDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record LanguageDiagnostic(
    string Code,
    string Message,
    LanguageDiagnosticSeverity Severity,
    Type? SourceType = null);

public sealed record LanguageBuildResult(
    LanguageSnapshot? Snapshot,
    IReadOnlyList<LanguageDiagnostic> Diagnostics)
{
    public bool Success => Snapshot is not null && Diagnostics.All(d => d.Severity != LanguageDiagnosticSeverity.Error);

    public void ThrowIfFailed()
    {
        if (Success)
        {
            return;
        }

        string message = string.Join(
            Environment.NewLine,
            Diagnostics.Where(d => d.Severity == LanguageDiagnosticSeverity.Error)
                .Select(d => $"{d.Code}: {d.Message}"));

        throw new InvalidOperationException(message);
    }
}
