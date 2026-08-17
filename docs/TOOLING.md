# Tooling

`FluNET.Classic.Tooling` is a transport-neutral document layer intended to sit underneath an LSP server, IDE extension, editor integration, or other interactive tooling. It reuses the production lexer, parser, binder, formatter, `LanguageSnapshot`, and `ExecutionPlanner`; tooling must not reimplement language semantics.

`ClassicDocumentService` provides:

- parse and binding diagnostics with source spans,
- canonical formatting,
- non-executing execution plans,
- prefix/context-aware completion based on the current verb and its sentence patterns,
- hover information for verbs, qualifiers, result binding, and contextual role surfaces.

```csharp
DocumentAnalysis analysis = documents.Analyze(source, knownVariableTypes);
IReadOnlyList<CompletionItem> items = documents.Complete(source, cursorPosition);
HoverInfo? hover = documents.Hover(source, cursorPosition);
ExecutionPlan plan = documents.Plan(source, knownVariableTypes);
```

A future LSP package should only translate LSP requests and responses to these APIs. Parser behavior, overload selection, capabilities, predicates, formatting and type reasoning remain in the compiler/runtime layers.
