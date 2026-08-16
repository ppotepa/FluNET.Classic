using FluNET.Syntax.Ast;
using System.Text;

namespace FluNET.Syntax.Parsing;

public sealed class ClassicLexer
{
    public IReadOnlyList<ClassicToken> Lex(string source)
    {
        source ??= string.Empty;
        var tokens = new List<ClassicToken>();
        int position = 0;

        while (position < source.Length)
        {
            char current = source[position];

            if (current is ' ' or '\t' or '\r')
            {
                position++;
                continue;
            }

            if (current == '\n')
            {
                tokens.Add(new ClassicToken(ClassicTokenKind.NewLine, "\n", new TextSpan(position, 1)));
                position++;
                continue;
            }

            if (current == ';')
            {
                tokens.Add(new ClassicToken(ClassicTokenKind.Semicolon, ";", new TextSpan(position, 1)));
                position++;
                continue;
            }

            if (current == '[')
            {
                tokens.Add(ReadDelimited(source, ref position, '[', ']', ClassicTokenKind.Variable));
                continue;
            }

            if (current == '{')
            {
                tokens.Add(ReadDelimited(source, ref position, '{', '}', ClassicTokenKind.Reference));
                continue;
            }

            if (current == '"')
            {
                tokens.Add(ReadString(source, ref position));
                continue;
            }

            tokens.Add(ReadWord(source, ref position));
        }

        tokens.Add(new ClassicToken(ClassicTokenKind.End, string.Empty, new TextSpan(source.Length, 0)));
        return tokens;
    }

    private static ClassicToken ReadDelimited(
        string source,
        ref int position,
        char open,
        char close,
        ClassicTokenKind kind)
    {
        int start = position++;
        int depth = 1;
        var value = new StringBuilder();

        while (position < source.Length && depth > 0)
        {
            char current = source[position++];
            if (current == open)
            {
                depth++;
                value.Append(current);
                continue;
            }

            if (current == close)
            {
                depth--;
                if (depth > 0)
                {
                    value.Append(current);
                }
                continue;
            }

            value.Append(current);
        }

        return new ClassicToken(kind, value.ToString().Trim(), new TextSpan(start, position - start));
    }

    private static ClassicToken ReadString(string source, ref int position)
    {
        int start = position++;
        var value = new StringBuilder();
        bool escaped = false;

        while (position < source.Length)
        {
            char current = source[position++];
            if (escaped)
            {
                value.Append(current switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => current
                });
                escaped = false;
                continue;
            }

            if (current == '\\')
            {
                escaped = true;
                continue;
            }

            if (current == '"')
            {
                break;
            }

            value.Append(current);
        }

        return new ClassicToken(ClassicTokenKind.String, value.ToString(), new TextSpan(start, position - start));
    }

    private static ClassicToken ReadWord(string source, ref int position)
    {
        int start = position;
        while (position < source.Length)
        {
            char current = source[position];
            if (char.IsWhiteSpace(current) || current == ';')
            {
                break;
            }

            position++;
        }

        string value = source[start..position].TrimEnd('.');
        return new ClassicToken(ClassicTokenKind.Word, value, new TextSpan(start, position - start));
    }
}
