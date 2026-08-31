# Changelog

All notable user-visible changes to FluNET.Classic are recorded here.

The format follows the spirit of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and package versions follow semantic-versioning conventions. FluNET.Classic is pre-1.0, so breaking improvements may still occur while the public language/runtime/SDK contract is being refined.

## [Unreleased]

### Added

- Zero-role `IQuery<TResult>` verbs for context-backed queries such as `GET PRINCIPAL`.
- `REQUIRE` assertions with stable `FLU-RUN-040` runtime diagnostics.
- Stable `FLU-PROC-002` diagnostics for native-process start failures.
- Stable `FLU-PROC-003` diagnostics for native-process timeouts.
- Stable `FLU-RUN-021` diagnostics for stage execution timeouts, distinct from caller cancellation.
- Host execution policies now reject invalid retry, timeout, and concurrency settings at composition time.
- Network-backed HTTP, DNS, SQL, storage, and email operations now participate in the long-running timeout policy.
- Changing the process working directory now requires the dedicated `os.system.write` capability.
- Parallel execution traces and observer callbacks now remain thread-safe and use unique sequence numbers.
- Parallel loop failures now cancel sibling iterations while preserving the original failure diagnostic.
- Concurrent runs on a shared executor now keep trace and observer state isolated per run.
- `BoundExecutor` now releases its observer synchronization resource with the host lifecycle.
- Concurrent checks on one engine now serialize access to the stateful semantic binder.
- Empty environment variable names are rejected during binding.
- `GET ENV` no longer requires an unused output-side `WHAT` constructor parameter.
- Base `GET ... FROM ...` verbs no longer force unused output-side `WHAT` parameters.
- `LOAD`, `DOWNLOAD`, and JSON list operations no longer force unused output-side `WHAT` parameters.
- Language snapshots now expose immutable collection implementations and copy nested descriptor inputs for tooling consumers.
- SDK compatibility and module-validation reports now snapshot diagnostic collections defensively.
- Document-tooling analysis and signature-help results now snapshot returned collections defensively.
- Execution-plan results now expose a read-only snapshot of their complete nested result tree.
- Generated module manifests and documentation now use stable ordering for collections and overloads.
- `HttpEndpoint` now accepts only absolute HTTP and HTTPS URIs and rejects invalid schemes during binding.
- Invalid textual HTTP endpoints now expose a stable `FormatException` domain error.
- HTTP header values are normalized to case-insensitive, defensive snapshots.
- HTTP responses expose raw body bytes through the typed `GET BODY` projection.
- Process results expose elapsed execution time through the typed `GET DURATION` projection.
- Process specifications now validate file names/timeouts and snapshot argument/environment collections.
- File metadata now supports typed `GET LENGTH`, `GET EXTENSION`, and `GET READONLY` projections.
- Directory metadata now supports typed count/existence projections.
- OS, current-user, and working-directory values now expose typed field projections.
- JSON properties and items now expose scalar and collection-aware typed field projections.
- JSON validation results now expose typed validation-error projections.
- Explicit qualifier surfaces no longer match unrelated verbs solely because their result types happen to be equal.
- Typed HTTP status and ETag values now reject invalid CLR construction inputs.
- HTTP responses and statuses now expose typed `CODE`, `REASON`, and `CONTENTTYPE` projections.
- Non-idempotent HTTP operations no longer opt into automatic retries that could duplicate side effects.
- Previous-pipeline (`THEM`) and current-loop-item (`IT`) bindings.
- Exact native-process argument lists backed by `ProcessStartInfo.ArgumentList`.
- All NuGet packages now include the repository README as package documentation.

### Changed

- Explicit execution traits now override conflicting inferred traits.
- Context-backed standard queries now use `IQuery<TResult>` without dummy `WHAT` parameters.
- Context-backed date/time queries now follow the same zero-input `IQuery<TResult>` contract.
- Zero-input `LIST` operations now use the generic `IContextQuery<TResult>` contract without dummy `WHAT` parameters.
- `READ` is accepted as a GET surface alias and formatter output uses the canonical GET form.
- Native process arguments use the canonical `WITH` role while accepting `ARGUMENTS` as a surface alias.
- The native-process demo now uses the production typed process contract and current result/property names.

