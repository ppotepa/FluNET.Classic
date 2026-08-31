namespace FluNET.Classic.Core;

public sealed record CompletionItem(string Label, string Kind, string? Detail = null);
public sealed record HoverInfo(string Label, string Detail);

public sealed class ClassicLanguageService(LanguageSnapshot language)
{
    public IReadOnlyList<CompletionItem> Complete(string? prefix = null)
    {
        prefix ??= string.Empty;
        var items = new List<CompletionItem>();
        items.AddRange(language.Verbs.SelectMany(verb => new[] { verb.Name }.Concat(verb.Aliases).Select(surface => new CompletionItem(surface, "verb", $"{verb.Implementations.Count} overload(s)"))));
        items.AddRange(language.Qualifiers.SelectMany(qualifier => new[] { qualifier.Name }.Concat(qualifier.AllAliases).Select(surface => new CompletionItem(surface, "qualifier", qualifier.TargetType?.Name))));
        items.AddRange(language.Verbs.SelectMany(v => v.Implementations).SelectMany(i => i.Patterns).SelectMany(p => p.Roles).SelectMany(r => r.AllSurfaceNames).Distinct(StringComparer.OrdinalIgnoreCase).Select(x => new CompletionItem(x, "role")));
        items.AddRange(language.Predicates.SelectMany(predicate => predicate.AllSurfaceNames.Select(surface => new CompletionItem(surface, "predicate", $"{predicate.Syntax}; precedence {predicate.Precedence}"))));
        items.AddRange(language.Operators.SelectMany(@operator => @operator.AllSurfaceNames.Select(surface => new CompletionItem(surface, "operator", $"{@operator.Arity}; precedence {@operator.Precedence}; {@operator.Compatibility}"))));
        items.AddRange(language.Intrinsics.SelectMany(intrinsic => intrinsic.AllSurfaceNames.Select(surface => new CompletionItem(surface, "intrinsic", $"{intrinsic.Syntax}; {intrinsic.Execution}"))));
        items.AddRange(language.StructuralSyntax.Select(surface => new CompletionItem(surface, "syntax")));
        items.AddRange(language.LiteralWords.Select(surface => new CompletionItem(surface, "literal")));
        return items
            .Where(x => x.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public HoverInfo? Hover(string token)
    {
        if (language.TryGetVerb(token, out VerbDescriptor verb))
            return new(verb.Name, $"{verb.Implementations.Count} overload(s): {string.Join("; ", verb.Implementations.Select(x => x.ResultType.Name).Distinct())}");
        if (language.TryGetQualifier(token, out QualifierDescriptor qualifier))
            return new(qualifier.Name, qualifier.TargetType?.FullName ?? "behavior qualifier");
        if (language.TryGetPredicate(token, out PredicateDescriptor predicate))
            return new(predicate.Name, $"Predicate: {predicate.Syntax}; precedence {predicate.Precedence}; operand types: {DescribeTypes(predicate.SupportedOperandTypes)}.");
        if (language.TryGetOperator(token, out OperatorDescriptor @operator))
            return new(@operator.Name, $"{@operator.Arity} operator; precedence {@operator.Precedence}; compatibility {@operator.Compatibility}; evaluation {@operator.Evaluation}.");
        if (language.TryGetIntrinsic(token, out IntrinsicDescriptor intrinsic))
            return new(intrinsic.Name, $"Intrinsic: {intrinsic.Syntax}; execution {intrinsic.Execution}{(intrinsic.StrategyType is null ? string.Empty : $"; {intrinsic.StrategyRole} {Friendly(intrinsic.StrategyType)}")}.");
        if (language.StructuralSyntax.Any(x => SplitSurface(x).Contains(token, StringComparer.OrdinalIgnoreCase)))
            return new(token.ToUpperInvariant(), "FluNET controlled-language structural syntax.");
        if (language.LiteralWords.Contains(token))
            return new(token.ToUpperInvariant(), "FluNET literal value.");
        return null;
    }

    private static string Friendly(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(',', type.GetGenericArguments().Select(Friendly))}>"
        : type.Name;
    private static string DescribeTypes(IReadOnlyList<Type> types) => types.Count == 0 ? "any" : string.Join(", ", types.Select(Friendly));
    private static IEnumerable<string> SplitSurface(string surface) => surface.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
