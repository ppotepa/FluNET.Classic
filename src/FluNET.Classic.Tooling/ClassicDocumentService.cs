using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Runtime;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Tooling;

public sealed record DocumentDiagnostic(string Source, string Code, string Message, TextSpan Span);
public sealed record DocumentAnalysis(bool Success, string? CanonicalSource, IReadOnlyList<DocumentDiagnostic> Diagnostics, ExecutionPlan Plan);
public sealed record SemanticDocumentToken(string Kind, TextSpan Span);
public sealed record DocumentSymbolInfo(string Name, string Kind, TextSpan Span);
public sealed record DocumentTextEdit(TextSpan Span, string NewText);
public sealed record SignatureInfo(string Label, string? Detail = null);
public sealed record SignatureHelpInfo(IReadOnlyList<SignatureInfo> Signatures, int ActiveSignature = 0);

public sealed class ClassicDocumentService
{
    private static readonly string[] ContinuationSyntax = { "INTO", "THEN", "AND THEN", "IF", "WHERE", "ELSE", "BY", "FROM", "CONTAINS", "STARTS WITH", "ENDS WITH", "MATCHES", "IN", "BEFORE", "AFTER", "BETWEEN", "SORT", "GROUP", "TAKE", "SKIP", "DISTINCT", "COUNT" };
    private static readonly HashSet<string> SyntaxWords = new(StringComparer.OrdinalIgnoreCase) { "INTO", "THEN", "AND", "IF", "WHERE", "ELSE", "FOR", "EACH", "NOT", "IS", "TRUE", "FALSE", "NULL", "EXISTS", "OK", "VALID", "EMPTY", "CONTAINS", "STARTS", "ENDS", "MATCHES", "BETWEEN", "SORT", "GROUP", "TAKE", "SKIP", "DISTINCT", "COUNT" };

    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;
    private readonly ClassicFormatter _formatter;
    private readonly ExecutionPlanner _planner;
    private readonly ClassicLanguageService _languageService;

    public ClassicDocumentService(LanguageSnapshot language, ClassicLexer lexer, ClassicParser parser, SemanticBinder binder, ClassicFormatter formatter, ExecutionPlanner planner, ClassicLanguageService languageService)
    { _language = language; _lexer = lexer; _parser = parser; _binder = binder; _formatter = formatter; _planner = planner; _languageService = languageService; }

    public DocumentAnalysis Analyze(string source, IReadOnlyDictionary<string, Type>? variableTypes = null)
    {
        source ??= string.Empty; ParseResult parse = _parser.Parse(source); BoundScript? bound = parse.Success ? _binder.Bind(parse.Script, variableTypes) : null; var check = new CheckResult(parse, bound); var diagnostics = new List<DocumentDiagnostic>();
        diagnostics.AddRange(parse.Diagnostics.Select(x => new DocumentDiagnostic("syntax", x.Code, x.Message, x.Span))); diagnostics.AddRange(bound?.Diagnostics.Select(x => new DocumentDiagnostic("binding", x.Code, x.Message, x.Span)) ?? Array.Empty<DocumentDiagnostic>());
        return new(check.Success, parse.Success ? _formatter.Format(parse.Script) : null, diagnostics, _planner.Build(check));
    }

