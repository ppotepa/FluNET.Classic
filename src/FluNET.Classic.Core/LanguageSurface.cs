using System.Collections;

namespace FluNET.Classic.Core;

public enum PredicateSyntaxKind
{
    Postfix,
    IsState
}

public sealed record PredicateDescriptor(
    string StableId,
    string Name,
    PredicateSyntaxKind Syntax,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<Type>? OperandTypes = null,
    IReadOnlyList<string>? Capabilities = null)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<Type> SupportedOperandTypes => OperandTypes ?? Array.Empty<Type>();
    public IReadOnlyList<string> RequiredCapabilities => Capabilities ?? Array.Empty<string>();
}

public enum OperatorArity
{
    Unary,
    Binary,
    Ternary
}

public enum OperatorAssociativity
{
    Left,
    Right
}

public sealed record OperatorDescriptor(
    string StableId,
    string Name,
    int Precedence,
    OperatorArity Arity = OperatorArity.Binary,
    OperatorAssociativity Associativity = OperatorAssociativity.Left,
    IReadOnlyList<string>? Aliases = null)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public enum IntrinsicSyntaxKind
{
    CollectionBy,
    CollectionAmountFrom,
    CollectionDistinct,
    CollectionSourceOptional
}

public sealed record IntrinsicDescriptor(
    string StableId,
    string Name,
    IntrinsicSyntaxKind Syntax,
    IReadOnlyList<string>? Aliases = null)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public static class StandardLanguageSurface
{
    public static IReadOnlyList<PredicateDescriptor> Predicates { get; } = new PredicateDescriptor[]
    {
        new("predicate:exists", "EXISTS", PredicateSyntaxKind.Postfix, OperandTypes: new[] { typeof(FileSystemInfo), typeof(IExistenceState) }, Capabilities: new[] { StandardCapabilities.FileSystemRead }),
        new("predicate:ok", "OK", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(bool), typeof(IOkState) }),
        new("predicate:valid", "VALID", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(bool), typeof(IValidState) }),
        new("predicate:empty", "EMPTY", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(string), typeof(IEnumerable) })
    };

    public static IReadOnlyList<OperatorDescriptor> Operators { get; } = new OperatorDescriptor[]
    {
        new("operator:not", "NOT", 6, OperatorArity.Unary, OperatorAssociativity.Right),
        new("operator:or", "OR", 1),
        new("operator:and", "AND", 2),
        new("operator:contains", "CONTAINS", 3),
        new("operator:starts-with", "STARTS WITH", 3),
        new("operator:ends-with", "ENDS WITH", 3),
        new("operator:matches", "MATCHES", 3),
        new("operator:in", "IN", 3),
        new("operator:before", "BEFORE", 3),
        new("operator:after", "AFTER", 3),
        new("operator:between", "BETWEEN", 3, OperatorArity.Ternary),
        new("operator:is-not", "IS NOT", 4),
        new("operator:is", "IS", 4),
        new("operator:eq", "=", 4, Aliases: new[] { "==" }),
        new("operator:neq", "!=", 4),
        new("operator:gte", ">=", 4),
        new("operator:lte", "<=", 4),
        new("operator:gt", ">", 4),
        new("operator:lt", "<", 4)
    };

    public static IReadOnlySet<string> ReservedWords { get; } = new HashSet<string>(
        new[] { "INTO", "THEN", "ELSE", "IF", "WHERE", "FOR", "EACH", "AS", "TRUE", "FALSE", "NULL" }
            .Concat(Predicates.SelectMany(x => x.AllSurfaceNames))
            .Concat(Operators.SelectMany(x => x.AllSurfaceNames).SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))),
        StringComparer.OrdinalIgnoreCase);
}
