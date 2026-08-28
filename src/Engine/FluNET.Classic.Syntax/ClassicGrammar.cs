using FluNET.Classic.Core;

namespace FluNET.Classic.Syntax;

public static class ClassicGrammar
{
    public static string GrammarId => ClassicLanguageContract.Id;

    public const string Ebnf = """
        script          = { statement , terminator } ;
        statement       = pipeline | if-statement | foreach-statement | try-statement | definition | record-definition | return-statement ;

        pipeline        = stage , { pipeline-continuation , stage } ;
        pipeline-continuation = "," , "THEN" ;

        stage           = sentence | check-stage | filter-stage | intrinsic-stage ;
        sentence        = verb , [ qualifier ] , [ implicit-value ] , { role-clause } , [ result-binding ] ;
        role-clause     = role , value-list ;
        value-list      = expression , { "," , expression } ;
        result-binding  = "INTO" , variable ;

        check-stage     = "CHECK" , "IF" , expression , [ result-binding ] ;
        filter-stage    = "FILTER" , [ expression ] , "WHERE" , expression , [ result-binding ] ;
        intrinsic-stage = intrinsic-surface ;

        if-statement    = "IF" , expression , "," , "THEN" , statements , [ "ELSE" , statements ] , "END" , "IF" , "." ;
        foreach-statement = "FOR" , "EACH" , variable , "IN" , expression , [ "," , "PARALLEL" , number ] , "," , "DO" , statements , "END" , "FOR" , "." ;
        try-statement   = "TRY" , "," , "DO" , statements , [ "ON" , "FAILURE" , statements ] , [ "FINALLY" , statements ] , "END" , "TRY" , "." ;
        definition      = "DEFINE" , ( "TASK" | "FUNCTION" ) , verb , [ qualifier ] , { parameter } , "RETURNING" , type , "," , "DO" , statements , "END" , ( "TASK" | "FUNCTION" ) , "." ;
        record-definition = "DEFINE" , "RECORD" , identifier , { "," , identifier , "AS" , type } , "." ;
        return-statement = "RETURN" , [ expression ] , "." ;
        statements      = { statement , "." } ;

        expression      = primary | unary-expression | binary-expression | predicate-expression | ternary-expression ;
        primary         = literal | variable | reference | identifier | "(" , expression , ")" ;
        literal         = string | number | "true" | "false" | "null" ;

        terminator      = "." ;

        (* verb, qualifier, role, predicate, operator and intrinsic surfaces are supplied by LanguageSnapshot. *)
        """;

    public static void EnsureCompatible(LanguageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
    }
}
