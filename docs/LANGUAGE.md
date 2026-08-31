# Language

FluNET.Classic has one active language contract, `flunet.classic`. Package version `0.2.0-alpha.2` identifies the current development package; it does not select a separate grammar version.

The structural grammar is implemented by `src/Engine/FluNET.Classic.Syntax/ClassicGrammar.cs`. The vocabulary layered onto that structure is compiled from CLR metadata into `LanguageSnapshot`.

A normal sentence follows the general shape:

```text
VERB [QUALIFIER] [WHAT] [ROLE value]... [INTO result].
```

Exact roles are supplied by typed sentence patterns rather than a global list of verb-specific parser rules. The same surface word can therefore be natural in different contexts while still binding to an explicit semantic role.

## Canonical role vocabulary

`0.2.0-alpha.2` defines one canonical contextual role catalog shared by the compiler, parser, SDK, tooling, and tests:

- `WHAT` — primary subject/value.
- `FROM` — source.
- `TO` — destination or target representation/state.
- `USING` — method, strategy, algorithm, encoding, or mode.
- `WITH` — additional arguments or options.
- `AS` — interpretation or presentation.
- `IN` — collection/container context.
- `AT` — location/resource point.
- `FOR` — awaited or target subject where natural, for example processes.
- `UNTIL` — temporal deadline.
- `BY` — selector/key/grouping criterion where the sentence pattern uses a role rather than structural intrinsic syntax.

`INTO` is not a CLR role. It is language-level result binding. `THEN` is not a CLR role either; it is structural pipeline/control-flow syntax.

## Result binding

`INTO` always binds the result of the current stage and is the only result-binding form:

```text
GET TEXT FROM {file.txt} INTO [text].
PARSE [text] AS JSON INTO [data].
CHECK IF [response] IS OK INTO [ok].
```

`AS` remains an interpretation or presentation role:

```text
GET {file.txt} AS TEXT INTO [text].
PARSE [text] AS JSON INTO [data].
FORMAT [data] AS JSON INTO [text].
```

`CHECK IF` and `FILTER ... WHERE` do not carry a legacy `AS [variable]` result-binding form. Use `INTO` consistently.

## Transformation semantics

Transformation target, method, and result are deliberately separate:

```text
TRANSFORM [text] USING UPPER INTO [upper].
TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes].
TRANSFORM [bytes] TO TEXT USING UTF8 INTO [text].
TRANSFORM [text] TO JSON INTO [json].
```

- `TO` describes what the value becomes.
- `USING` describes how the transformation happens.
- `INTO` names the produced result.
- `AS` is not a `TRANSFORM` role.

The compiler and SDK validate this convention so modules do not create competing transformation dialects.

## Pipelines and punctuation

```text
GET TEXT FROM {input.txt},
THEN TRANSFORM USING TRIM,
THEN TRANSFORM USING UPPER,
THEN SAVE TO {output.txt}.
```

- `, THEN` passes the typed result of the previous stage into one compatible missing input role of the next stage.
- `.` is mandatory and ends every complete statement, including the final statement in a file or block.
- `,` separates role values and introduces a pipeline continuation only when followed by `THEN`.
- Newlines are formatting whitespace and do not terminate statements.
- `;` and `AND THEN` are not canonical language constructs.
- Structured blocks use explicit endings such as `END IF.`, `END FOR.`, and `END TRY.`.

## Conditions

`IF`, `CHECK IF`, and `FILTER ... WHERE` share the same typed expression model and operator/predicate descriptors.

```text
IF [response.status] IS 200, THEN
    SAY "ok".
ELSE
    SAY "failed".
END IF.

CHECK IF [status] IS 200 AND [active] IS true INTO [ok].
FILTER [users] WHERE Active IS true AND Age >= 18 INTO [active].
```

The expression grammar includes boolean composition, equality, ordering, membership/string/temporal operators, and named typed predicates. Core surfaces include `NOT`, `AND`, `OR`, `IS`, `IS NOT`, `=`, `==`, `!=`, `>`, `<`, `>=`, and `<=`, with additional descriptors such as `CONTAINS`, `STARTS WITH`, `ENDS WITH`, `MATCHES`, `IN`, `BEFORE`, `AFTER`, and `BETWEEN` supplied through the compiled language surface.

Named predicates are extensible through `PredicateRegistry`:

```text
CHECK IF [file] EXISTS INTO [exists].
CHECK IF {config.json} EXISTS INTO [exists].
CHECK IF [operation] IS OK INTO [ok].
CHECK IF [document] IS VALID INTO [valid].
```

