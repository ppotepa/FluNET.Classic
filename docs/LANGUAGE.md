# Language

FluNET.Classic is a typed controlled natural language. A sentence follows the general shape:

```text
VERB [QUALIFIER] [WHAT] [ROLE value]... [INTO result].
```

The exact roles are supplied by CLR sentence patterns rather than a global grammar. The same surface word can therefore be natural in different contexts while still binding to an explicit semantic role.

## Result binding

`INTO` always binds the result of the current stage and is the only result-binding form:

```text
GET TEXT FROM {file.txt} INTO [lines].
PARSE [text] AS JSON INTO [data].
CHECK IF [response.status] IS 200 INTO [ok].
```

`AS` is otherwise available as a representation/interpretation role:

```text
GET {file.txt} AS TEXT INTO [lines].
PARSE [text] AS JSON INTO [data].
FORMAT [data] AS JSON INTO [text].
```

## Contextual roles

The core vocabulary is intentionally small:

- `FROM` — source.
- `TO` — destination or target state/representation, depending on the verb.
- `USING` — method, strategy, algorithm, or encoding.
- `WITH` — additional arguments/options.
- `AS` — interpretation or presentation.
- `IN` — collection/container context.
- `AT` — location/resource point.
- `FOR` — awaited/target subject where natural, for example processes.
- `UNTIL` — temporal deadline.
- `INTO` — result binding only.

For `TRANSFORM`, the distinction is explicit:

```text
TRANSFORM [text] USING UPPER INTO [upper].
TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes].
TRANSFORM [bytes] TO TEXT USING UTF8 INTO [text].
TRANSFORM [text] TO JSON INTO [json].
```

`TO` describes what the value becomes, `USING` describes how it happens, and `INTO` names the produced value.

## Pipelines and punctuation

```text
GET TEXT FROM {input.txt},
THEN TRANSFORM USING TRIM,
THEN TRANSFORM USING UPPER,
THEN SAVE TO {output.txt}.
```

- `, THEN` passes the typed result of the previous stage into a compatible missing input role of the next stage.
- `.` is mandatory and ends a complete statement, including the final statement in a file or block.
- `,` separates role values and introduces a pipeline continuation only when followed by `THEN`.
- Newlines are formatting whitespace and never terminate a statement.
- `;` and `AND THEN` are invalid; use a period and `THEN` respectively.
- Operational blocks use named endings: `TRY, DO ... ON FAILURE ... FINALLY ... END TRY.`.

## Script definitions

Script functions and tasks use typed role contracts and direct sentence invocation. A definition must declare `RETURNING TYPE`, and a non-void definition must contain an explicit `RETURN`:

```text
DEFINE FUNCTION NORMALIZE, WHAT [value] AS TEXT, RETURNING TEXT, DO
    RETURN [value].
END FUNCTION.

NORMALIZE "hello" INTO [normalized].
```

Functions execute in isolated nested state and can participate in pipelines through their `WHAT` role. Tasks use the same call surface but are intended for effectful orchestration.

Records are immutable schema-backed values:

```text
DEFINE RECORD USER, NAME AS TEXT, AGE AS INTEGER.
MAKE USER WITH "Ada", 42 INTO [user].
```

Collection concurrency is explicit: `FOR EACH [item] IN [items], PARALLEL 4, DO ... END FOR.`. Iterations do not share local binding scopes.

Variadic values can be written naturally:

```text
GET TEXT FROM {a.txt}, {b.txt}, {c.txt} INTO [allLines].
```

## Conditions

`IF`, `CHECK IF`, and `FILTER ... WHERE` share the same typed expression grammar:

```text
IF [response.status] IS 200 THEN SAY "ok" ELSE SAY "failed"
CHECK IF [status] IS 200 AND [active] IS true INTO [ok].
FILTER [users] WHERE Active IS true AND Age >= 18 INTO [active].
```

The expression grammar includes `NOT`, `AND`, `OR`, `IS`, `IS NOT`, `=`, `==`, `!=`, `>`, `<`, `>=`, and `<=`. Named typed predicates currently include `EXISTS`, `OK`, and `VALID`; predicates are extensible through `PredicateRegistry`.

```text
CHECK IF [file] EXISTS INTO [exists].
CHECK IF {config.json} EXISTS INTO [exists].
CHECK IF [operation] IS OK INTO [ok].
CHECK IF [document] IS VALID INTO [valid].
```

`EXISTS` supports file-system values and `IExistenceState`; `OK` supports `bool` and `IOkState`; `VALID` supports `bool` and `IValidState`.

## Surface aliases

A CLR role has one stable semantic name, but a parameter may declare contextual surface aliases with `[RoleAlias]`. The parser preserves the explicit surface marker in the AST, and the binder resolves it against each candidate sentence pattern, so an alias is genuinely pattern-scoped rather than globally synonymous.

Qualifiers remain first-class metadata. Verb overloads are selected by role shape, CLR type compatibility, cardinality, conversion cost and qualifier metadata. `params T[]` means a variadic sentence role; `T[]` without `params` is simply one collection-valued role.

## First standard wave

The default host loads eight standard domains: `text`, `files`, `datetime`, `os`, `process`, `json`, `http`, and `collections`. Collection filtering remains a compiler intrinsic so `FILTER ... WHERE` preserves the element type instead of degrading to `object` or relying on reflection-specific generic hacks.

```text
GET NOW INTO [now].
GET TODAY INTO [today].
PARSE DATE FROM "2026-08-17" INTO [date].
FORMAT [date] USING "yyyy-MM-dd" INTO [text].
TRANSFORM [now] TO UTC INTO [utc].
WAIT UNTIL [deadline].

GET ENV {PATH} INTO [path].
SAVE ENV [value] TO {MY_VARIABLE}.
GET OS INTO [os].
GET USER INTO [user].
GET CWD INTO [cwd].

RUN {dotnet} WITH "--info" INTO [result].
GET STDOUT FROM [result] INTO [stdout].
CHECK IF [result] IS OK INTO [ok].
LIST PROCESSES INTO [processes].
STOP [process].
WAIT FOR [process].
```
