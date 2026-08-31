# Tooling and language server

`FluNET.Classic.Tooling` is the transport-neutral document layer for editors and developer tools. It reuses the production lexer, parser, binder, formatter, `LanguageSnapshot`, and `ExecutionPlanner`; tooling must not reimplement language semantics.

## ClassicDocumentService

`ClassicDocumentService` provides the semantic editor surface, including:

- syntax and binding diagnostics with source spans and explicit severity,
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

Tooling behavior remains downstream of the production compiler. Diagnostics, overload selection, capabilities, predicates, formatting, types, and execution planning must have the same meaning in an editor that they have in `fluc check`, `fluc format`, and `fluc plan`.

## Diagnostics

`DocumentDiagnostic` carries a `LanguageDiagnosticSeverity` (`Info`, `Warning`, or `Error`) in addition to source, code, message, and span.

- Syntax diagnostics are surfaced as errors.

## CLI introspection

The CLI exposes the same compiled snapshot used by document services:

    fluc verbs
    fluc verb GET
    fluc qualifiers
    fluc modules
    fluc language

The text commands include stable identifiers and composition details. The verb
command reports implementation types, qualifiers, capabilities, execution
traits, and role shapes. The qualifiers command marks a shared surface with ?
when target types conflict across modules. Use language for the complete
machine-readable JSON snapshot.
- Binding diagnostics preserve their binder severity; warnings and informational diagnostics are not discarded.
- LSP publishing maps `Error`/`Warning`/`Info` to protocol severity 1/2/3 instead of inferring severity from the diagnostic source string.

This keeps CLI/compiler semantics and editor presentation aligned while still allowing the binder/SDK to introduce non-blocking diagnostics.

## Language-server host

The repository contains `src/Hosts/FluNET.Classic.LanguageServer`; it is a thin JSON-RPC/LSP adapter over `ClassicDocumentService`, not a second language implementation.

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

## Canonical surface rule

Completion, hover, signature help, semantic tokens, and formatting should expose the same canonical surface validated by `LanguageRoleNames` / `LanguageSurfaceValidation` and represented by `LanguageSnapshot`. Tooling must not independently re-introduce aliases or legacy result-binding forms that the compiler no longer accepts.

The canonical formatter is also treated as an executable contract: module validation and language-surface tests parse, format, parse again, and require the second formatting pass to be identical.

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
