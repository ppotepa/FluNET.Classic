using FluNET.Syntax.Ast;

namespace FluNET.Syntax.Parsing;

public enum ClassicTokenKind
{
    Word,
    Variable,
    Reference,
    String,
    NewLine,
    Semicolon,
    End
}

public readonly record struct ClassicToken(
    ClassicTokenKind Kind,
    string Value,
    TextSpan Span);

public sealed record SyntaxDiagnostic(
    string Code,
    string Message,
    TextSpan Span);

public sealed record ClassicParseResult(
    ScriptNode? Script,
    IReadOnlyList<SyntaxDiagnostic> Diagnostics)
{
    public bool Success => Script is not null && Diagnostics.Count == 0;
}
