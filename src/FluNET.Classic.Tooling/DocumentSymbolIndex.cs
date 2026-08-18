using FluNET.Classic.Syntax;

namespace FluNET.Classic.Tooling;

internal sealed class DocumentSymbolIndex
{
    private readonly IReadOnlyList<IndexedOccurrence> _occurrences;

    private DocumentSymbolIndex(IReadOnlyList<IndexedOccurrence> occurrences) => _occurrences = occurrences;

    public static DocumentSymbolIndex Build(string source, ClassicLexer lexer, ClassicParser parser)
    {
        source ??= string.Empty;
        ParseResult parse = parser.Parse(source);
        var scopes = new List<ScopeInfo> { new(0, null, new TextSpan(0, source.Length)) };
        var iteratorScopes = new List<IteratorScope>();
        var promotionScopes = new List<PromotionScope>();
        if (parse.Success)
            BuildScopes(parse.Script.Statements, 0, scopes, iteratorScopes, promotionScopes);

        IReadOnlyList<SyntaxToken> tokens = lexer.Lex(source);
        var definitions = new List<DefinitionSeed>();
        var variables = new List<(SyntaxToken Token, int ScopeId)>();

        for (int index = 0; index < tokens.Count; index++)
        {
            SyntaxToken token = tokens[index];
            if (token.Kind != TokenKind.Variable) continue;

            int scopeId = InnermostScope(scopes, token.Span.Start);
            string root = RootName(token);
            bool followsEach = PreviousWord(tokens, index) == "EACH";
            IteratorScope? iterator = followsEach
                ? iteratorScopes.FirstOrDefault(x =>
                    token.Span.Start >= x.PrefixStart && token.Span.End <= x.PrefixEnd &&
                    root.Equals(x.Name, StringComparison.OrdinalIgnoreCase))
                : null;
            if (iterator is not null) scopeId = iterator.ScopeId;

            variables.Add((token, scopeId));
            bool resultDefinition = PreviousWord(tokens, index) is "INTO" or "AS";
            bool iteratorDefinition = iterator is not null;
            if (resultDefinition || iteratorDefinition)
                definitions.Add(new(token, scopeId, iteratorDefinition ? "iterator" : "variable", Guid.NewGuid()));
        }

        var promotions = BuildPromotions(definitions, promotionScopes);
        var occurrences = new List<IndexedOccurrence>();
        foreach ((SyntaxToken token, int scopeId) in variables)
        {
            string name = RootName(token);
            DefinitionSeed? self = definitions.FirstOrDefault(x => SameSpan(x.Token.Span, token.Span));
            if (self is not null)
            {
                occurrences.Add(new(name, self.Kind, token.Span, true, self.SymbolId, scopeId));
                continue;
            }

            Guid? resolved = ResolveSymbol(name, token.Span.Start, scopeId, definitions, promotions, scopes);
            occurrences.Add(new(name, "reference", token.Span, false, resolved, scopeId));
        }

        return new(occurrences);
    }

    public IReadOnlyList<DocumentSymbolInfo> Definitions => _occurrences
        .Where(x => x.IsDefinition)
        .Select(x => new DocumentSymbolInfo(x.Name, x.Kind, x.Span))
        .ToArray();

    public DocumentSymbolInfo? DefinitionAt(int position)
    {
        IndexedOccurrence? occurrence = OccurrenceAt(position);
        if (occurrence?.SymbolId is null) return null;
        IndexedOccurrence? definition = _occurrences.FirstOrDefault(x => x.IsDefinition && x.SymbolId == occurrence.SymbolId);
        return definition is null ? null : new DocumentSymbolInfo(definition.Name, definition.Kind, definition.Span);
    }

