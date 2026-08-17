using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Runtime;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Tooling;

public sealed record DocumentDiagnostic(string Source, string Code, string Message, TextSpan Span);

public sealed record DocumentAnalysis(
    bool Success,
    string? CanonicalSource,
    IReadOnlyList<DocumentDiagnostic> Diagnostics,
    ExecutionPlan Plan);

public sealed class ClassicDocumentService
{
    private static readonly string[] ContinuationSyntax = { "INTO", "THEN", "AND THEN", "IF", "WHERE", "ELSE" };

    private readonly LanguageSnapshot _language;
    private readonly ClassicLexer _lexer;
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;
    private readonly ClassicFormatter _formatter;
    private readonly ExecutionPlanner _planner;
    private readonly ClassicLanguageService _languageService;

    public ClassicDocumentService(
        LanguageSnapshot language,
        ClassicLexer lexer,
        ClassicParser parser,
        SemanticBinder binder,
        ClassicFormatter formatter,
        ExecutionPlanner planner,
        ClassicLanguageService languageService)
    {
        _language = language;
        _lexer = lexer;
        _parser = parser;
        _binder = binder;
        _formatter = formatter;
        _planner = planner;
        _languageService = languageService;
    }

    public DocumentAnalysis Analyze(string source, IReadOnlyDictionary<string, Type>? variableTypes = null)
    {
        source ??= string.Empty;
        ParseResult parse = _parser.Parse(source);
        BoundScript? bound = parse.Success ? _binder.Bind(parse.Script, variableTypes) : null;
        var check = new CheckResult(parse, bound);
        var diagnostics = new List<DocumentDiagnostic>();
        diagnostics.AddRange(parse.Diagnostics.Select(x => new DocumentDiagnostic("syntax", x.Code, x.Message, x.Span)));
        diagnostics.AddRange(bound?.Diagnostics.Select(x => new DocumentDiagnostic("binding", x.Code, x.Message, x.Span)) ?? Array.Empty<DocumentDiagnostic>());
        return new(
            check.Success,
            parse.Success ? _formatter.Format(parse.Script) : null,
            diagnostics,
            _planner.Build(check));
    }

    public IReadOnlyList<CompletionItem> Complete(string source, int position)
    {
        source ??= string.Empty;
        position = Math.Clamp(position, 0, source.Length);
        string prefix = PrefixAt(source, position);
        VerbDescriptor? verb = CurrentVerb(source, position);
        if (verb is null) return _languageService.Complete(prefix);

        var items = new List<CompletionItem>();
        string[] qualifiers = verb.Implementations.SelectMany(x => x.Qualifiers).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        items.AddRange(_language.Qualifiers
            .Where(q => qualifiers.Contains(q.Name, StringComparer.OrdinalIgnoreCase) && q.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(q => new CompletionItem(q.Name, "qualifier", q.TargetType?.Name)));
        items.AddRange(verb.Implementations.SelectMany(x => x.Patterns).SelectMany(x => x.Roles).SelectMany(x => x.AllSurfaceNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(x => new CompletionItem(x, "role", $"role for {verb.Name}")));
        items.AddRange(ContinuationSyntax.Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(x => new CompletionItem(x, "syntax")));
        return items.GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public HoverInfo? Hover(string source, int position)
    {
        source ??= string.Empty;
        position = Math.Clamp(position, 0, source.Length);
        SyntaxToken? token = _lexer.Lex(source).FirstOrDefault(x => x.Span.Start <= position && position <= x.Span.End && x.Kind != TokenKind.End);
        if (token is null || token.Kind != TokenKind.Word) return null;
        HoverInfo? direct = _languageService.Hover(token.Text);
        if (direct is not null) return direct;

        RoleSlotDescriptor[] roles = _language.Verbs.SelectMany(x => x.Implementations).SelectMany(x => x.Patterns).SelectMany(x => x.Roles)
            .Where(x => x.AllSurfaceNames.Contains(token.Text, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (roles.Length == 0) return null;
        string semantics = string.Join(", ", roles.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase));
        return new(token.Text.ToUpperInvariant(), $"Contextual role surface; semantic role(s): {semantics}.");
    }

    public string Format(string source)
    {
        ParseResult parse = _parser.Parse(source);
        if (!parse.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, parse.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        return _formatter.Format(parse.Script);
    }

    public ExecutionPlan Plan(string source, IReadOnlyDictionary<string, Type>? variableTypes = null) => Analyze(source, variableTypes).Plan;

    private VerbDescriptor? CurrentVerb(string source, int position)
    {
        IReadOnlyList<SyntaxToken> tokens = _lexer.Lex(source[..position]);
        SyntaxToken? candidate = null;
        foreach (SyntaxToken token in tokens)
        {
            if (token.Kind is TokenKind.NewLine or TokenKind.Semicolon or TokenKind.Period) candidate = null;
            else if (candidate is null && token.Kind == TokenKind.Word) candidate = token;
        }
        if (candidate is not null && _language.TryGetVerb(candidate.Text, out VerbDescriptor verb)) return verb;
        return null;
    }

    private static string PrefixAt(string source, int position)
    {
        int start = position;
        while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] is '_' or '-')) start--;
        return source[start..position];
    }
}
