# Language

FluNET.Classic has one active language contract, `flunet.classic`. Package version `0.2.0-alpha.1` identifies the current development package baseline; it does not select a separate grammar version.

The structural grammar is implemented by `src/Engine/FluNET.Classic.Syntax/ClassicGrammar.cs`. The vocabulary layered onto that structure is compiled from CLR metadata into `LanguageSnapshot`.

A normal sentence follows the general shape:

```text
VERB [QUALIFIER] [WHAT] [ROLE value]... [INTO result].
```

Exact roles are supplied by typed sentence patterns rather than a global list of verb-specific parser rules. The same surface word can therefore be natural in different contexts while still binding to an explicit semantic role.

## Result binding

`INTO` always binds the result of the current stage and is the only canonical result-binding form:

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

## Contextual roles

The core role vocabulary is intentionally small:

- `FROM` — source.
- `TO` — destination or target state/representation, depending on the verb.
- `USING` — method, strategy, algorithm, or encoding.
- `WITH` — additional arguments or options.
- `AS` — interpretation or presentation.
- `IN` — collection/container context.
- `AT` — location/resource point.
- `FOR` — awaited or target subject where natural, for example processes.
- `UNTIL` — temporal deadline.
- `INTO` — result binding only.

For transformation sentences, the distinction is explicit:

```text
TRANSFORM [text] USING UPPER INTO [upper].
TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes].
TRANSFORM [bytes] TO TEXT USING UTF8 INTO [text].
TRANSFORM [text] TO JSON INTO [json].
```

`TO` describes what the value becomes, `USING` describes how it happens, and `INTO` names the produced result.

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

`IF`, `CHECK IF`, and `FILTER ... WHERE` share the same typed expression model.

```text
IF [response.status] IS 200, THEN
    SAY "ok".
ELSE
    SAY "failed".
END IF.

CHECK IF [status] IS 200 AND [active] IS true INTO [ok].
FILTER [users] WHERE Active IS true AND Age >= 18 INTO [active].
```

The expression grammar includes boolean composition, equality, comparison, and named typed predicates. Current operator surfaces include `NOT`, `AND`, `OR`, `IS`, `IS NOT`, `=`, `==`, `!=`, `>`, `<`, `>=`, and `<=`.

Named predicates are extensible through `PredicateRegistry`:

```text
CHECK IF [file] EXISTS INTO [exists].
CHECK IF {config.json} EXISTS INTO [exists].
CHECK IF [operation] IS OK INTO [ok].
CHECK IF [document] IS VALID INTO [valid].
```

Predicate support is type-aware. For example, `EXISTS`, `OK`, and `VALID` can bind to appropriate typed state interfaces as well as built-in supported values.

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

Operational failure handling is represented as a language structure and executes through the same bound runtime model as ordinary sentences.

## Script definitions

Script functions and tasks use typed role contracts and direct sentence invocation:

```text
DEFINE FUNCTION NORMALIZE, WHAT [value] AS TEXT, RETURNING TEXT, DO
    RETURN [value].
END FUNCTION.

NORMALIZE "hello" INTO [normalized].
```

Definitions execute in nested state and can participate in ordinary typed sentence binding. Non-void definitions must return a value through explicit `RETURN` behavior.

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

## Surface aliases and overload selection

A CLR role has one stable semantic identity, but a parameter can declare contextual surface aliases with `[RoleAlias]`. The parser preserves the explicit surface marker in the AST, and the binder resolves that marker against each candidate sentence pattern. An alias is therefore pattern-scoped rather than globally synonymous.

Qualifiers remain first-class metadata. Sentence overloads are selected using role shape, CLR type compatibility, cardinality, conversion/resolution cost, and qualifier metadata.

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

RUN {dotnet} WITH "--info" INTO [result].
GET STDOUT FROM [result] INTO [stdout].
CHECK IF [result] IS OK INTO [ok].

GET RESPONSE FROM {https://example.com} INTO [response],
THEN GET STATUS FROM [response] INTO [status].
```

Domain semantics should increasingly live in typed CLR resource/value types rather than raw strings when that improves binding, diagnostics, planning, or tooling.

## Extension boundary

Most language growth should come from typed CLR values, sentence patterns, modules, resolvers, converters, predicates, and capabilities rather than parser changes. See [`SDK.md`](SDK.md) for module authoring and [`ARCHITECTURE.md`](ARCHITECTURE.md) for compiler boundaries.
