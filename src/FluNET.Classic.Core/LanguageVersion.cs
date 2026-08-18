namespace FluNET.Classic.Core;

public sealed record LanguageVersionDescriptor(string Name, Version Version, string GrammarId)
{
    public override string ToString() => Name;

    public bool IsCompatibleWith(LanguageVersionDescriptor other) =>
        other is not null && Version.Major == other.Version.Major && Version.Minor == other.Version.Minor;
}

public static class ClassicLanguageVersions
{
    public static LanguageVersionDescriptor V0_2 { get; } = new("0.2", new Version(0, 2), "flunet.classic.grammar/0.2");
    public static LanguageVersionDescriptor Current => V0_2;
}
