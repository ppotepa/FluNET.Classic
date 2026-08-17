using FluNET.Classic.Core;

namespace FluNET.Classic.Syntax;

public sealed record SyntaxDiagnostic(string Code, string Message, TextSpan Span);
public sealed record ParseResult(ScriptNode Script, IReadOnlyList<SyntaxDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Count == 0;
}

public sealed class ClassicParser
{
    private static readonly HashSet<string> NamedPredicates = new(StringComparer.OrdinalIgnoreCase) { "OK", "VALID" };

    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private IReadOnlyList<SyntaxToken> _tokens = Array.Empty<SyntaxToken>();
    private int _position;

    public ClassicParser(LanguageSnapshot language, ClassicLexer? lexer = null)
    {
        _language = language;
        _lexer = lexer ?? new ClassicLexer();
    }

    public ParseResult Parse(string? source)
    {
        source ??= string.Empty;
        _tokens = _lexer.Lex(source);
        _position = 0;
        _diagnostics.Clear();
        List<StatementNode> statements = ParseStatements(stopAtRightBrace: false);
        TextSpan span = statements.Count == 0
            ? new(0, source.Length)
            : TextSpan.FromBounds(statements[0].Span.Start, statements[^1].Span.End);
        return new(new ScriptNode(statements, span), _diagnostics.ToArray());
    }

    private List<StatementNode> ParseStatements(bool stopAtRightBrace)
    {
        var result = new List<StatementNode>();
        SkipStatementSeparators();
        while (Current.Kind != TokenKind.End && (!stopAtRightBrace || Current.Kind != TokenKind.RightBrace))
        {
            StatementNode? statement = ParseStatement();
            if (statement is not null) result.Add(statement);
            else SkipToBoundary();
            SkipStatementSeparators();
        }
        return result;
    }

    private StatementNode? ParseStatement()
    {
        if (IsWord("IF")) return ParseIf();
        if (IsWord("FOR") && IsWord("EACH", 1)) return ParseForEach();
        return ParsePipeline();
    }

    private IfNode ParseIf()
    {
        int start = Advance().Span.Start;
        ExpressionNode condition = ParseExpressionUntil("THEN");
        ConsumeThen("FLU-SYN-101", "IF requires THEN.");
        BlockNode then = ParseBody(stopAtElse: true);
        BlockNode? otherwise = null;
        if (IsWord("ELSE"))
        {
            Advance();
            otherwise = ParseBody(stopAtElse: false);
        }
        int end = otherwise?.Span.End ?? then.Span.End;
        return new(condition, then, otherwise, TextSpan.FromBounds(start, end));
    }

    private ForEachNode ParseForEach()
    {
        int start = Advance().Span.Start;
        ExpectWord("EACH", "FLU-SYN-110", "FOR must be followed by EACH.");
        SyntaxToken variable = Current;
        if (variable.Kind != TokenKind.Variable)
            _diagnostics.Add(new("FLU-SYN-111", "FOR EACH requires an iterator variable.", variable.Span));
        else
            Advance();

        ExpectWord("IN", "FLU-SYN-112", "FOR EACH requires IN.");
        ExpressionNode source = ParseExpressionUntil("THEN");
        ConsumeThen("FLU-SYN-113", "FOR EACH requires THEN.");
        BlockNode body = ParseBody(stopAtElse: false);
        return new(variable.Value?.ToString() ?? string.Empty, source, body, TextSpan.FromBounds(start, body.Span.End));
    }

    private BlockNode ParseBody(bool stopAtElse)
    {
        SkipNewLines();
        if (Current.Kind == TokenKind.LeftBrace)
        {
            int start = Advance().Span.Start;
            List<StatementNode> statements = ParseStatements(stopAtRightBrace: true);
            int end = Current.Kind == TokenKind.RightBrace ? Advance().Span.End : (statements.LastOrDefault()?.Span.End ?? start);
            return new(statements, TextSpan.FromBounds(start, end));
        }

        StatementNode? single = ParsePipeline(stopAtElse);
        if (single is null) return new(Array.Empty<StatementNode>(), Current.Span);
        return new(new[] { single }, single.Span);
    }

