namespace FluNET.Language;

public interface ILanguageElement
{
    string Name { get; }
}

public interface IAliasedLanguageElement
{
    IReadOnlyCollection<string> Aliases { get; }
}

public interface IDocumentedLanguageElement
{
    string? Description { get; }

    IReadOnlyCollection<string> Examples { get; }
}

public interface IRole
{
}

public interface IRole<out TValue> : IRole
{
}

public enum RoleDirection
{
    Input,
    Output,
    InputOutput
}

public enum RoleCardinality
{
    One,
    ZeroOrOne,
    OneOrMore,
    ZeroOrMore
}
