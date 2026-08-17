using System.Collections;

namespace FluNET.Classic.Core;

public enum PredicateSyntaxKind
{
    Postfix,
    IsState
}

public sealed record PredicateCapabilityRequirement(string Capability, Type? OperandType = null)
{
    public bool AppliesTo(Type operandType)
    {
        if (OperandType is null) return true;
        Type expected = Nullable.GetUnderlyingType(OperandType) ?? OperandType;
        Type actual = Nullable.GetUnderlyingType(operandType) ?? operandType;
        return expected == actual || expected.IsAssignableFrom(actual);
    }
}

public sealed record PredicateDescriptor(
    string StableId,
    string Name,
    PredicateSyntaxKind Syntax,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<Type>? OperandTypes = null,
    IReadOnlyList<PredicateCapabilityRequirement>? Capabilities = null,
    Type? ReferenceOperandType = null)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyList<Type> SupportedOperandTypes => OperandTypes ?? Array.Empty<Type>();
    public IReadOnlyList<PredicateCapabilityRequirement> CapabilityRequirements => Capabilities ?? Array.Empty<PredicateCapabilityRequirement>();
    public IReadOnlyList<string> RequiredCapabilities => CapabilityRequirements.Select(x => x.Capability).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> CapabilitiesFor(Type operandType) => CapabilityRequirements
        .Where(x => x.AppliesTo(operandType))
        .Select(x => x.Capability)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool CanApplyTo(Type operandType)
    {
        if (SupportedOperandTypes.Count == 0) return true;
        Type effective = Nullable.GetUnderlyingType(operandType) ?? operandType;
        return SupportedOperandTypes.Any(type =>
        {
            Type supported = Nullable.GetUnderlyingType(type) ?? type;
            return supported == effective || supported.IsAssignableFrom(effective);
        });
    }
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

public enum OperatorSemanticKind
{
    Custom,
    Logical,
    Equality,
    Ordering,
    Contains,
    StartsWith,
    EndsWith,
    RegexMatch,
    Membership,
    Temporal,
    Between
}

public sealed record OperatorDescriptor(
    string StableId,
    string Name,
    int Precedence,
    OperatorArity Arity = OperatorArity.Binary,
    OperatorAssociativity Associativity = OperatorAssociativity.Left,
    IReadOnlyList<string>? Aliases = null,
    OperatorSemanticKind Semantic = OperatorSemanticKind.Custom)
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
        new(
            "predicate:exists",
            "EXISTS",
            PredicateSyntaxKind.Postfix,
            OperandTypes: new[] { typeof(FileInfo), typeof(DirectoryInfo), typeof(IExistenceState) },
            Capabilities: new[] { new PredicateCapabilityRequirement(StandardCapabilities.FileSystemRead, typeof(FileSystemInfo)) },
            ReferenceOperandType: typeof(FileInfo)),
        new("predicate:ok", "OK", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(bool), typeof(IOkState) }),
        new("predicate:valid", "VALID", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(bool), typeof(IValidState) }),
        new("predicate:empty", "EMPTY", PredicateSyntaxKind.IsState, OperandTypes: new[] { typeof(string), typeof(IEnumerable) })
    };

    public static IReadOnlyList<OperatorDescriptor> Operators { get; } = new OperatorDescriptor[]
    {
        new("operator:not", "NOT", 6, OperatorArity.Unary, OperatorAssociativity.Right, Semantic: OperatorSemanticKind.Logical),
        new("operator:or", "OR", 1, Semantic: OperatorSemanticKind.Logical),
        new("operator:and", "AND", 2, Semantic: OperatorSemanticKind.Logical),
        new("operator:contains", "CONTAINS", 3, Semantic: OperatorSemanticKind.Contains),
        new("operator:starts-with", "STARTS WITH", 3, Semantic: OperatorSemanticKind.StartsWith),
        new("operator:ends-with", "ENDS WITH", 3, Semantic: OperatorSemanticKind.EndsWith),
        new("operator:matches", "MATCHES", 3, Semantic: OperatorSemanticKind.RegexMatch),
        new("operator:in", "IN", 3, Semantic: OperatorSemanticKind.Membership),
        new("operator:before", "BEFORE", 3, Semantic: OperatorSemanticKind.Temporal),
        new("operator:after", "AFTER", 3, Semantic: OperatorSemanticKind.Temporal),
        new("operator:between", "BETWEEN", 3, OperatorArity.Ternary, Semantic: OperatorSemanticKind.Between),
        new("operator:is-not", "IS NOT", 4, Semantic: OperatorSemanticKind.Equality),
        new("operator:is", "IS", 4, Semantic: OperatorSemanticKind.Equality),
        new("operator:eq", "=", 4, Aliases: new[] { "==" }, Semantic: OperatorSemanticKind.Equality),
        new("operator:neq", "!=", 4, Semantic: OperatorSemanticKind.Equality),
        new("operator:gte", ">=", 4, Semantic: OperatorSemanticKind.Ordering),
        new("operator:lte", "<=", 4, Semantic: OperatorSemanticKind.Ordering),
        new("operator:gt", ">", 4, Semantic: OperatorSemanticKind.Ordering),
        new("operator:lt", "<", 4, Semantic: OperatorSemanticKind.Ordering)
    };

    public static IReadOnlySet<string> ReservedWords { get; } = new HashSet<string>(
        new[] { "INTO", "THEN", "ELSE", "IF", "WHERE", "FOR", "EACH", "AS", "TRUE", "FALSE", "NULL" }
            .Concat(Predicates.SelectMany(x => x.AllSurfaceNames))
            .Concat(Operators.SelectMany(x => x.AllSurfaceNames).SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))),
        StringComparer.OrdinalIgnoreCase);
}
