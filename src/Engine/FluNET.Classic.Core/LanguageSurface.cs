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
    Type? ReferenceOperandType = null,
    int Precedence = 7)
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

public enum OperatorCompatibilityRule
{
    Any,
    BooleanOperand,
    BooleanPair,
    ComparablePair,
    OrderedPair,
    ContainerContainsValue,
    ValueInContainer,
    StringPair,
    TemporalPair
}

public enum OperatorEvaluationKind
{
    Custom,
    LogicalNot,
    LogicalAnd,
    LogicalOr,
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith,
    RegexMatch,
    Membership,
    Before,
    After,
    Between
}

public sealed record OperatorDescriptor(
    string StableId,
    string Name,
    int Precedence,
    OperatorArity Arity = OperatorArity.Binary,
    OperatorAssociativity Associativity = OperatorAssociativity.Left,
    IReadOnlyList<string>? Aliases = null,
    OperatorSemanticKind Semantic = OperatorSemanticKind.Custom,
    OperatorCompatibilityRule Compatibility = OperatorCompatibilityRule.Any,
    OperatorEvaluationKind Evaluation = OperatorEvaluationKind.Custom,
    Type? ResultType = null)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public Type EffectiveResultType => ResultType ?? typeof(bool);
}

public enum IntrinsicSyntaxKind
{
    CollectionBy,
    CollectionAmountFrom,
    CollectionDistinct,
    CollectionSourceOptional
}

public enum IntrinsicExecutionKind
{
    Streaming,
    Materializing,
    Scalar
}

public enum IntrinsicSemanticKind
{
    Custom,
    Sort,
    Group,
    Take,
    Skip,
    Distinct,
    Count
}

public sealed record IntrinsicDescriptor(
    string StableId,
    string Name,
    IntrinsicSyntaxKind Syntax,
    IReadOnlyList<string>? Aliases = null,
    IntrinsicExecutionKind Execution = IntrinsicExecutionKind.Materializing,
    Type? StrategyType = null,
    string StrategyRole = "USING",
    IntrinsicSemanticKind Semantic = IntrinsicSemanticKind.Custom)
{
    public IReadOnlyList<string> AllSurfaceNames => new[] { Name }
        .Concat(Aliases ?? Array.Empty<string>())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool AcceptsStrategy => StrategyType is not null;
}

public static class StandardLanguageSurface
{
    public static IReadOnlyList<string> StructuralSyntax { get; } = new[]
    {
        "INTO", "THEN", "IF", "WHERE", "ELSE", "FOR", "EACH", "DO", "END", "AS", "TRY", "ON", "FAILURE", "FINALLY", "DEFINE", "TASK", "FUNCTION", "RETURNING", "RETURN", "RECORD", "MAKE"
    };

    public static IReadOnlySet<string> LiteralWords { get; } = new HashSet<string>(
        new[] { "TRUE", "FALSE", "NULL" },
        StringComparer.OrdinalIgnoreCase);

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
        new("operator:not", "NOT", 6, OperatorArity.Unary, OperatorAssociativity.Right, Semantic: OperatorSemanticKind.Logical, Compatibility: OperatorCompatibilityRule.BooleanOperand, Evaluation: OperatorEvaluationKind.LogicalNot),
        new("operator:or", "OR", 1, Semantic: OperatorSemanticKind.Logical, Compatibility: OperatorCompatibilityRule.BooleanPair, Evaluation: OperatorEvaluationKind.LogicalOr),
        new("operator:and", "AND", 2, Semantic: OperatorSemanticKind.Logical, Compatibility: OperatorCompatibilityRule.BooleanPair, Evaluation: OperatorEvaluationKind.LogicalAnd),
        new("operator:contains", "CONTAINS", 3, Semantic: OperatorSemanticKind.Contains, Compatibility: OperatorCompatibilityRule.ContainerContainsValue, Evaluation: OperatorEvaluationKind.Contains),
        new("operator:starts-with", "STARTS WITH", 3, Semantic: OperatorSemanticKind.StartsWith, Compatibility: OperatorCompatibilityRule.StringPair, Evaluation: OperatorEvaluationKind.StartsWith),
        new("operator:ends-with", "ENDS WITH", 3, Semantic: OperatorSemanticKind.EndsWith, Compatibility: OperatorCompatibilityRule.StringPair, Evaluation: OperatorEvaluationKind.EndsWith),
        new("operator:matches", "MATCHES", 3, Semantic: OperatorSemanticKind.RegexMatch, Compatibility: OperatorCompatibilityRule.StringPair, Evaluation: OperatorEvaluationKind.RegexMatch),
        new("operator:in", "IN", 3, Semantic: OperatorSemanticKind.Membership, Compatibility: OperatorCompatibilityRule.ValueInContainer, Evaluation: OperatorEvaluationKind.Membership),
        new("operator:before", "BEFORE", 3, Semantic: OperatorSemanticKind.Temporal, Compatibility: OperatorCompatibilityRule.TemporalPair, Evaluation: OperatorEvaluationKind.Before),
        new("operator:after", "AFTER", 3, Semantic: OperatorSemanticKind.Temporal, Compatibility: OperatorCompatibilityRule.TemporalPair, Evaluation: OperatorEvaluationKind.After),
        new("operator:between", "BETWEEN", 3, OperatorArity.Ternary, Semantic: OperatorSemanticKind.Between, Compatibility: OperatorCompatibilityRule.OrderedPair, Evaluation: OperatorEvaluationKind.Between),
        new("operator:is-not", "IS NOT", 4, Semantic: OperatorSemanticKind.Equality, Compatibility: OperatorCompatibilityRule.ComparablePair, Evaluation: OperatorEvaluationKind.NotEqual),
        new("operator:is", "IS", 4, Semantic: OperatorSemanticKind.Equality, Compatibility: OperatorCompatibilityRule.ComparablePair, Evaluation: OperatorEvaluationKind.Equal),
        new("operator:eq", "=", 4, Aliases: new[] { "==" }, Semantic: OperatorSemanticKind.Equality, Compatibility: OperatorCompatibilityRule.ComparablePair, Evaluation: OperatorEvaluationKind.Equal),
        new("operator:neq", "!=", 4, Semantic: OperatorSemanticKind.Equality, Compatibility: OperatorCompatibilityRule.ComparablePair, Evaluation: OperatorEvaluationKind.NotEqual),
        new("operator:gte", ">=", 4, Semantic: OperatorSemanticKind.Ordering, Compatibility: OperatorCompatibilityRule.OrderedPair, Evaluation: OperatorEvaluationKind.GreaterThanOrEqual),
        new("operator:lte", "<=", 4, Semantic: OperatorSemanticKind.Ordering, Compatibility: OperatorCompatibilityRule.OrderedPair, Evaluation: OperatorEvaluationKind.LessThanOrEqual),
        new("operator:gt", ">", 4, Semantic: OperatorSemanticKind.Ordering, Compatibility: OperatorCompatibilityRule.OrderedPair, Evaluation: OperatorEvaluationKind.GreaterThan),
        new("operator:lt", "<", 4, Semantic: OperatorSemanticKind.Ordering, Compatibility: OperatorCompatibilityRule.OrderedPair, Evaluation: OperatorEvaluationKind.LessThan)
    };

    public static IReadOnlySet<string> ReservedWords { get; } = new HashSet<string>(
        StructuralSyntax.SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Concat(LiteralWords)
            .Concat(Predicates.SelectMany(x => x.AllSurfaceNames))
            .Concat(Operators.SelectMany(x => x.AllSurfaceNames).SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries))),
        StringComparer.OrdinalIgnoreCase);
}
