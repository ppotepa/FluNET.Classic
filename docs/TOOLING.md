# Tooling and language server

`FluNET.Classic.Tooling` is the transport-neutral document layer for editors and developer tools. It reuses the production lexer, parser, binder, formatter, `LanguageSnapshot`, and `ExecutionPlanner`; tooling must not reimplement language semantics.

## ClassicDocumentService

`ClassicDocumentService` provides the semantic editor surface, including:

- syntax and binding diagnostics with source spans,
- canonical formatting,
- non-executing execution plans,
- prefix/context-aware completion based on the current verb and sentence patterns,
- hover information for language elements,
- semantic tokens,
- document symbols,
- definition and reference lookup,
- rename edits,
- signature help.

```csharp
DocumentAnalysis analysis = documents.Analyze(source, knownVariableTypes);
IReadOnlyList<CompletionItem> items = documents.Complete(source, cursorPosition);
HoverInfo? hover = documents.Hover(source, cursorPosition);
ExecutionPlan plan = documents.Plan(source, knownVariableTypes);
```

Tooling behavior should remain downstream of the production compiler. Diagnostics, overload selection, capabilities, predicates, formatting, types, and execution planning must have the same meaning in an editor that they have in `fluc check`, `fluc format`, and `fluc plan`.

## Language-server host

The repository already contains `src/Hosts/FluNET.Classic.LanguageServer`; it is not a future placeholder. The host is a thin JSON-RPC/LSP adapter over `ClassicDocumentService`.

The current server advertises and implements:

- text-document synchronization and diagnostics,
- completion,
- hover,
- document formatting,
- semantic tokens,
- document symbols,
- definition lookup,
- references,
- rename,
- signature help.

Protocol-specific request/response translation belongs in the language-server host. Language reasoning belongs in `FluNET.Classic.Tooling` and the compiler layers below it.

## Architecture rule

Editor integrations should depend on the narrowest production surface that already owns the required behavior:

```text
editor / IDE / protocol client
            |
            v
LanguageServer (protocol adapter, when LSP is used)
            |
            v
ClassicDocumentService
            |
            v
Syntax -> Binding -> LanguageSnapshot -> Planning
```

Do not add parser, binder, formatter, or module-resolution forks specifically for an editor. A feature that needs new semantic information should normally extend the shared document/compiler model first, then expose that information through LSP or another transport.

## Road to 0.2

For the `0.2.x` line, tooling work should focus on correctness and contract quality rather than duplicating IDE-specific implementations: stable diagnostics, predictable completion/signature help, accurate navigation/rename, canonical formatting, semantic-token correctness, and a small transport-neutral API suitable for external integrations.

See [`../ROADMAP.md`](../ROADMAP.md) for milestone status and [`ARCHITECTURE.md`](ARCHITECTURE.md) for dependency boundaries.
