using FluNET.Language;
using FluNET.Syntax.Ast;

namespace FluNET.Syntax.Parsing;

public sealed class ClassicParser
{
    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;
    private IReadOnlyList<ClassicToken> _tokens = Array.Empty<ClassicToken>();
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private int _position;

    public ClassicParser(LanguageSnapshot language, ClassicLexer? lexer = null)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _lexer = lexer ?? new ClassicLexer();
    }

    public ClassicParseResult Parse(string source)
    {
        _tokens = _lexer.Lex(source);
        _diagnostics.Clear();
        _position = 0;

        var pipelines = new List<PipelineNode>();
        SkipSeparators();

        while (Current.Kind != ClassicTokenKind.End)
        {
            PipelineNode? pipeline = ParsePipeline();
            if (pipeline is not null)
            {
                pipelines.Add(pipeline);
            }

            SkipSeparators();
        }

        TextSpan span = pipelines.Count == 0
            ? new TextSpan(0, source?.Length ?? 0)
            : SpanFrom(pipelines[0].Span, pipelines[^1].Span);

        var script = new ScriptNode(pipelines, span);
        return new ClassicParseResult(script, _diagnostics.ToArray());
    }

    private PipelineNode? ParsePipeline()
    {
        int start = Current.Span.Start;
        var sentences = new List<SentenceNode>();

        SentenceNode? first = ParseSentence();
        if (first is null)
        {
            SkipToStatementBoundary();
            return null;
        }

        sentences.Add(first);

        while (TryConsumeThen())
        {
            SentenceNode? next = ParseSentence();
            if (next is null)
            {
                _diagnostics.Add(new SyntaxDiagnostic(
                    "FLU-SYN-004",
                    "THEN must be followed by a sentence.",
                    Current.Span));
                break;
            }

            sentences.Add(next);
        }

        int end = sentences[^1].Span.End;
        return new PipelineNode(sentences, new TextSpan(start, end - start));
    }

    private SentenceNode? ParseSentence()
    {
        if (Current.Kind != ClassicTokenKind.Word || IsThen(Current))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "FLU-SYN-001",
                $"Expected a verb but found '{Current.Value}'.",
                Current.Span));
            return null;
        }

        ClassicToken verbToken = Advance();
        if (!_language.TryGetVerb(verbToken.Value, out VerbDescriptor verb))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                "FLU-SYN-002",
                $"Unknown verb '{verbToken.Value}'.",
                verbToken.Span));
            return null;
        }

        HashSet<string> roleNames = verb.Implementations
            .SelectMany(i => i.Patterns)
            .SelectMany(p => p.Roles)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool hasWhat = roleNames.Contains("WHAT");
        HashSet<string> explicitRoles = roleNames
            .Where(r => !r.Equals("WHAT", StringComparison.OrdinalIgnoreCase) &&
                        !r.Equals("THEN", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? qualifier = null;
        if (hasWhat && Current.Kind == ClassicTokenKind.Word &&
            !explicitRoles.Contains(Current.Value) && !IsThen(Current) &&
            Peek(1).Kind is ClassicTokenKind.Variable or ClassicTokenKind.Reference)
        {
            qualifier = Advance().Value.ToUpperInvariant();
        }

        var clauses = new List<ClauseNode>();
        string? currentRole = hasWhat ? "WHAT" : null;
        ClassicToken? roleToken = null;
        var values = new List<ExpressionNode>();

        while (!IsSentenceBoundary(Current))
        {
            if (Current.Kind == ClassicTokenKind.Word && explicitRoles.Contains(Current.Value))
            {
                FlushClause();
                roleToken = Advance();
                currentRole = roleToken.Value.ToUpperInvariant();
                continue;
            }

            if (currentRole is null)
            {
                _diagnostics.Add(new SyntaxDiagnostic(
                    "FLU-SYN-003",
                    $"Unexpected value '{Current.Value}' after {verb.Name}.",
                    Current.Span));
                Advance();
                continue;
            }

            values.Add(ParseExpression(Advance()));
        }

        FlushClause();

        int end = clauses.Count > 0 ? clauses[^1].Span.End : verbToken.Span.End;
        return new SentenceNode(
            verb.Name,
            qualifier,
            clauses,
            new TextSpan(verbToken.Span.Start, end - verbToken.Span.Start));

        void FlushClause()
        {
            if (currentRole is null || values.Count == 0)
            {
                values.Clear();
                return;
            }

            int clauseStart = roleToken?.Span.Start ?? values[0].Span.Start;
            int clauseEnd = values[^1].Span.End;
            clauses.Add(new ClauseNode(
                currentRole,
                values.ToArray(),
                new TextSpan(clauseStart, clauseEnd - clauseStart)));

            values.Clear();
            roleToken = null;
        }
    }

    private ExpressionNode ParseExpression(ClassicToken token) => token.Kind switch
    {
        ClassicTokenKind.Variable => ParseVariable(token),
        ClassicTokenKind.Reference => new ReferenceExpression(token.Value, token.Span),
        ClassicTokenKind.String => ParseInterpolatedString(token),
        _ => new LiteralExpression(token.Value, token.Span)
    };

    private static ExpressionNode ParseVariable(ClassicToken token)
    {
        string[] parts = token.Value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return new VariableExpression(string.Empty, token.Span);
        }

        ExpressionNode expression = new VariableExpression(parts[0], token.Span);
        for (int i = 1; i < parts.Length; i++)
        {
            expression = new PropertyExpression(expression, parts[i], token.Span);
        }

        return expression;
    }

    private static ExpressionNode ParseInterpolatedString(ClassicToken token)
    {
        string text = token.Value;
        var parts = new List<ExpressionNode>();
        int cursor = 0;

        while (cursor < text.Length)
        {
            int open = text.IndexOf('[', cursor);
            if (open < 0)
            {
                if (cursor < text.Length)
                {
                    parts.Add(new LiteralExpression(text[cursor..], token.Span));
                }
                break;
            }

            if (open > cursor)
            {
                parts.Add(new LiteralExpression(text[cursor..open], token.Span));
            }

            int close = text.IndexOf(']', open + 1);
            if (close < 0)
            {
                parts.Add(new LiteralExpression(text[open..], token.Span));
                break;
            }

            string variable = text[(open + 1)..close];
            parts.Add(ParseVariable(new ClassicToken(ClassicTokenKind.Variable, variable, token.Span)));
            cursor = close + 1;
        }

        return parts.Count == 1 && parts[0] is LiteralExpression literal
            ? literal
            : new InterpolatedStringExpression(parts, token.Span);
    }

    private bool TryConsumeThen()
    {
        if (IsThen(Current))
        {
            Advance();
            SkipNewLines();
            return true;
        }

        if (Current.Kind != ClassicTokenKind.NewLine)
        {
            return false;
        }

        int saved = _position;
        SkipNewLines();
        if (IsThen(Current))
        {
            Advance();
            SkipNewLines();
            return true;
        }

        _position = saved;
        return false;
    }

    private static bool IsThen(ClassicToken token) =>
        token.Kind == ClassicTokenKind.Word &&
        token.Value.Equals("THEN", StringComparison.OrdinalIgnoreCase);

    private static bool IsSentenceBoundary(ClassicToken token) =>
        token.Kind is ClassicTokenKind.End or ClassicTokenKind.NewLine or ClassicTokenKind.Semicolon || IsThen(token);

    private void SkipSeparators()
    {
        while (Current.Kind is ClassicTokenKind.NewLine or ClassicTokenKind.Semicolon)
        {
            Advance();
        }
    }

    private void SkipNewLines()
    {
        while (Current.Kind == ClassicTokenKind.NewLine)
        {
            Advance();
        }
    }

    private void SkipToStatementBoundary()
    {
        while (Current.Kind is not ClassicTokenKind.End and not ClassicTokenKind.NewLine and not ClassicTokenKind.Semicolon)
        {
            Advance();
        }
    }

    private ClassicToken Current => Peek(0);

    private ClassicToken Peek(int offset)
    {
        int index = Math.Min(_position + offset, _tokens.Count - 1);
        return _tokens[index];
    }

    private ClassicToken Advance()
    {
        ClassicToken current = Current;
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }
        return current;
    }

    private static TextSpan SpanFrom(TextSpan first, TextSpan last) =>
        new(first.Start, last.End - first.Start);
}
