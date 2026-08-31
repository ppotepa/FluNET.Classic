# Architecture

FluNET.Classic has one compiler/runtime architecture and no legacy execution path.

## Repository layers

```text
Engine  -> Core -> Syntax -> Binding -> Runtime
Modules -> Engine, with domain providers and bundles grouped by capability
Tooling -> Engine, exposing SDK and document/editor services
Hosts   -> Engine + Modules + Tooling, composing runnable applications
```

`src/Engine` contains the language itself: contracts and metadata in Core,
lexing/parsing in Syntax, semantic selection in Binding, and execution in
Runtime. `src/Modules` supplies vocabulary without becoming part of the engine.
`src/Tooling` builds developer experiences on the engine. `src/Hosts` is the
composition boundary; hosting may select standard or ecosystem modules and
the CLI/language server may assemble the services they need.

The architecture tests enforce these project-reference rules so a new module
cannot accidentally pull tooling or host concerns into the engine.

```text
CLR interfaces + constructor signatures + nullability + params + attributes
                               |
                         LanguageCompiler
                               |
                        LanguageSnapshot
                               |
source -> ClassicLexer -> ClassicParser -> immutable AST
                               |
                  SemanticBinder + predicates
                               |
                          BoundScript
                         /          \
                ExecutionPlanner   BoundExecutor
                         |              |
                    static plan       runtime
```

CLR types describe value shape. Semantic interfaces describe language families and stable semantic roles. Constructors describe role occurrences, ordering, optionality and variadicity. Attributes refine inferred metadata; `[RoleAlias]` adds pattern-scoped surface spellings without changing the semantic role. Reflection runs while compiling a `LanguageSnapshot`; activators, invokers and property accessors are compiled delegates.

The parser owns sentence punctuation and controlled-natural-language surface syntax. `INTO` is reserved for result binding; explicit role spellings such as `FROM`, `TO`, `USING`, `WITH`, `AS`, `IN`, and `AT` are preserved in the AST and normalized by the binder against each candidate sentence pattern. Periods are mandatory statement terminators, commas separate values or introduce `THEN`, and newlines are whitespace.

`, THEN` forms a typed pipeline: the binder may inject the previous stage result into one compatible missing required input role. A period ends a statement and therefore does not carry a pipeline value. Output-only CLR roles do not require a source-level variable; without `INTO`, their result remains available as the pipeline value.

`ExecutionPlanner` projects a bound script into a non-executing plan containing selected implementations, patterns, typed role values, resolution/conversion information, capabilities and execution traits. Planning is intentionally downstream of binding: it describes what the compiler actually selected instead of re-interpreting source text.

`FILTER ... WHERE`, `REQUIRE`, `IF ... THEN`, `CHECK IF`, and `FOR EACH` are compiler nodes rather than ad-hoc verb rules. `IF`, `CHECK IF`, `REQUIRE`, and `FILTER ... WHERE` share one typed expression tree. Named predicates such as `EXISTS`, `OK`, and `VALID` bind through `PredicateRegistry`, so modules can extend boolean language without adding parser-specific special cases. `THEM` refers to the previous pipeline value and `IT` to the current loop item; both are bound explicitly and are not magic runtime string values.

`FluNET.Classic.SDK` sits above Core/Syntax/Binding/Runtime as the module-authoring surface. It validates compiled module contracts and generates manifests/documentation from `LanguageSnapshot`; it does not define a second language model.
