# FluNET.Classic

FluNET.Classic is a typed, sentence-oriented scripting language and runtime for .NET. Its surface syntax is a controlled natural language: sentences are intentionally small and deterministic, but read in the same direction as ordinary English.

The repository is organized by responsibility:

```text
src/Engine    language contracts, lexer/parser, binding, and execution
src/Modules   standard vocabulary, domain modules, providers, and bundles
src/Tooling   SDK and editor/document services
src/Hosts     dependency-injection composition, CLI, and language server
tests         behavioral, integration, and architecture tests
demo          runnable FluNET programs
```

The dependency direction is intentionally one-way: Engine does not know about
modules or tools; Modules depend on Engine; Tooling depends on Engine; Hosts
compose Engine, Modules, and Tooling.

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

IF [ok] IS true, THEN
    SAY "ok".
ELSE
    SAY "failed".
END IF.

FOR EACH [user] IN [users], DO
    SAY "Processing [user.name]".
END FOR.
```

Sentence punctuation is deliberately small and strict: `.` ends every complete statement, while `, THEN` continues a typed pipeline. Commas also separate values within a role; newlines are formatting whitespace. Semicolons and `AND THEN` are not language constructs. `INTO [name]` binds a result; `TO`, `USING`, `AS`, `FROM`, `WITH`, `IN`, `AT`, `FOR`, and `UNTIL` are contextual roles selected by the verb pattern.

Projects use a small `flu.json` manifest. It names an `entry` script, optional `sources`, module package versions, allowed `capabilities`, and execution defaults such as `timeout` and `parallelism`. Operational failure handling uses the same sentence-shaped style:

```flu
TRY, DO
    SEND EMAIL TO {ops@example.com} WITH "report".
ON FAILURE
    SAY "Could not send report".
FINALLY
    SAY "Finished".
END TRY.
```

Tasks and functions are declared with typed role contracts and called as ordinary verbs:

```flu
DEFINE FUNCTION NORMALIZE, WHAT [value] AS TEXT, RETURNING TEXT, DO
    RETURN [value].
END FUNCTION.

NORMALIZE "hello" INTO [normalized].
```

Immutable records are declared and constructed with the same role-oriented surface:

```flu
DEFINE RECORD USER, NAME AS TEXT, AGE AS INTEGER.
MAKE USER WITH "Ada", 42 INTO [user].
```

`FOR EACH` can opt into bounded concurrency explicitly with `, PARALLEL n, DO`; each iteration receives an isolated local state.

The first standard wave includes `Text`, `Files`, `DateTime`, `OS`, `Process`, `Json`, `Http`, and typed collection filtering. Domain semantics increasingly live in CLR resource types instead of raw strings:

```text
LIST FILES IN {./logs} WITH "*.log" INTO [files].
GET METADATA FROM {config.json} INTO [metadata].

GET RESPONSE FROM {https://example.com} INTO [response],
THEN GET STATUS FROM [response] INTO [status].
CHECK IF [response] IS OK INTO [ok].
```

HTTP exposes `HttpEndpoint`, `HttpResponse`, `HttpStatus`, and `HttpHeaders`. A response can be projected as `STATUS`, `HEADERS`, `TEXT`, or `JSON` through ordinary typed `GET` overloads; no HTTP-specific parser syntax is required.

```text
CLR types + interfaces + constructors + attributes
                    ↓
             LanguageCompiler
                    ↓
             LanguageSnapshot
                    ↓
source → lexer → parser → AST → binder → bound program → runtime
                    |                 ↓
                    |           execution plan
                    ↓
              document tooling
```

The CLI exposes canonical formatting, static checking, planning and explanation:

```text
flu check script.flu
flu format script.flu
flu plan script.flu
flu explain script.flu
```

`flu plan` does not execute the script. It exposes selected overloads, typed role bindings, resolution/conversion information, result types, required capabilities, and execution traits so a program can be inspected before runtime.

Module authors should reference `FluNET.Classic.SDK`. `FluNetModuleTestHarness` validates modules and their example sentences, while `ModuleArtifactGenerator` generates module manifests and Markdown documentation directly from `LanguageSnapshot` metadata. See `docs/SDK.md`.

Editor and IDE integrations should build on `FluNET.Classic.Tooling`. `ClassicDocumentService` provides diagnostics, completion, hover, formatting and planning by reusing the production compiler pipeline; a future LSP server can remain a thin protocol adapter. See `docs/TOOLING.md`.

`main` is the only development line for FluNET.Classic. The project has one language contract: there is no legacy parser, compatibility mode, or second runtime. Breaking language improvements are made directly on `main` while the project is pre-1.0.
