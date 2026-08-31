using FluNET.Classic.Core;
using System.Globalization;
using System.Text;

namespace FluNET.Classic.Syntax;

public sealed class ClassicFormatter
{
    private readonly LanguageSnapshot? _language;

    public ClassicFormatter() { }
    public ClassicFormatter(LanguageSnapshot language) => _language = language;

    public string Format(ScriptNode script) => string.Join(Environment.NewLine, script.Statements.Select(FormatStatement));
    private string FormatStatement(StatementNode statement) => statement switch
    {
        PipelineNode pipeline => FormatPipeline(pipeline) + ".",
        IfNode conditional => FormatIf(conditional),
        ForEachNode loop => FormatForEach(loop),
        TryNode @try => FormatTry(@try),
        DefinitionNode definition => FormatDefinition(definition),
        RecordDefinitionNode record => FormatRecordDefinition(record),
        ReturnNode @return => _language is null ? "RETURN." : $"RETURN{(@return.Value is null ? string.Empty : " " + FormatExpression(@return.Value))}.",
        CommentStatementNode comment => $"# {comment.Text}".TrimEnd(),
        _ => string.Empty
    };
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
        IntrinsicDescriptor? intrinsic = ResolveIntrinsic(node.Operation);
        var sb = new StringBuilder(intrinsic?.Name ?? node.Operation.ToUpperInvariant());
        switch (intrinsic?.Syntax)
        {
            case IntrinsicSyntaxKind.CollectionBy:
                if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source));
                sb.Append(" BY ").Append(FormatExpression(node.Argument!));
                break;
            case IntrinsicSyntaxKind.CollectionAmountFrom:
                sb.Append(' ').Append(FormatExpression(node.Argument!));
                if (node.Source is not null) sb.Append(" FROM ").Append(FormatExpression(node.Source));
                break;
            case IntrinsicSyntaxKind.CollectionDistinct:
                if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source));
                if (node.Argument is not null) sb.Append(" BY ").Append(FormatExpression(node.Argument));
                break;
            case IntrinsicSyntaxKind.CollectionSourceOptional:
                if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source));
                break;
            default:
                if (node.Source is not null) sb.Append(' ').Append(FormatExpression(node.Source));
                if (node.Argument is not null) sb.Append(' ').Append(FormatExpression(node.Argument));
                break;
        }
        if (node.Strategy is not null)
        {
            string strategyRole = intrinsic?.StrategyRole ?? "USING";
            sb.Append(' ').Append(strategyRole.ToUpperInvariant()).Append(' ').Append(FormatStrategy(node.Strategy));
        }
        if (!string.IsNullOrWhiteSpace(node.ResultAlias)) sb.Append(" INTO [").Append(node.ResultAlias).Append(']'); return sb.ToString();
    }
    private string FormatIf(IfNode conditional)
    {
        var sb = new StringBuilder("IF ").Append(FormatExpression(conditional.Condition)).AppendLine(", THEN");
        sb.Append(Indent(FormatBlock(conditional.Then))).AppendLine();
        if (conditional.Else is not null)
        {
            sb.AppendLine("ELSE");
            sb.Append(Indent(FormatBlock(conditional.Else))).AppendLine();
        }
        return sb.Append("END IF.").ToString();
    }
    private string FormatForEach(ForEachNode loop)
    {
        var sb = new StringBuilder("FOR EACH [").Append(loop.Variable).Append("] IN ").Append(FormatExpression(loop.Source));
        if (loop.Parallelism is { } parallelism) sb.Append(", PARALLEL ").Append(parallelism);
        sb.AppendLine(", DO");
        sb.Append(Indent(FormatBlock(loop.Body))).AppendLine();
        return sb.Append("END FOR.").ToString();
    }
    private string FormatTry(TryNode @try)
    {
        var sb = new StringBuilder("TRY, DO").AppendLine();
        sb.Append(Indent(FormatBlock(@try.Body))).AppendLine();
        if (@try.Failure is not null)
        {
            sb.AppendLine("ON FAILURE");
            sb.Append(Indent(FormatBlock(@try.Failure))).AppendLine();
        }
        if (@try.Finally is not null)
        {
            sb.AppendLine("FINALLY");
            sb.Append(Indent(FormatBlock(@try.Finally))).AppendLine();
        }
        return sb.Append("END TRY.").ToString();
    }
    private string FormatDefinition(DefinitionNode definition)
    {
        var sb = new StringBuilder("DEFINE ").Append(definition.Kind.ToString().ToUpperInvariant()).Append(' ').Append(definition.Name.ToUpperInvariant());
        if (definition.Qualifier is not null) sb.Append(' ').Append(definition.Qualifier.ToUpperInvariant());
        foreach (DefinitionParameterNode parameter in definition.Parameters) sb.Append(", ").Append(parameter.RoleName).Append(" [").Append(parameter.Name).Append("] AS ").Append(parameter.TypeName.ToUpperInvariant());
        sb.Append(", RETURNING ").Append(definition.ReturnTypeName.ToUpperInvariant()).AppendLine(", DO");
        sb.Append(Indent(FormatBlock(definition.Body))).AppendLine();
        return sb.Append("END ").Append(definition.Kind.ToString().ToUpperInvariant()).Append('.').ToString();
    }
    private string FormatRecordDefinition(RecordDefinitionNode record) => $"DEFINE RECORD {record.Name.ToUpperInvariant()}{string.Concat(record.Fields.Select(field => $", {field.Name.ToUpperInvariant()} AS {field.TypeName.ToUpperInvariant()}"))}.";
    private string FormatBlock(BlockNode block) => string.Join(Environment.NewLine, block.Statements.Select(FormatStatement));
    private string FormatStrategy(ExpressionNode expression) => expression is IdentifierExpression identifier ? identifier.Name.ToUpperInvariant() : FormatExpression(expression);
    private static string Indent(string text) => string.Join(Environment.NewLine, text.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Select(x => "    " + x));
    private string FormatExpression(ExpressionNode expression, int parentPrecedence = 0) => expression switch { LiteralExpression literal => FormatLiteral(literal.Value), VariableExpression variable => $"[{variable.Name}]", PropertyExpression property => FormatProperty(property), ReferenceExpression reference => $"{{{reference.Value}}}", IdentifierExpression identifier => identifier.Name, InterpolatedStringExpression interpolated => FormatInterpolated(interpolated), PredicateExpression predicate => FormatPredicate(predicate, parentPrecedence), UnaryExpression unary => FormatUnary(unary, parentPrecedence), BinaryExpression binary => FormatBinary(binary, parentPrecedence), BetweenExpression between => FormatBetween(between, parentPrecedence), _ => string.Empty };
    private string FormatBinary(BinaryExpression binary, int parentPrecedence)
    {
        OperatorDescriptor? descriptor = ResolveOperator(binary.Operator);
        int precedence = descriptor?.Precedence ?? 0;
        string surface = descriptor?.Name ?? binary.Operator.ToUpperInvariant();
        string text = $"{FormatExpression(binary.Left, precedence)} {surface} {FormatExpression(binary.Right, precedence)}";
        return precedence < parentPrecedence ? $"({text})" : text;
    }
    private string FormatBetween(BetweenExpression between, int parentPrecedence)
    {
        OperatorDescriptor? descriptor = ResolveOperator(between.Operator);
        int precedence = descriptor?.Precedence ?? 0;
        string surface = descriptor?.Name ?? between.Operator.ToUpperInvariant();
        string text = $"{FormatExpression(between.Operand, precedence)} {surface} {FormatExpression(between.Lower, precedence)} AND {FormatExpression(between.Upper, precedence)}";
        return precedence < parentPrecedence ? $"({text})" : text;
    }
    private string FormatUnary(UnaryExpression unary, int parentPrecedence)
    {
        OperatorDescriptor? descriptor = ResolveOperator(unary.Operator);
        int precedence = descriptor?.Precedence ?? 0;
        string surface = descriptor?.Name ?? unary.Operator.ToUpperInvariant();
        string text = $"{surface} {FormatExpression(unary.Operand, precedence)}";
        return precedence < parentPrecedence ? $"({text})" : text;
    }
    private string FormatPredicate(PredicateExpression predicate, int parentPrecedence)
    {
        PredicateDescriptor? descriptor = ResolvePredicate(predicate.Predicate);
        int precedence = descriptor?.Precedence ?? 7;
        string surface = descriptor?.Name ?? predicate.Predicate.ToUpperInvariant();
        string operand = FormatExpression(predicate.Operand, precedence);
        string text = descriptor?.Syntax == PredicateSyntaxKind.Postfix ? $"{operand} {surface}" : $"{operand} IS {surface}";
        return precedence < parentPrecedence ? $"({text})" : text;
    }
    private string FormatProperty(PropertyExpression property) { var parts = new Stack<string>(); ExpressionNode current = property; while (current is PropertyExpression p) { parts.Push(p.Property); current = p.Target; } if (current is VariableExpression variable) { parts.Push(variable.Name); return $"[{string.Join(".", parts)}]"; } return $"{FormatExpression(current)}.{string.Join(".", parts)}"; }
    private string FormatInterpolated(InterpolatedStringExpression interpolated) { var sb = new StringBuilder("\""); foreach (ExpressionNode part in interpolated.Parts) { switch (part) { case LiteralExpression literal: sb.Append(Escape(literal.Value?.ToString() ?? string.Empty)); break; case VariableExpression variable: sb.Append('[').Append(variable.Name).Append(']'); break; case PropertyExpression property: sb.Append(FormatProperty(property)); break; default: sb.Append(FormatExpression(part)); break; } } return sb.Append('"').ToString(); }
    private OperatorDescriptor? ResolveOperator(string surface)
    {
        if (_language is not null && _language.TryGetOperator(surface, out OperatorDescriptor descriptor)) return descriptor;
        return StandardLanguageSurface.Operators.FirstOrDefault(x => x.AllSurfaceNames.Contains(surface, StringComparer.OrdinalIgnoreCase));
    }
    private PredicateDescriptor? ResolvePredicate(string surface)
    {
        if (_language is not null && _language.TryGetPredicate(surface, out PredicateDescriptor descriptor)) return descriptor;
        return StandardLanguageSurface.Predicates.FirstOrDefault(x => x.AllSurfaceNames.Contains(surface, StringComparer.OrdinalIgnoreCase));
    }
    private IntrinsicDescriptor? ResolveIntrinsic(string surface)
    {
        if (_language is not null && _language.TryGetIntrinsic(surface, out IntrinsicDescriptor descriptor)) return descriptor;
        return null;
    }
    private static string FormatLiteral(object? value) => value switch { null => "null", bool boolean => boolean ? "true" : "false", string text => $"\"{Escape(text)}\"", IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty, _ => value.ToString() ?? string.Empty };
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
}
