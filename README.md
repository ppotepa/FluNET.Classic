# FluNET.Classic

FluNET.Classic is a typed, sentence-oriented scripting language and runtime for .NET.

The language is compiled from CLR metadata: interfaces describe semantic roles, constructors describe binding/cardinality, attributes refine metadata, and reflection builds an immutable `LanguageSnapshot`. Source is parsed into an immutable AST, semantically bound to CLR overloads, and executed as a typed bound program.

Core examples:

```text
GET TEXT FROM {input.txt} AS [lines]
THEN TRANSFORM [lines] USING UPPER AS [upper]

FILTER [users] WHERE active IS true AS [activeUsers]

IF [response.status] IS 200 THEN SAY "ok" ELSE SAY "failed"

FOR EACH [user] IN [users] THEN SAY "Processing [user.name]"
```

Architecture:

```text
CLR types + interfaces + constructors + attributes
                    ↓
             LanguageCompiler
                    ↓
             LanguageSnapshot
                    ↓
source → lexer → parser → AST → binder → bound program → runtime
```

`main` is the only development line for FluNET.Classic. The project does not maintain a legacy runtime compatibility layer.
