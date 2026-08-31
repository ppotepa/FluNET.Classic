# Changelog

All notable user-visible changes to FluNET.Classic are recorded here.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and package versions follow semantic-versioning conventions. FluNET.Classic is pre-1.0, so breaking improvements may still occur while the public language/runtime/SDK contract is being refined.

## [Unreleased]

### Added

- Project roadmap with versioned milestones, exit criteria, and progress-tracking rules.
- Documentation index defining the purpose and ownership of each maintained document.

### Changed

- Reworked the root README into a concise project entry point instead of duplicating the language, architecture, SDK, and tooling guides.
- Consolidated extension-authoring guidance into `docs/SDK.md`.
- Updated tooling documentation to describe the language-server host that already exists in the repository.
- Clarified that package versions and the single `flunet.classic` language-contract identity are separate concepts.

### Removed

- Redundant `docs/EXTENDING.md`; its unique guidance now lives in `docs/SDK.md`.

## [0.2.0-alpha.1] - 2026-08-31

This entry establishes the first explicit changelog baseline for the existing `0.2.x` development line. It summarizes the state of `main` at the baseline date; it does not claim that `0.2.0-alpha.1` was previously published as a GitHub Release.

### Added

- Typed controlled-natural-language compiler/runtime with lexer, parser, immutable AST, semantic binding, execution planning, and runtime execution.
- Canonical `INTO` result binding, contextual sentence roles, strict statement termination, and typed `, THEN` pipelines.
- Shared typed expression and predicate semantics used by conditions and filtering.
- Script functions/tasks, immutable records, iteration with bounded parallelism, and structured failure handling.
- Standard text, files, date/time, OS, process, JSON, HTTP, and collection semantics.
- Typed resource/value abstractions plus resolver, converter, predicate, capability, and execution-trait extension points.
- CLI for running, checking, formatting, planning, explaining, and introspecting FluNET programs and the compiled language surface.
- `flu.json` project manifests and runnable demo programs.
- Module-authoring SDK with contract validation and generated machine/human-readable artifacts.
- Transport-neutral document services for diagnostics, completion, hover, formatting, planning, semantic tokens, symbols, definition, references, rename, and signature help.
- Language-server host built on the production document service.
- Side-by-side `fluc` installer scripts for Windows, macOS, and Linux development checkouts.
- Layered repository structure separating Engine, Modules, Tooling, Hosts, tests, and demos.

### Notes

- `Directory.Build.props` is the canonical package-version source and currently defines `0.2.0-alpha.1`.
- `main` is the active development line.
- The project has one language-contract identity, `flunet.classic`; package releases do not select parallel grammar versions.
- Earlier development remains available in Git history rather than being reconstructed into artificial historical changelog entries.
