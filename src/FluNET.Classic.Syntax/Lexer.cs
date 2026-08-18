using System.Globalization;
using System.Text;

namespace FluNET.Classic.Syntax;

public enum TokenKind
{
    Word,
    Variable,
    Reference,
    String,
    Number,
    Operator,
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Period,
    Comment,
    NewLine,
    Semicolon,
    End
}

public sealed record SyntaxToken(TokenKind Kind, string Text, object? Value, TextSpan Span);

public sealed class ClassicLexer
{
    public IReadOnlyList<SyntaxToken> Lex(string? source)
    {
        source ??= string.Empty;
        var tokens = new List<SyntaxToken>();
        int i = 0;
        while (i < source.Length)
        {
            char ch = source[i];
            if (ch is ' ' or '\t' or '\r') { i++; continue; }
            if (ch == '#')
            {
                int start = i++;
                int contentStart = i;
                while (i < source.Length && source[i] != '\n') i++;
                string value = source[contentStart..i].Trim();
                tokens.Add(new(TokenKind.Comment, source[start..i], value, new(start, i - start)));
                continue;
            }
            if (ch == '\n') { tokens.Add(new(TokenKind.NewLine, "\n", null, new(i++, 1))); continue; }
            if (ch == ';') { tokens.Add(new(TokenKind.Semicolon, ";", null, new(i++, 1))); continue; }
            if (ch == ',') { tokens.Add(new(TokenKind.Comma, ",", null, new(i++, 1))); continue; }
            if (ch == '.') { tokens.Add(new(TokenKind.Period, ".", null, new(i++, 1))); continue; }
            if (ch == '(') { tokens.Add(new(TokenKind.LeftParen, "(", null, new(i++, 1))); continue; }
            if (ch == ')') { tokens.Add(new(TokenKind.RightParen, ")", null, new(i++, 1))); continue; }
            if (ch == '}') { tokens.Add(new(TokenKind.RightBrace, "}", null, new(i++, 1))); continue; }
            if (ch == '{' && (i + 1 >= source.Length || char.IsWhiteSpace(source[i + 1]))) { tokens.Add(new(TokenKind.LeftBrace, "{", null, new(i++, 1))); continue; }
            if (ch == '{')
            {
                int start = i++;
                int depth = 1;
                var sb = new StringBuilder();
                while (i < source.Length && depth > 0)
                {
                    if (source[i] == '{') { depth++; sb.Append(source[i++]); continue; }
                    if (source[i] == '}') { depth--; if (depth == 0) { i++; break; } sb.Append(source[i++]); continue; }
                    sb.Append(source[i++]);
                }
                tokens.Add(new(TokenKind.Reference, source[start..i], sb.ToString(), new(start, i - start)));
                continue;
            }
            if (ch == '[')
            {
                int start = i++;
                var sb = new StringBuilder();
                while (i < source.Length && source[i] != ']') sb.Append(source[i++]);
                if (i < source.Length && source[i] == ']') i++;
                tokens.Add(new(TokenKind.Variable, source[start..i], sb.ToString().Trim(), new(start, i - start)));
                continue;
            }
            if (ch is '"' or '\'')
            {
                int start = i++;
                char quote = ch;
                var sb = new StringBuilder();
                while (i < source.Length && source[i] != quote)
                {
                    if (source[i] == '\\' && i + 1 < source.Length)
                    {
                        i++;
                        sb.Append(source[i++] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', '\\' => '\\', '"' => '"', '\'' => '\'', var c => c });
                    }
                    else sb.Append(source[i++]);
                }
                if (i < source.Length && source[i] == quote) i++;
                tokens.Add(new(TokenKind.String, source[start..i], sb.ToString(), new(start, i - start)));
                continue;
            }
            if (char.IsDigit(ch) || (ch == '-' && i + 1 < source.Length && char.IsDigit(source[i + 1])))
            {
                int start = i;
                if (source[i] == '-') i++;
                while (i < source.Length && char.IsDigit(source[i])) i++;
                if (i < source.Length && source[i] == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1]))
                {
                    i++;
                    while (i < source.Length && char.IsDigit(source[i])) i++;
                }
                string numberText = source[start..i];
                if (decimal.TryParse(numberText, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                {
                    tokens.Add(new(TokenKind.Number, numberText, number, new(start, i - start)));
                    continue;
                }
                i = start;
            }
            if (IsOperatorStart(ch))
            {
                int start = i++;
                if (i < source.Length && source[i] == '=' && ch is '=' or '!' or '>' or '<') i++;
                string op = source[start..i];
                tokens.Add(new(TokenKind.Operator, op, op, new(start, i - start)));
                continue;
            }

            int wordStart = i;
            while (i < source.Length)
            {
                char current = source[i];
                if (char.IsWhiteSpace(current) || current is ';' or ',' or '(' or ')' or '{' or '}' or '[' or ']' or '"' or '\'' or '#' || IsOperatorStart(current)) break;
                if (current == '.' && !IsInternalPathDot(source, i, wordStart)) break;
                i++;
            }
            string word = source[wordStart..i];
            if (word.Length == 0) { i++; continue; }
            tokens.Add(new(TokenKind.Word, word, word, new(wordStart, i - wordStart)));
        }
        tokens.Add(new(TokenKind.End, string.Empty, null, new(source.Length, 0)));
        return tokens;
    }

    private static bool IsInternalPathDot(string source, int index, int wordStart)
    {
        if (source[index] != '.' || index <= wordStart || index + 1 >= source.Length) return false;
        return IsPathPart(source[index - 1]) && IsPathPart(source[index + 1]);
    }

    private static bool IsPathPart(char ch) => char.IsLetterOrDigit(ch) || ch is '_' or '-';
    private static bool IsOperatorStart(char ch) => ch is '=' or '!' or '>' or '<';
}