## [0.2.0-alpha.2] - 2026-08-31

`0.2.0-alpha.2` is the language-surface-consistency milestone. It turns the sentence conventions established during early `0.2` development into explicit compiler, SDK, tooling, test, and documentation contracts.

### Added

- Canonical `LanguageRoleNames` catalog for `WHAT`, `FROM`, `TO`, `USING`, `WITH`, `AS`, `IN`, `AT`, `FOR`, `UNTIL`, and `BY`.
- Shared `LanguageSurfaceValidation` used by language compilation to reject non-canonical roles, structural role surfaces, and invalid transformation role shapes.
- SDK module-quality checks for canonical roles, structural-word misuse, cross-role aliases, transformation semantics, execution traits, capabilities, and streaming shape.
- Formatter idempotence checks in the module test harness using the compiled `LanguageSnapshot`.
- Structural language-surface regression coverage across pipelines, conditions, loops, failure handling, definitions, records, and collection intrinsics.
- Executable documentation fixture at `docs/examples/language-surface.flu`, copied into the test output and executed through the production engine.
- Project roadmap, changelog baseline, and documentation index introduced after the `alpha.1` baseline.

### Changed

- `THEN` is structural syntax only; the obsolete CLR `ThenAttribute`/`IThen<T>` semantic-role surface was removed.
- The parser now derives generic script-call roles from the canonical role catalog instead of maintaining a second hard-coded role list.
- Script definitions reject role names outside the canonical contextual vocabulary.
- `CHECK IF` and `FILTER ... WHERE` no longer carry the old parser stop-word accommodation for `AS` result binding; `INTO` is the only result-binding form.
- `TRANSFORM` conventions are enforced: `TO` selects target representation/state, `USING` selects method/strategy, and produced values are bound with language-level `INTO`.
- Standard Files and Storage sentences use one canonical location/container spelling: `LIST ... IN ...` and `DELETE ... AT ...` rather than accepting accidental `FROM` aliases.
- Standard HTTP operations use typed `HttpEndpoint` values instead of raw `Uri` parameters for JSON GET, download, and POST endpoint semantics.
- HTTP JSON GET explicitly declares its network capability after adopting the typed endpoint value.
- Tooling diagnostics now carry explicit `Info`, `Warning`, and `Error` severity; binding warnings/infos are preserved instead of being dropped.
- The language server maps document diagnostic severity to LSP severity rather than guessing severity from the diagnostic source.
- Module example validation now preserves binding severity and checks parse/format idempotence with the production language snapshot.
- Root README, SDK, language, and tooling documentation are aligned to the canonical surface instead of duplicating or describing superseded behavior.

### Removed

- Accidental `FROM` aliases from standard Files/Storage `IN` and `AT` roles.
- Redundant `docs/EXTENDING.md`; its unique module-authoring material is consolidated into `docs/SDK.md`.
- Legacy notion of `THEN` as a CLR role.

### Notes

- `Directory.Build.props` is the package-version source and defines `0.2.0-alpha.2` for this milestone.
- The language-contract identity remains `flunet.classic`; the package-version change does not introduce a parallel grammar/runtime mode.
- GitHub Actions runs during this work did not receive a hosted runner (`runner_id: 0`, no workflow steps executed), so those failed/cancelled run records are infrastructure allocation results rather than test failures.

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

- `Directory.Build.props` was the canonical package-version source for this baseline and defined `0.2.0-alpha.1`.
- `main` is the active development line.
- The project has one language-contract identity, `flunet.classic`; package releases do not select parallel grammar versions.
- Earlier development remains available in Git history rather than being reconstructed into artificial historical changelog entries.
