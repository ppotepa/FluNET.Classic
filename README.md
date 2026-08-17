# FluNET.Classic

FluNET.Classic is a typed, sentence-oriented scripting language and runtime for .NET. Its surface syntax is a controlled natural language: sentences are intentionally small and deterministic, but read in the same direction as ordinary English.

The language is compiled from CLR metadata: interfaces describe semantic roles, constructors describe binding/cardinality, attributes refine metadata, and reflection builds an immutable `LanguageSnapshot`. Source is parsed into an immutable AST, semantically bound to CLR overloads, and executed as a typed bound program.

```text
GET TEXT FROM {input.txt} INTO [lines],
THEN TRANSFORM USING UPPER INTO [upper],
THEN SAVE TO {output.txt}.

GET {input.txt} AS TEXT INTO [content].
PARSE [jsonText] AS JSON INTO [data].
TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes].

CHECK IF [response.status] IS 200 INTO [ok].
FILTER [users] WHERE Active IS true AND Age >= 18 INTO [activeUsers].

IF [ok] IS true THEN SAY "ok" ELSE SAY "failed"
FOR EACH [user] IN [users] THEN SAY "Processing [user.name]"
```

Sentence punctuation is semantic but small: `.` ends a complete statement, `;` separates independent statements, `,` is a soft separator, and `THEN`/`AND THEN` continues a typed pipeline. `INTO [name]` binds a result; `TO`, `USING`, `AS`, `FROM`, `WITH`, `IN`, `AT`, `FOR`, and `UNTIL` are contextual roles selected by the verb pattern.

The first standard wave now includes `Text`, `Files`, `DateTime`, `OS`, `Process`, `Json`, `Http`, and typed collection filtering. Examples include `GET NOW`, `GET OS`, `RUN {dotnet} WITH "--info"`, `CHECK IF [result] IS OK`, and `LIST FILES IN {./logs}`.

```text
CLR types + interfaces + constructors + attributes
                    ↓
             LanguageCompiler
                    ↓
             LanguageSnapshot
                    ↓
source → lexer → parser → AST → binder → bound program → runtime
```

The CLI also exposes the canonical formatter:

```text
flu format script.flu
```

`main` is the only development line for FluNET.Classic. The project does not maintain a legacy runtime compatibility layer; the parser does, however, accept the former `AS [variable]` result-binding spelling while `INTO [variable]` is the canonical form.