    private PipelineNode? ParsePipeline(bool stopAtElse = false)
    {
        int start = Current.Span.Start;
        var stages = new List<PipelineStageNode>();
        PipelineStageNode? first = ParseStage();
        if (first is null) return null;
        stages.Add(first);

        while (TryConsumePipelineContinuation())
        {
            if (stopAtElse && IsWord("ELSE")) break;
            PipelineStageNode? stage = ParseStage();
            if (stage is null) break;
            stages.Add(stage);
        }

        return new(stages, TextSpan.FromBounds(start, stages[^1].Span.End));
    }

    private bool TryConsumePipelineContinuation()
    {
        int saved = _position;
        if (Current.Kind == TokenKind.Comma) Advance();
        SkipNewLines();

        if (IsWord("AND") && IsWord("THEN", 1))
        {
            Advance();
            Advance();
            SkipNewLines();
            return true;
        }
        if (IsWord("THEN"))
        {
            Advance();
            SkipNewLines();
            return true;
        }

        _position = saved;
        return false;
    }

    private PipelineStageNode? ParseStage()
    {
        if (IsWord("FILTER")) return ParseFilter();
        if (IsWord("CHECK") && IsWord("IF", 1)) return ParseCheck();
        return ParseSentence();
    }

    private FilterStageNode ParseFilter()
    {
        int start = Advance().Span.Start;
        ExpressionNode? source = null;
        if (!IsWord("WHERE")) source = ParseAtomic();
        ExpectWord("WHERE", "FLU-SYN-120", "FILTER requires WHERE.");
        ExpressionNode predicate = ParseExpressionUntil("INTO", "AS", "THEN", "ELSE");
        string? alias = ParseOptionalResultAlias(allowLegacyAs: true);
        int end = alias is not null ? Previous.Span.End : predicate.Span.End;
        return new(source, predicate, alias, TextSpan.FromBounds(start, end));
    }

    private CheckStageNode ParseCheck()
    {
        int start = Advance().Span.Start;
        ExpectWord("IF", "FLU-SYN-125", "CHECK requires IF.");
        ExpressionNode condition = ParseExpressionUntil("INTO", "AS", "THEN", "ELSE");
        string? alias = ParseOptionalResultAlias(allowLegacyAs: true);
        int end = alias is not null ? Previous.Span.End : condition.Span.End;
        return new(condition, alias, TextSpan.FromBounds(start, end));
    }

