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

The first standard wave includes `Text`, `Files`, `DateTime`, `OS`, `Process`, `Json`, `Http`, and typed collection filtering. Domain semantics increasingly live in CLR resource types instead of raw strings:

```text
LIST FILES IN {./logs} WITH "*.log" INTO [files].
GET METADATA FROM {config.json} INTO [metadata].

GET RESPONSE FROM {https://example.com} INTO [response],
THEN GET STATUS FROM [response] INTO [status];
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

`main` is the only development line for FluNET.Classic. The project does not maintain a legacy runtime compatibility layer; the parser does, however, accept the former `AS [variable]` result-binding spelling while `INTO [variable]` is the canonical form.
