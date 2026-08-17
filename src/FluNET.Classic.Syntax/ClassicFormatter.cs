using System.Globalization;
using System.Text;

namespace FluNET.Classic.Syntax;

public sealed class ClassicFormatter
{
    public string Format(ScriptNode script) => string.Join(Environment.NewLine, script.Statements.Select(FormatStatement));
    private string FormatStatement(StatementNode statement) => statement switch { PipelineNode pipeline => FormatPipeline(pipeline) + ".", IfNode conditional => FormatIf(conditional), ForEachNode loop => FormatForEach(loop), _ => string.Empty };
    private string FormatPipeline(PipelineNode pipeline) { if (pipeline.Stages.Count == 0) return string.Empty; var sb = new StringBuilder(FormatStage(pipeline.Stages[0])); foreach (PipelineStageNode stage in pipeline.Stages.Skip(1)) sb.Append(",").AppendLine().Append("THEN ").Append(FormatStage(stage)); return sb.ToString(); }
    private string FormatStage(PipelineStageNode stage) => stage switch { SentenceNode sentence => FormatSentence(sentence), FilterStageNode filter => FormatFilter(filter), CheckStageNode check => FormatCheck(check), CollectionStageNode collection => FormatCollection(collection), _ => string.Empty };
    private string FormatSentence(SentenceNode sentence)
    {
        var sb = new StringBuilder(sentence.Verb.ToUpperInvariant()); if (!string.IsNullOrWhiteSpace(sentence.Qualifier)) sb.Append(' ').Append(sentence.Qualifier!.ToUpperInvariant());
        foreach (ClauseNode clause in sentence.Clauses) { if (clause.Values.Count == 0) continue; if (clause.RoleName.Equals("WHAT", StringComparison.OrdinalIgnoreCase)) sb.Append(' ').Append(string.Join(", ", clause.Values.Select(FormatExpression))); else sb.Append(' ').Append(clause.RoleName.ToUpperInvariant()).Append(' ').Append(string.Join(", ", clause.Values.Select(FormatExpression))); }
        if (!string.IsNullOrWhiteSpace(sentence.ResultAlias)) sb.Append(" INTO [").Append(sentence.ResultAlias).Append(']'); return sb.ToString();
    }
    private string FormatFilter(FilterStageNode filter) { var sb = new StringBuilder("FILTER"); if (filter.Source is not null) sb.Append(' ').Append(FormatExpression(filter.Source)); sb.Append(" WHERE ").Append(FormatExpression(filter.Predicate)); if (!string.IsNullOrWhiteSpace(filter.ResultAlias)) sb.Append(" INTO [").Append(filter.ResultAlias).Append(']'); return sb.ToString(); }
    private string FormatCheck(CheckStageNode check) { var sb = new StringBuilder("CHECK IF ").Append(FormatExpression(check.Condition)); if (!string.IsNullOrWhiteSpace(check.ResultAlias)) sb.Append(" INTO [").Append(check.ResultAlias).Append(']'); return sb.ToString(); }
    private string FormatCollection(CollectionStageNode node)
    {
        var sb = new StringBuilder(node.Operation.ToUpperInvariant());
        switch (node.Operation.ToUpperInvariant())
        {
            case "SORT": case "GROUP": if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source)); sb.Append(" BY ").Append(FormatExpression(node.Argument!)); break;
            case "TAKE": case "SKIP": sb.Append(' ').Append(FormatExpression(node.Argument!)); if (node.Source is not null) sb.Append(" FROM ").Append(FormatExpression(node.Source)); break;
            case "DISTINCT": if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source)); if (node.Argument is not null) sb.Append(" BY ").Append(FormatExpression(node.Argument)); break;
            case "COUNT": if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source)); break;
        }
        if (!string.IsNullOrWhiteSpace(node.ResultAlias)) sb.Append(" INTO [").Append(node.ResultAlias).Append(']'); return sb.ToString();
    }
    private string FormatIf(IfNode conditional) { var sb = new StringBuilder("IF ").Append(FormatExpression(conditional.Condition)).AppendLine(" THEN {"); sb.Append(Indent(FormatBlock(conditional.Then))).AppendLine().Append('}'); if (conditional.Else is not null) { sb.AppendLine(" ELSE {"); sb.Append(Indent(FormatBlock(conditional.Else))).AppendLine().Append('}'); } return sb.ToString(); }
    private string FormatForEach(ForEachNode loop) { var sb = new StringBuilder("FOR EACH [").Append(loop.Variable).Append("] IN ").Append(FormatExpression(loop.Source)).AppendLine(" THEN {"); sb.Append(Indent(FormatBlock(loop.Body))).AppendLine().Append('}'); return sb.ToString(); }
    private string FormatBlock(BlockNode block) => string.Join(Environment.NewLine, block.Statements.Select(FormatStatement));
    private static string Indent(string text) => string.Join(Environment.NewLine, text.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Select(x => "    " + x));
    private string FormatExpression(ExpressionNode expression, int parentPrecedence = 0) => expression switch { LiteralExpression literal => FormatLiteral(literal.Value), VariableExpression variable => $"[{variable.Name}]", PropertyExpression property => FormatProperty(property), ReferenceExpression reference => $"{{{reference.Value}}}", IdentifierExpression identifier => identifier.Name, InterpolatedStringExpression interpolated => FormatInterpolated(interpolated), PredicateExpression predicate => FormatPredicate(predicate, parentPrecedence), UnaryExpression unary => FormatUnary(unary, parentPrecedence), BinaryExpression binary => FormatBinary(binary, parentPrecedence), BetweenExpression between => FormatBetween(between, parentPrecedence), _ => string.Empty };
    private string FormatBinary(BinaryExpression binary, int parentPrecedence) { int precedence = BinaryPrecedence(binary.Operator); string text = $"{FormatExpression(binary.Left, precedence)} {binary.Operator.ToUpperInvariant()} {FormatExpression(binary.Right, precedence)}"; return precedence < parentPrecedence ? $"({text})" : text; }
    private string FormatBetween(BetweenExpression between, int parentPrecedence) { const int precedence = 3; string text = $"{FormatExpression(between.Operand, precedence)} BETWEEN {FormatExpression(between.Lower, precedence)} AND {FormatExpression(between.Upper, precedence)}"; return precedence < parentPrecedence ? $"({text})" : text; }
    private string FormatUnary(UnaryExpression unary, int parentPrecedence) { const int precedence = 6; string text = $"{unary.Operator.ToUpperInvariant()} {FormatExpression(unary.Operand, precedence)}"; return precedence < parentPrecedence ? $"({text})" : text; }
    private string FormatPredicate(PredicateExpression predicate, int parentPrecedence) { const int precedence = 7; string operand = FormatExpression(predicate.Operand, precedence); string text = predicate.Predicate.Equals("EXISTS", StringComparison.OrdinalIgnoreCase) ? $"{operand} EXISTS" : $"{operand} IS {predicate.Predicate.ToUpperInvariant()}"; return precedence < parentPrecedence ? $"({text})" : text; }
    private string FormatProperty(PropertyExpression property) { var parts = new Stack<string>(); ExpressionNode current = property; while (current is PropertyExpression p) { parts.Push(p.Property); current = p.Target; } if (current is VariableExpression variable) { parts.Push(variable.Name); return $"[{string.Join(".", parts)}]"; } return $"{FormatExpression(current)}.{string.Join(".", parts)}"; }
    private string FormatInterpolated(InterpolatedStringExpression interpolated) { var sb = new StringBuilder("\""); foreach (ExpressionNode part in interpolated.Parts) { switch (part) { case LiteralExpression literal: sb.Append(Escape(literal.Value?.ToString() ?? string.Empty)); break; case VariableExpression variable: sb.Append('[').Append(variable.Name).Append(']'); break; case PropertyExpression property: sb.Append(FormatProperty(property)); break; default: sb.Append(FormatExpression(part)); break; } } return sb.Append('"').ToString(); }
    private static string FormatLiteral(object? value) => value switch { null => "null", bool boolean => boolean ? "true" : "false", string text => $"\"{Escape(text)}\"", IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty, _ => value.ToString() ?? string.Empty };
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
    private static int BinaryPrecedence(string op) => op.ToUpperInvariant() switch { "OR" => 1, "AND" => 2, "CONTAINS" or "STARTS WITH" or "ENDS WITH" or "MATCHES" or "IN" or "BEFORE" or "AFTER" => 3, "=" or "==" or "!=" or ">" or "<" or ">=" or "<=" or "IS" or "IS NOT" => 4, _ => 0 };
}