    private SentenceNode? ParseSentence()
    {
        if (Current.Kind != TokenKind.Word)
        {
            _diagnostics.Add(new("FLU-SYN-001", $"Expected a verb but found '{Current.Text}'.", Current.Span));
            return null;
        }

        SyntaxToken verbToken = Advance();
        if (!_language.TryGetVerb(verbToken.Text, out VerbDescriptor verb))
        {
            _diagnostics.Add(new("FLU-SYN-002", $"Unknown verb '{verbToken.Text}'.", verbToken.Span));
            return null;
        }

        string? qualifier = null;
        if (Current.Kind == TokenKind.Word && _language.TryGetQualifier(Current.Text, out QualifierDescriptor qualifierDescriptor))
        {
            qualifier = qualifierDescriptor.Name;
            Advance();
        }

        HashSet<string> surfaceRoles = BuildSurfaceRoleSet(verb);
        string? currentRole = DetermineImplicitRole(verb);
        SyntaxToken? roleToken = null;
        var clauses = new List<ClauseNode>();
        var values = new List<ExpressionNode>();
        string? alias = null;

        while (!IsStageBoundary(Current))
        {
            if (Current.Kind == TokenKind.Comma)
            {
                Advance();
                SkipNewLines();
                continue;
            }

            if (IsWord("INTO"))
            {
                Flush();
                alias = ParseOptionalResultAlias(allowLegacyAs: false);
                break;
            }

            if (IsWord("AS") && Peek(1).Kind == TokenKind.Variable)
            {
                Flush();
                alias = ParseOptionalResultAlias(allowLegacyAs: true);
                break;
            }

            if (Current.Kind == TokenKind.Word && surfaceRoles.Contains(Current.Text))
            {
                Flush();
                roleToken = Advance();
                // Keep the explicit surface marker in the AST. The binder resolves it against
                // each candidate SentencePattern, which makes aliases truly pattern-scoped.
                currentRole = roleToken.Text.ToUpperInvariant();
                SkipNewLines();
                continue;
            }

            if (currentRole is null)
            {
                _diagnostics.Add(new("FLU-SYN-003", $"Unexpected value '{Current.Text}' after {verb.Name}.", Current.Span));
                Advance();
                continue;
            }
            values.Add(ParseAtomic());
        }
        Flush();

        int end = alias is not null ? Previous.Span.End : clauses.LastOrDefault()?.Span.End ?? verbToken.Span.End;
        return new(verb.Name, qualifier, clauses, alias, TextSpan.FromBounds(verbToken.Span.Start, end));

        void Flush()
        {
            if (currentRole is null || values.Count == 0)
            {
                values.Clear();
                roleToken = null;
                return;
            }
            int clauseStart = roleToken?.Span.Start ?? values[0].Span.Start;
            clauses.Add(new(currentRole, values.ToArray(), TextSpan.FromBounds(clauseStart, values[^1].Span.End)));
            values.Clear();
            roleToken = null;
        }
    }

