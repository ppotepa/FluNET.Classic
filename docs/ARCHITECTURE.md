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
                         SemanticBinder
                               |
                          BoundScript
                               |
                         BoundExecutor
```

CLR types describe value shape. Semantic interfaces describe language families/roles. Constructors describe role occurrences, ordering, optionality and variadicity. Attributes refine inferred metadata. Reflection runs while compiling a `LanguageSnapshot`; activators, invokers and property accessors are compiled delegates.

`THEN` is a typed pipeline. `AS` creates result symbols. `FILTER/WHERE`, `IF/ELSE`, and `FOR EACH` are compiler nodes, not ad-hoc verb validation rules.
