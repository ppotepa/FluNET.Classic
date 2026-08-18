using FluNET.Classic.Core;

namespace FluNET.Classic.Syntax;

public static class ClassicGrammar
{
    public static LanguageVersionDescriptor LanguageVersion => ClassicLanguageVersions.V0_2;
    public static string GrammarId => LanguageVersion.GrammarId;

    public const string Ebnf = """
        script          = { statement , terminator } ;
        statement       = pipeline | if-statement | foreach-statement ;

        pipeline        = stage , { pipeline-continuation , stage } ;
        pipeline-continuation = [ "," ] , [ "AND" ] , "THEN" ;

        stage           = sentence | check-stage | filter-stage | intrinsic-stage ;
        sentence        = verb , [ qualifier ] , [ implicit-value ] , { role-clause } , [ result-binding ] ;
        role-clause     = role , value-list ;
        value-list      = expression , { "," , expression } ;
        result-binding  = "INTO" , variable ;

        check-stage     = "CHECK" , "IF" , expression , [ result-binding ] ;
        filter-stage    = "FILTER" , [ expression ] , "WHERE" , expression , [ result-binding ] ;
        intrinsic-stage = intrinsic-surface ;

        if-statement    = "IF" , expression , [ "," ] , "THEN" , body , [ "ELSE" , body ] ;
        foreach-statement = "FOR" , "EACH" , variable , "IN" , expression , [ "," ] , "THEN" , body ;
        body            = block | pipeline ;
        block           = "{" , { statement , terminator } , "}" ;

        expression      = primary | unary-expression | binary-expression | predicate-expression | ternary-expression ;
        primary         = literal | variable | reference | identifier | "(" , expression , ")" ;
        literal         = string | number | "true" | "false" | "null" ;

        terminator      = "." | ";" | newline | end-of-file ;

        (* verb, qualifier, role, predicate, operator and intrinsic surfaces are supplied by LanguageSnapshot. *)
        (* AS [variable] is accepted only as a legacy result-binding compatibility form; canonical output is INTO. *)
        """;

    public static void EnsureCompatible(LanguageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!LanguageVersion.IsCompatibleWith(snapshot.LanguageVersion))
            throw new InvalidOperationException($"Grammar '{GrammarId}' is not compatible with language snapshot '{snapshot.LanguageVersion}'.");
    }
}
