# Architecture

FluNET.Classic has one compiler/runtime architecture and no legacy execution path.

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
                               |
                         BoundExecutor
```

CLR types describe value shape. Semantic interfaces describe language families and stable semantic roles. Constructors describe role occurrences, ordering, optionality and variadicity. Attributes refine inferred metadata; `[RoleAlias]` adds pattern-scoped surface spellings without changing the semantic role. Reflection runs while compiling a `LanguageSnapshot`; activators, invokers and property accessors are compiled delegates.

The parser owns sentence punctuation and controlled-natural-language surface syntax. `INTO` is reserved for result binding; explicit role spellings such as `FROM`, `TO`, `USING`, `WITH`, `AS`, `IN`, and `AT` are preserved in the AST and normalized by the binder against each candidate sentence pattern. `ClassicFormatter` emits the canonical result-binding spelling and normalizes the legacy `AS [variable]` result form to `INTO [variable]`.

`THEN`/`AND THEN` form a typed pipeline: the binder may inject the previous stage result into one compatible missing required input role. A semicolon starts a separate statement and therefore does not carry a pipeline value. Output-only CLR roles do not require a source-level variable; without `INTO`, their result remains available as the pipeline value.

`FILTER ... WHERE`, `IF ... THEN`, `CHECK IF`, and `FOR EACH` are compiler nodes rather than ad-hoc verb rules. `IF`, `CHECK IF`, and `FILTER ... WHERE` share one typed expression tree. Named predicates such as `EXISTS`, `OK`, and `VALID` bind through `PredicateRegistry`, so modules can extend boolean language without adding parser-specific special cases.