    public IReadOnlyList<CompletionItem> Complete(string source, int position)
    {
        source ??= string.Empty; position = Math.Clamp(position, 0, source.Length); string prefix = PrefixAt(source, position); VerbDescriptor? verb = CurrentVerb(source, position); if (verb is null) return Merge(_languageService.Complete(prefix), SyntaxCompletion(prefix));
        var items = new List<CompletionItem>(); string[] qualifiers = verb.Implementations.SelectMany(x => x.Qualifiers).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        items.AddRange(_language.Qualifiers.Where(q => qualifiers.Contains(q.Name, StringComparer.OrdinalIgnoreCase) && q.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(q => new CompletionItem(q.Name, "qualifier", q.TargetType?.Name)));
        items.AddRange(verb.Implementations.SelectMany(x => x.Patterns).SelectMany(x => x.Roles).SelectMany(x => x.AllSurfaceNames).Distinct(StringComparer.OrdinalIgnoreCase).Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x, "role", $"role for {verb.Name}")));
        items.AddRange(SyntaxCompletion(prefix)); return items.GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public HoverInfo? Hover(string source, int position)
    {
        source ??= string.Empty; position = Math.Clamp(position, 0, source.Length); SyntaxToken? token = TokenAt(source, position); if (token is null || token.Kind != TokenKind.Word) return null; HoverInfo? direct = _languageService.Hover(token.Text); if (direct is not null) return direct;
        RoleSlotDescriptor[] roles = _language.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).SelectMany(x => x.Roles).Where(x => x.AllSurfaceNames.Contains(token.Text, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (roles.Length > 0) return new(token.Text.ToUpperInvariant(), $"Contextual role surface; semantic role(s): {string.Join(", ", roles.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase))}.");
        if (SyntaxWords.Contains(token.Text) || ContinuationSyntax.Any(x => x.Equals(token.Text, StringComparison.OrdinalIgnoreCase))) return new(token.Text.ToUpperInvariant(), "FluNET controlled-language syntax.");
        return null;
    }

    public IReadOnlyList<SemanticDocumentToken> SemanticTokens(string source)
    {
        var roles = _language.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).SelectMany(x => x.Roles).SelectMany(x => x.AllSurfaceNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _lexer.Lex(source ?? string.Empty).Where(x => x.Kind != TokenKind.End).Select(token => new SemanticDocumentToken(Classify(token, roles), token.Span)).ToArray();
    }

    public IReadOnlyList<DocumentSymbolInfo> Symbols(string source)
    {
        IReadOnlyList<SyntaxToken> tokens = _lexer.Lex(source ?? string.Empty); var result = new List<DocumentSymbolInfo>();
        for (int i = 0; i < tokens.Count; i++)
        {
            SyntaxToken token = tokens[i]; if (token.Kind != TokenKind.Variable) continue; string root = RootName(token);
            bool resultDefinition = i > 0 && tokens[i - 1].Kind == TokenKind.Word && (tokens[i - 1].Text.Equals("INTO", StringComparison.OrdinalIgnoreCase) || tokens[i - 1].Text.Equals("AS", StringComparison.OrdinalIgnoreCase));
            bool loopDefinition = i > 0 && tokens[i - 1].Kind == TokenKind.Word && tokens[i - 1].Text.Equals("EACH", StringComparison.OrdinalIgnoreCase);
            if (resultDefinition || loopDefinition) result.Add(new(root, loopDefinition ? "iterator" : "variable", token.Span));
        }
        return result;
    }

    public DocumentSymbolInfo? Definition(string source, int position)
    {
        SyntaxToken? token = TokenAt(source ?? string.Empty, position); if (token?.Kind != TokenKind.Variable) return null; string name = RootName(token); return Symbols(source).FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<DocumentSymbolInfo> References(string source, string name)
    {
        return _lexer.Lex(source ?? string.Empty).Where(x => x.Kind == TokenKind.Variable && RootName(x).Equals(name, StringComparison.OrdinalIgnoreCase)).Select(x => new DocumentSymbolInfo(name, "reference", x.Span)).ToArray();
    }

    public IReadOnlyList<DocumentTextEdit> Rename(string source, int position, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-'))) throw new ArgumentException("Variable name contains unsupported characters.", nameof(newName));
        SyntaxToken? token = TokenAt(source ?? string.Empty, position); if (token?.Kind != TokenKind.Variable) return Array.Empty<DocumentTextEdit>(); string oldName = RootName(token);
        return _lexer.Lex(source).Where(x => x.Kind == TokenKind.Variable && RootName(x).Equals(oldName, StringComparison.OrdinalIgnoreCase)).Select(x => { string value = x.Value?.ToString() ?? oldName; int dot = value.IndexOf('.'); string suffix = dot >= 0 ? value[dot..] : string.Empty; return new DocumentTextEdit(x.Span, $"[{newName}{suffix}]"); }).ToArray();
    }

    public SignatureHelpInfo? SignatureHelp(string source, int position)
    {
        VerbDescriptor? verb = CurrentVerb(source ?? string.Empty, Math.Clamp(position, 0, source?.Length ?? 0)); if (verb is null) return null;
        SignatureInfo[] signatures = verb.Implementations.SelectMany(i => i.Patterns.Select(p => new SignatureInfo(Signature(verb, i, p), i.ImplementationType.FullName))).ToArray(); return new(signatures);
    }

    public string Format(string source) { ParseResult parse = _parser.Parse(source); if (!parse.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, parse.Diagnostics.Select(x => $"{x.Code}: {x.Message}"))); return _formatter.Format(parse.Script); }
    public ExecutionPlan Plan(string source, IReadOnlyDictionary<string, Type>? variableTypes = null) => Analyze(source, variableTypes).Plan;

    private VerbDescriptor? CurrentVerb(string source, int position)
    {
        IReadOnlyList<SyntaxToken> tokens = _lexer.Lex(source[..position]); SyntaxToken? candidate = null;
        foreach (SyntaxToken token in tokens) { if (token.Kind is TokenKind.NewLine or TokenKind.Semicolon or TokenKind.Period) candidate = null; else if (candidate is null && token.Kind == TokenKind.Word) candidate = token; }
        return candidate is not null && _language.TryGetVerb(candidate.Text, out VerbDescriptor verb) ? verb : null;
    }

    private SyntaxToken? TokenAt(string source, int position) => _lexer.Lex(source).FirstOrDefault(x => x.Span.Start <= position && position <= x.Span.End && x.Kind != TokenKind.End);
    private static string PrefixAt(string source, int position) { int start = position; while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] is '_' or '-')) start--; return source[start..position]; }
    private static string RootName(SyntaxToken token) => (token.Value?.ToString() ?? string.Empty).Split('.', 2)[0];
    private IEnumerable<CompletionItem> SyntaxCompletion(string prefix) => ContinuationSyntax.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x, "syntax"));
    private static IReadOnlyList<CompletionItem> Merge(IEnumerable<CompletionItem> first, IEnumerable<CompletionItem> second) => first.Concat(second).GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    private string Classify(SyntaxToken token, HashSet<string> roles) => token.Kind switch { TokenKind.Variable => "variable", TokenKind.Reference => "reference", TokenKind.String => "string", TokenKind.Number => "number", TokenKind.Operator => "operator", TokenKind.Word when _language.TryGetVerb(token.Text, out _) => "verb", TokenKind.Word when _language.TryGetQualifier(token.Text, out _) => "qualifier", TokenKind.Word when roles.Contains(token.Text) => "role", TokenKind.Word when SyntaxWords.Contains(token.Text) => "keyword", TokenKind.Word => "identifier", _ => "operator" };
    private static string Signature(VerbDescriptor verb, VerbImplementationDescriptor implementation, SentencePattern pattern)
    {
        string qualifier = implementation.Qualifiers.Count > 0 ? $" [{string.Join('|', implementation.Qualifiers)}]" : string.Empty;
        string roles = string.Join(' ', pattern.Roles.OrderBy(x => x.Position).Select(x => $"{(x.Required ? "" : "[")}{x.Name}:{Friendly(x.ValueType)}{(x.Required ? "" : "]")}")); return $"{verb.Name}{qualifier} {roles} -> {Friendly(implementation.ResultType)}".Trim();
    }
    private static string Friendly(Type type) => type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(',', type.GetGenericArguments().Select(Friendly))}>" : type.Name;
}