    private static HashSet<string> BuildSurfaceRoleSet(VerbDescriptor verb)
    {
        return verb.Implementations
            .SelectMany(x => x.Patterns)
            .SelectMany(x => x.Roles)
            .Where(x => !x.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && !x.Name.Equals("THEN", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.AllSurfaceNames)
            .Where(x => !x.Equals("INTO", StringComparison.OrdinalIgnoreCase) && !x.Equals("THEN", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string? DetermineImplicitRole(VerbDescriptor verb)
    {
        RoleSlotDescriptor[] roles = verb.Implementations.SelectMany(x => x.Patterns).SelectMany(x => x.Roles).ToArray();
        if (roles.Any(x => x.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && x.Direction is RoleDirection.Input or RoleDirection.InputOutput))
            return "WHAT";

        string[] requiredInputs = roles
            .Where(x => x.Direction != RoleDirection.Output && x.Required && !x.Name.Equals("WHAT", StringComparison.OrdinalIgnoreCase) && !x.Name.Equals("THEN", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return requiredInputs.Length == 1 ? requiredInputs[0] : null;
    }

    private string? ParseOptionalResultAlias(bool allowLegacyAs)
    {
        if (IsWord("INTO")) Advance();
        else if (allowLegacyAs && IsWord("AS")) Advance();
        else return null;

        SkipNewLines();
        if (Current.Kind != TokenKind.Variable)
        {
            _diagnostics.Add(new("FLU-SYN-130", "INTO requires a [variable].", Current.Span));
            return null;
        }
        string alias = Current.Value?.ToString() ?? string.Empty;
        Advance();
        return alias;
    }

    private ExpressionNode ParseExpressionUntil(params string[] stopWords) => ParseBinary(0, stopWords);

    private ExpressionNode ParseBinary(int parentPrecedence, string[] stopWords)
    {
        ExpressionNode left;
        int unary = UnaryPrecedence(Current);
        if (unary > 0)
        {
            SyntaxToken op = Advance();
            ExpressionNode operand = ParseBinary(unary, stopWords);
            left = new UnaryExpression(op.Text.ToUpperInvariant(), operand, TextSpan.FromBounds(op.Span.Start, operand.Span.End));
        }
        else
        {
            left = ParsePrimary(stopWords);
        }

        while (!AtExpressionBoundary(stopWords))
        {
            if (TryParsePredicate(ref left)) continue;

            int precedence = BinaryPrecedence(Current);
            if (precedence == 0 || precedence <= parentPrecedence) break;
            SyntaxToken op = Advance();
            string operatorText = op.Text.ToUpperInvariant();
            if (operatorText == "IS" && IsWord("NOT"))
            {
                Advance();
                operatorText = "IS NOT";
            }
            ExpressionNode right = ParseBinary(precedence, stopWords);
            left = new BinaryExpression(left, operatorText, right, TextSpan.FromBounds(left.Span.Start, right.Span.End));
        }
        return left;
    }

    private bool TryParsePredicate(ref ExpressionNode left)
    {
        if (IsWord("EXISTS"))
        {
            SyntaxToken predicate = Advance();
            left = new PredicateExpression("EXISTS", left, TextSpan.FromBounds(left.Span.Start, predicate.Span.End));
            return true;
        }

        if (!IsWord("IS")) return false;
        int offset = 1;
        bool negate = false;
        if (IsWord("NOT", offset)) { negate = true; offset++; }
        if (Peek(offset).Kind != TokenKind.Word || !NamedPredicates.Contains(Peek(offset).Text)) return false;

        Advance();
        if (negate) Advance();
        SyntaxToken predicateToken = Advance();
        ExpressionNode predicateExpression = new PredicateExpression(
            predicateToken.Text.ToUpperInvariant(),
            left,
            TextSpan.FromBounds(left.Span.Start, predicateToken.Span.End));
        left = negate
            ? new UnaryExpression("NOT", predicateExpression, predicateExpression.Span)
            : predicateExpression;
        return true;
    }

    private ExpressionNode ParsePrimary(string[] stopWords)
    {
        if (Current.Kind == TokenKind.LeftParen)
        {
            int start = Advance().Span.Start;
            ExpressionNode expression = ParseBinary(0, Array.Empty<string>());
            if (Current.Kind == TokenKind.RightParen) Advance();
            return expression with { Span = TextSpan.FromBounds(start, Previous.Span.End) };
        }
        return ParseAtomic();
    }

    private ExpressionNode ParseAtomic()
    {
        SyntaxToken token = Advance();
        return token.Kind switch
        {
            TokenKind.Variable => ParseVariable(token),
            TokenKind.Reference => new ReferenceExpression(token.Value?.ToString() ?? string.Empty, token.Span),
            TokenKind.String => ParseInterpolated(token),
            TokenKind.Number => new LiteralExpression(token.Value, token.Span),
            TokenKind.Word when token.Text.Equals("true", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(true, token.Span),
            TokenKind.Word when token.Text.Equals("false", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(false, token.Span),
            TokenKind.Word when token.Text.Equals("null", StringComparison.OrdinalIgnoreCase) => new LiteralExpression(null, token.Span),
            TokenKind.Word => new IdentifierExpression(token.Text, token.Span),
            _ => new LiteralExpression(token.Text, token.Span)
        };
    }

    private static ExpressionNode ParseVariable(SyntaxToken token)
    {
        string[] parts = (token.Value?.ToString() ?? string.Empty).Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        ExpressionNode result = new VariableExpression(parts.FirstOrDefault() ?? string.Empty, token.Span);
        foreach (string part in parts.Skip(1)) result = new PropertyExpression(result, part, token.Span);
        return result;
    }

    private static ExpressionNode ParseInterpolated(SyntaxToken token)
    {
        string text = token.Value?.ToString() ?? string.Empty;
        var parts = new List<ExpressionNode>();
        int cursor = 0;
        while (cursor < text.Length)
        {
            int open = text.IndexOf('[', cursor);
            if (open < 0)
            {
                if (cursor < text.Length) parts.Add(new LiteralExpression(text[cursor..], token.Span));
                break;
            }
            if (open > cursor) parts.Add(new LiteralExpression(text[cursor..open], token.Span));
            int close = text.IndexOf(']', open + 1);
            if (close < 0)
            {
                parts.Add(new LiteralExpression(text[open..], token.Span));
                break;
            }
            string name = text[(open + 1)..close];
            parts.Add(ParseVariable(new(TokenKind.Variable, name, name, token.Span)));
            cursor = close + 1;
        }
        return parts.Count == 1 && parts[0] is LiteralExpression literal ? literal : new InterpolatedStringExpression(parts, token.Span);
    }

    private bool AtExpressionBoundary(string[] stopWords) =>
        Current.Kind is TokenKind.End or TokenKind.NewLine or TokenKind.Semicolon or TokenKind.Period or TokenKind.Comma or TokenKind.RightBrace or TokenKind.RightParen ||
        stopWords.Any(x => IsWord(x));

    private static int UnaryPrecedence(SyntaxToken token) => token.Kind == TokenKind.Word && token.Text.Equals("NOT", StringComparison.OrdinalIgnoreCase) ? 6 : 0;

    private static int BinaryPrecedence(SyntaxToken token)
    {
        if (token.Kind == TokenKind.Operator) return 4;
        if (token.Kind != TokenKind.Word) return 0;
        return token.Text.ToUpperInvariant() switch { "OR" => 1, "AND" => 2, "IS" => 4, _ => 0 };
    }

    private bool IsStageBoundary(SyntaxToken token) =>
        token.Kind is TokenKind.End or TokenKind.NewLine or TokenKind.Semicolon or TokenKind.Period or TokenKind.RightBrace ||
        IsWord("THEN") || IsWord("ELSE") || (IsWord("AND") && IsWord("THEN", 1)) ||
        (token.Kind == TokenKind.Comma && CommaStartsPipelineContinuation());

    private bool CommaStartsPipelineContinuation()
    {
        if (Current.Kind != TokenKind.Comma) return false;
        int offset = 1;
        while (Peek(offset).Kind == TokenKind.NewLine) offset++;
        return IsWordAt("THEN", offset) || (IsWordAt("AND", offset) && IsWordAt("THEN", offset + 1));
    }

    private bool IsWord(string word, int offset = 0) => IsWordAt(word, offset);
    private bool IsWordAt(string word, int offset) => Peek(offset).Kind == TokenKind.Word && Peek(offset).Text.Equals(word, StringComparison.OrdinalIgnoreCase);

    private void ExpectWord(string word, string code, string message)
    {
        if (IsWord(word)) Advance();
        else _diagnostics.Add(new(code, message, Current.Span));
    }

    private void ConsumeThen(string code, string message)
    {
        if (Current.Kind == TokenKind.Comma) Advance();
        SkipNewLines();
        ExpectWord("THEN", code, message);
        SkipNewLines();
    }

    private void SkipStatementSeparators()
    {
        while (Current.Kind is TokenKind.NewLine or TokenKind.Semicolon or TokenKind.Period) Advance();
    }

    private void SkipNewLines()
    {
        while (Current.Kind == TokenKind.NewLine) Advance();
    }

    private void SkipToBoundary()
    {
        while (Current.Kind is not TokenKind.End and not TokenKind.NewLine and not TokenKind.Semicolon and not TokenKind.Period and not TokenKind.RightBrace) Advance();
    }

    private SyntaxToken Current => Peek(0);
    private SyntaxToken Previous => Peek(-1);
    private SyntaxToken Peek(int offset)
    {
        int index = Math.Clamp(_position + offset, 0, _tokens.Count - 1);
        return _tokens[index];
    }

    private SyntaxToken Advance()
    {
        SyntaxToken token = Current;
        if (_position < _tokens.Count - 1) _position++;
        return token;
    }
}