Predicate support is type-aware. `EXISTS`, `OK`, `VALID`, and other registered predicates bind only to compatible operand types.

## Iteration

```text
FOR EACH [user] IN [users], DO
    SAY "Processing [user.name]".
END FOR.
```

Bounded concurrency is explicit:

```text
FOR EACH [user] IN [users], PARALLEL 4, DO
    SAY "Processing [user.name]".
END FOR.
```

Parallel iterations use isolated local binding scopes rather than sharing mutable local state.

## Failure handling

```text
TRY, DO
    SEND EMAIL TO {ops@example.com} WITH "report".
ON FAILURE
    SAY "Could not send report".
FINALLY
    SAY "Finished".
END TRY.
```

Operational failure handling is represented as a structural language construct and executes through the same bound runtime model as ordinary sentences.

## Script definitions

Script functions and tasks use the same canonical role vocabulary as CLR sentence patterns:

```text
DEFINE FUNCTION NORMALIZE, WHAT [value] AS TEXT, RETURNING TEXT, DO
    RETURN [value].
END FUNCTION.

NORMALIZE "hello" INTO [normalized].
```

A definition parameter role outside the canonical catalog is a syntax error. Definitions execute in nested state and can participate in ordinary typed sentence binding. Non-void definitions must return a value through explicit `RETURN` behavior.

Immutable records are schema-backed values:

```text
DEFINE RECORD USER, NAME AS TEXT, AGE AS INTEGER.
MAKE USER WITH "Ada", 42 INTO [user].
```

## Variadic values

A typed variadic role can accept multiple values naturally:

```text
GET TEXT FROM {a.txt}, {b.txt}, {c.txt} INTO [allLines].
```

On the CLR side, `params T[]` represents variadic sentence cardinality. A plain `T[]` is one collection-valued role rather than an automatically variadic role.

## Surface aliases

A CLR role has one stable semantic identity, but a parameter can declare a contextual surface alias with `[RoleAlias]`. The parser preserves the source spelling and the binder resolves it against the candidate pattern, so an alias is pattern-scoped rather than a global synonym.

Aliases are intentionally narrow. Do not add `FROM` merely to make an `IN` container or `AT` location accept more spellings. The standard Files and Storage surfaces use canonical `LIST ... IN ...` and `DELETE ... AT ...`. A cross-role alias should exist only when the alternate wording is genuinely natural for that specific pattern; HTTP resource reads, for example, can deliberately expose `AT` alongside the semantic `FROM` source role.

Structural-only words such as `INTO`, `THEN`, `ELSE`, `IF`, `WHERE`, `DO`, and `END` cannot be claimed by role metadata.

## Standard domains

The default host composes standard vocabulary for:

- text,
- files,
- date/time,
- operating-system values,
- processes,
- JSON,
- HTTP,
- collections.

Examples:

```text
GET NOW INTO [now].
PARSE DATE FROM "2026-08-17" INTO [date].
TRANSFORM [now] TO UTC INTO [utc].
WAIT UNTIL [deadline].

GET ENV {PATH} INTO [path].
GET OS INTO [os].
GET USER INTO [user].
GET CWD INTO [cwd].

LIST FILES IN {./logs} WITH "*.log" INTO [files].
DELETE FILE AT {./old.log} INTO [deleted].

RUN {dotnet} WITH "--info" INTO [result].
GET STDOUT FROM [result] INTO [stdout].
CHECK IF [result] IS OK INTO [ok].

GET RESPONSE FROM {https://example.com} INTO [response],
THEN GET STATUS FROM [response] INTO [status].
```

HTTP endpoint-oriented operations use the typed `HttpEndpoint` resource value rather than exposing raw `Uri` as the sentence-domain contract. More generally, domain semantics should live in typed CLR resource/value types whenever that improves binding, diagnostics, planning, or tooling.

## Executable documentation

[`examples/language-surface.flu`](examples/language-surface.flu) is a maintained executable fixture. The test project copies it into the test output and runs it through the production engine so the documented structural surface does not silently drift away from the parser/binder/runtime contract.

## Extension boundary

Most language growth should come from typed CLR values, canonical sentence roles, sentence patterns, modules, resolvers, converters, predicates, capabilities, and execution traits rather than parser changes. `LanguageSurfaceValidation` enforces the core role/structural/transform invariants during language compilation, and the SDK adds quality diagnostics for module authors.

See [`SDK.md`](SDK.md) for module authoring and [`ARCHITECTURE.md`](ARCHITECTURE.md) for compiler boundaries.