    public IReadOnlyList<DocumentSymbolInfo> ReferencesByName(string name) => _occurrences
        .Where(x => !x.IsDefinition && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        .Select(x => new DocumentSymbolInfo(x.Name, "reference", x.Span))
        .ToArray();

    public IReadOnlyList<TextSpan> RenameSpansAt(int position)
    {
        IndexedOccurrence? occurrence = OccurrenceAt(position);
        if (occurrence?.SymbolId is null) return Array.Empty<TextSpan>();
        return _occurrences.Where(x => x.SymbolId == occurrence.SymbolId).Select(x => x.Span).Distinct().ToArray();
    }

    private IndexedOccurrence? OccurrenceAt(int position) => _occurrences
        .Where(x => x.Span.Start <= position && position <= x.Span.End)
        .OrderBy(x => x.Span.Length)
        .FirstOrDefault();

    private static IReadOnlyList<PromotionSeed> BuildPromotions(List<DefinitionSeed> definitions, IReadOnlyList<PromotionScope> scopes)
    {
        var promotions = new List<PromotionSeed>();
        foreach (PromotionScope scope in scopes)
        {
            DefinitionSeed[] thenDefinitions = definitions.Where(x => x.ScopeId == scope.ThenScopeId && x.Kind == "variable").ToArray();
            DefinitionSeed[] elseDefinitions = definitions.Where(x => x.ScopeId == scope.ElseScopeId && x.Kind == "variable").ToArray();
            foreach (string name in thenDefinitions.Select(x => RootName(x.Token)).Intersect(elseDefinitions.Select(x => RootName(x.Token)), StringComparer.OrdinalIgnoreCase))
            {
                DefinitionSeed then = thenDefinitions.Where(x => RootName(x.Token).Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Token.Span.Start).First();
                DefinitionSeed otherwise = elseDefinitions.Where(x => RootName(x.Token).Equals(name, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.Token.Span.Start).First();
                Guid shared = Guid.NewGuid();
                then.SymbolId = shared;
                otherwise.SymbolId = shared;
                promotions.Add(new(name, scope.ParentScopeId, scope.ActivationPosition, shared));
            }
        }
        return promotions;
    }

    private static Guid? ResolveSymbol(string name, int position, int scopeId, IReadOnlyList<DefinitionSeed> definitions, IReadOnlyList<PromotionSeed> promotions, IReadOnlyList<ScopeInfo> scopes)
    {
        int? current = scopeId;
        while (current is not null)
        {
            DefinitionSeed? match = definitions
                .Where(x => x.ScopeId == current.Value && x.Token.Span.Start <= position && RootName(x.Token).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.Token.Span.Start)
                .FirstOrDefault();
            if (match is not null) return match.SymbolId;

            PromotionSeed? promoted = promotions
                .Where(x => x.ScopeId == current.Value && x.ActivationPosition <= position && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.ActivationPosition)
                .FirstOrDefault();
            if (promoted is not null) return promoted.SymbolId;

            current = scopes.First(x => x.Id == current.Value).ParentId;
        }
        return null;
    }

    private static void BuildScopes(IEnumerable<StatementNode> statements, int parentId, List<ScopeInfo> scopes, List<IteratorScope> iterators, List<PromotionScope> promotions)
    {
        foreach (StatementNode statement in statements)
        {
            switch (statement)
            {
                case IfNode conditional:
                    int thenId = AddScope(scopes, parentId, conditional.Then.Span);
                    BuildScopes(conditional.Then.Statements, thenId, scopes, iterators, promotions);
                    if (conditional.Else is not null)
                    {
                        int elseId = AddScope(scopes, parentId, conditional.Else.Span);
                        BuildScopes(conditional.Else.Statements, elseId, scopes, iterators, promotions);
                        promotions.Add(new(parentId, thenId, elseId, conditional.Span.End));
                    }
                    break;
                case ForEachNode loop:
                    int bodyId = AddScope(scopes, parentId, loop.Body.Span);
                    iterators.Add(new(loop.Variable, bodyId, loop.Span.Start, loop.Body.Span.Start));
                    BuildScopes(loop.Body.Statements, bodyId, scopes, iterators, promotions);
                    break;
            }
        }
    }

    private static int AddScope(List<ScopeInfo> scopes, int parentId, TextSpan span)
    {
        int id = scopes.Count;
        scopes.Add(new(id, parentId, span));
        return id;
    }

    private static int InnermostScope(IReadOnlyList<ScopeInfo> scopes, int position) => scopes
        .Where(x => x.Span.Start <= position && position <= x.Span.End)
        .OrderBy(x => x.Span.Length)
        .Select(x => x.Id)
        .FirstOrDefault();

    private static string? PreviousWord(IReadOnlyList<SyntaxToken> tokens, int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            if (tokens[i].Kind == TokenKind.NewLine) continue;
            return tokens[i].Kind == TokenKind.Word ? tokens[i].Text.ToUpperInvariant() : null;
        }
        return null;
    }

    private static string RootName(SyntaxToken token) => (token.Value?.ToString() ?? string.Empty).Split('.', 2)[0];
    private static bool SameSpan(TextSpan left, TextSpan right) => left.Start == right.Start && left.Length == right.Length;

    private sealed record ScopeInfo(int Id, int? ParentId, TextSpan Span);
    private sealed record IteratorScope(string Name, int ScopeId, int PrefixStart, int PrefixEnd);
    private sealed record PromotionScope(int ParentScopeId, int ThenScopeId, int ElseScopeId, int ActivationPosition);
    private sealed record PromotionSeed(string Name, int ScopeId, int ActivationPosition, Guid SymbolId);
    private sealed class DefinitionSeed(SyntaxToken token, int scopeId, string kind, Guid symbolId)
    {
        public SyntaxToken Token { get; } = token;
        public int ScopeId { get; } = scopeId;
        public string Kind { get; } = kind;
        public Guid SymbolId { get; set; } = symbolId;
    }
    private sealed record IndexedOccurrence(string Name, string Kind, TextSpan Span, bool IsDefinition, Guid? SymbolId, int ScopeId);
}
