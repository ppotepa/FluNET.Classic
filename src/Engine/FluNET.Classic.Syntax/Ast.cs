namespace FluNET.Classic.Syntax;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;
    public static TextSpan FromBounds(int start, int end) => new(start, Math.Max(0, end - start));
}

public abstract record SyntaxNode(TextSpan Span);
public sealed record ScriptNode(IReadOnlyList<StatementNode> Statements, TextSpan Span) : SyntaxNode(Span);
public abstract record StatementNode(TextSpan Span) : SyntaxNode(Span);
public sealed record BlockNode(IReadOnlyList<StatementNode> Statements, TextSpan Span) : SyntaxNode(Span);
public sealed record CommentStatementNode(string Text, TextSpan Span) : StatementNode(Span);

public sealed record PipelineNode(IReadOnlyList<PipelineStageNode> Stages, TextSpan Span) : StatementNode(Span);
public abstract record PipelineStageNode(TextSpan Span) : SyntaxNode(Span);

public sealed record SentenceNode(string Verb, string? Qualifier, IReadOnlyList<ClauseNode> Clauses, string? ResultAlias, TextSpan Span) : PipelineStageNode(Span);
public sealed record FilterStageNode(ExpressionNode? Source, ExpressionNode Predicate, string? ResultAlias, TextSpan Span) : PipelineStageNode(Span);
public sealed record CheckStageNode(ExpressionNode Condition, string? ResultAlias, TextSpan Span) : PipelineStageNode(Span);
public sealed record CollectionStageNode(string Operation, ExpressionNode? Source, ExpressionNode? Argument, ExpressionNode? Strategy, string? ResultAlias, TextSpan Span) : PipelineStageNode(Span);
public sealed record ClauseNode(string RoleName, IReadOnlyList<ExpressionNode> Values, TextSpan Span) : SyntaxNode(Span);

public sealed record IfNode(ExpressionNode Condition, BlockNode Then, BlockNode? Else, TextSpan Span) : StatementNode(Span);
public sealed record ForEachNode(string Variable, ExpressionNode Source, int? Parallelism, BlockNode Body, TextSpan Span) : StatementNode(Span);
public sealed record TryNode(BlockNode Body, BlockNode? Failure, BlockNode? Finally, TextSpan Span) : StatementNode(Span);
public enum DefinitionKind { Task, Function }
public sealed record DefinitionParameterNode(string RoleName, string Name, string TypeName, TextSpan Span) : SyntaxNode(Span);
public sealed record DefinitionNode(DefinitionKind Kind, string Name, string? Qualifier, IReadOnlyList<DefinitionParameterNode> Parameters, string ReturnTypeName, BlockNode Body, TextSpan Span) : StatementNode(Span);
public sealed record ReturnNode(ExpressionNode? Value, TextSpan Span) : StatementNode(Span);
public sealed record RecordFieldNode(string Name, string TypeName, TextSpan Span) : SyntaxNode(Span);
public sealed record RecordDefinitionNode(string Name, IReadOnlyList<RecordFieldNode> Fields, TextSpan Span) : StatementNode(Span);

public abstract record ExpressionNode(TextSpan Span) : SyntaxNode(Span);
public sealed record LiteralExpression(object? Value, TextSpan Span) : ExpressionNode(Span);
public sealed record VariableExpression(string Name, TextSpan Span) : ExpressionNode(Span);
public sealed record ReferenceExpression(string Value, TextSpan Span) : ExpressionNode(Span);
public sealed record IdentifierExpression(string Name, TextSpan Span) : ExpressionNode(Span);
public sealed record PropertyExpression(ExpressionNode Target, string Property, TextSpan Span) : ExpressionNode(Span);
public sealed record InterpolatedStringExpression(IReadOnlyList<ExpressionNode> Parts, TextSpan Span) : ExpressionNode(Span);
public sealed record UnaryExpression(string Operator, ExpressionNode Operand, TextSpan Span) : ExpressionNode(Span);
public sealed record PredicateExpression(string Predicate, ExpressionNode Operand, TextSpan Span) : ExpressionNode(Span);
public sealed record BinaryExpression(ExpressionNode Left, string Operator, ExpressionNode Right, TextSpan Span) : ExpressionNode(Span);
public sealed record BetweenExpression(string Operator, ExpressionNode Operand, ExpressionNode Lower, ExpressionNode Upper, TextSpan Span) : ExpressionNode(Span);
