using FluNET.Language;

namespace FluNET.Syntax.Ast;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
}

public abstract record SyntaxNode(TextSpan Span);

public sealed record ScriptNode(
    IReadOnlyList<PipelineNode> Pipelines,
    TextSpan Span) : SyntaxNode(Span);

public sealed record PipelineNode(
    IReadOnlyList<SentenceNode> Sentences,
    TextSpan Span) : SyntaxNode(Span);

public sealed record SentenceNode(
    string Verb,
    string? Qualifier,
    IReadOnlyList<ClauseNode> Clauses,
    TextSpan Span) : SyntaxNode(Span);

public sealed record ClauseNode(
    string RoleName,
    IReadOnlyList<ExpressionNode> Values,
    TextSpan Span) : SyntaxNode(Span);

public abstract record ExpressionNode(TextSpan Span) : SyntaxNode(Span);

public sealed record LiteralExpression(
    string Value,
    TextSpan Span) : ExpressionNode(Span);

public sealed record VariableExpression(
    string Name,
    TextSpan Span) : ExpressionNode(Span);

public sealed record ReferenceExpression(
    string Value,
    TextSpan Span) : ExpressionNode(Span);

public sealed record PropertyExpression(
    ExpressionNode Target,
    string Property,
    TextSpan Span) : ExpressionNode(Span);

public sealed record InterpolatedStringExpression(
    IReadOnlyList<ExpressionNode> Parts,
    TextSpan Span) : ExpressionNode(Span);
