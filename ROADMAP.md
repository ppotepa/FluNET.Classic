# Roadmap

This roadmap defines the active development direction for FluNET.Classic. It is intentionally version-oriented: planned work belongs here; completed user-visible work belongs in [`CHANGELOG.md`](CHANGELOG.md).

## Baseline

- **Active package line:** `0.2.x`
- **Current package version:** `0.2.0-alpha.2`
- **Baseline date:** 2026-08-31
- **Development branch:** `main`
- **Language contract:** `flunet.classic`
- **Runtime target:** .NET 8

`0.2.0-alpha.1` is the first explicitly documented project baseline. It describes the package state from which the versioned roadmap started; it is not an attempt to reconstruct historical releases that were never published as GitHub Releases.

FluNET.Classic currently has one language contract and one compiler/runtime path. Package versions follow semantic-versioning conventions, but while the project remains pre-1.0, language and SDK improvements may be breaking when that produces a cleaner long-term contract.

## Status legend

- `[x]` landed on `main` and represented in the changelog.
- `[ ]` planned for the milestone.
- Milestone contents may be refined as implementation exposes better abstractions, but milestone intent should remain stable.

## 0.2.0-alpha.1 — documented baseline

- [x] Typed controlled-natural-language compiler pipeline: lexer, parser, immutable AST, semantic binding, bound program, planning, and execution.
- [x] Strict canonical sentence punctuation with `.` terminators and typed `, THEN` pipelines.
- [x] Stable result binding through `INTO` and contextual semantic roles such as `FROM`, `TO`, `USING`, `WITH`, `AS`, `IN`, `AT`, `FOR`, and `UNTIL`.
- [x] Shared typed condition semantics across `IF`, `CHECK IF`, and `FILTER ... WHERE`.
- [x] Script functions/tasks, immutable records, `FOR EACH`, bounded parallel iteration, and structured failure handling.
- [x] Standard module wave covering text, files, date/time, operating system, process, JSON, HTTP, and collections.
- [x] Typed resource/domain values and extensible resolvers, converters, predicates, capabilities, and execution traits.
- [x] CLI commands for run, check, format, plan, explain, and language introspection.
- [x] Module-authoring SDK, validation harness, and generated module artifacts.
- [x] Transport-neutral document tooling plus an LSP host with diagnostics, completion, hover, formatting, semantic tokens, symbols, definition, references, rename, and signature help.
- [x] Repository split into Engine, Modules, Tooling, Hosts, tests, and demo layers.
- [x] Documentation baseline, roadmap, changelog, and non-redundant documentation map.

## 0.2.0-alpha.2 — language surface consistency

Goal: make the sentence surface internally predictable enough that new vocabulary follows conventions instead of creating exceptions.

- [x] Audit verb/qualifier/role combinations for natural and context-appropriate wording.
- [x] Normalize transformation semantics so `TO` means target representation/state, `USING` means method/strategy, and `INTO` remains result binding only.
- [x] Normalize `CHECK IF`, `IF`, and predicate wording around one expression model and one set of boolean semantics.
- [x] Remove accidental aliases, ambiguous role spellings, and parser-level special cases that can be expressed through typed metadata.
- [x] Prefer typed CLR resource/value types wherever raw strings currently hide domain meaning.
- [x] Complete canonical formatter round-trip coverage for every structural language construct.
- [x] Make syntax and binding diagnostics consistent in code, severity, span propagation, and terminology across tooling/LSP boundaries.
- [x] Ensure maintained language documentation examples are executable against the production grammar.

Completed on 2026-08-31. The milestone introduced the canonical `LanguageRoleNames` catalog, compiler-level `LanguageSurfaceValidation`, SDK quality checks, canonical standard Files/Storage role surfaces, typed HTTP endpoints, production-grammar documentation fixtures, structural formatter round-trip coverage, and end-to-end diagnostic severity propagation.

Exit criterion: **met**. Ordinary sentence patterns are expressed through canonical typed role metadata; grammar changes are reserved for genuinely new structural constructs.

## 0.2.0-alpha.3 — standard semantics and runtime completeness

Goal: deepen the standard vocabulary without weakening the language model.

- [x] Complete typed semantics for the existing standard domains before adding unrelated domains.
- [x] Expand useful projections and operations through normal typed sentence overloads instead of dedicated parser syntax.
- [x] Review cross-domain conversions and resolution costs so overload selection remains deterministic and explainable.
- [x] Strengthen resource lifecycle, cancellation, timeout, and failure semantics across effectful operations.
- [x] Harden bounded concurrency and isolated execution-state behavior for loops, tasks, and nested execution.
- [x] Expand capability and execution-trait coverage for operations with external effects.
- [x] Increase semantic tests around edge cases, invalid programs, overload ambiguity, and typed failure behavior.

Exit criterion: the standard modules form a coherent reference implementation of how third-party FluNET modules should model domain semantics.

Completed on 2026-08-31. The standard reference vocabulary uses typed resource
values, normal sentence overloads, stable projections, and executable planning
coverage across files, processes, JSON, HTTP, operating-system values, date/time,
collections, and text.

## 0.2.0-beta.1 — SDK and tooling contract

Goal: make the public authoring and tooling surfaces stable enough for external consumers to build against.

- [x] Review the public SDK surface and remove implementation details that should not become compatibility obligations.
- [x] Stabilize module dependency, discovery, validation, resolver/converter, predicate, and capability extension points.
- [x] Stabilize generated module manifest/documentation schemas for the 0.2 line.
- [x] Keep `ClassicDocumentService` as the single semantic source for editor integrations.
- [x] Harden language-server protocol behavior and keep it a thin adapter over production document services.
- [x] Make CLI introspection output sufficient for diagnosing module and language composition.
- [x] Document supported extension patterns and anti-patterns with executable examples.

Exit criterion: module authors and editor/tooling authors can use documented APIs without depending on internal compiler implementation classes.

## 0.2.0-beta.2 — release hardening

Goal: close correctness, documentation, packaging, and usability gaps before the stable 0.2 release.

- [x] Audit every public example against `check`, `format`, `plan`, and `run` where applicable.
- [x] Resolve known grammar ambiguities and high-impact diagnostic inconsistencies.
- [x] Review architecture boundaries and public package references for accidental coupling.
- [x] Review package metadata, installer behavior, demo projects, and first-run experience.
- [x] Bring README, language, architecture, SDK, tooling, and generated module documentation into agreement with the code.
- [x] Classify remaining breaking changes as either required for 0.2 or explicitly deferred to 0.3.
- [x] Produce release notes from the accumulated `CHANGELOG.md` entries instead of reconstructing history at release time.

Exit criterion: no known blocker remains for using FluNET.Classic as a coherent pre-1.0 language/runtime/SDK release.

### Breaking-change classification

Required before stable 0.2: the single `flunet.classic` contract, canonical role
and transformation semantics, deterministic stable IDs and diagnostics, the
document-service/LSP boundary, and the intentional SDK/package surface. These
are contract fixes and may still require breaking API changes while 0.2 remains
pre-1.0.

Deferred to 0.3: interactive shell sessions, native stdin/redirection, history and
prompt workflows, broader ecosystem domains, and compatibility work for APIs not
listed as part of the 0.2 SDK contract. They must not delay the coherent 0.2
runtime and authoring surface.

## 0.2.0 — stable 0.2 milestone

The 0.2 release is ready when all of the following are true:

- [x] The grammar and canonical formatter agree on every supported construct.
- [x] Binding and overload selection are deterministic and explainable for the supported standard vocabulary.
- [x] Runtime execution, cancellation, failure, and bounded-concurrency semantics are documented and covered by tests.
- [x] Standard modules consistently use typed domain semantics where type information materially improves correctness or tooling.
- [x] The module SDK and document-tooling APIs have an intentional public surface.
- [x] CLI, SDK, tooling, language server, installers, demo, and documentation describe the same product state.
- [ ] The changelog contains the complete user-visible delta from the `0.2.0-alpha.1` baseline.

## 0.3 — after 0.2

0.3 is deliberately not specified feature-by-feature yet. Candidate directions include broader ecosystem modules, richer project/package composition, deeper editor workflows, and additional typed language abstractions. Work should move into this section only after the 0.2 contract is sufficiently stable that it will not distract from closing the current milestone.

## Tracking rules

1. `ROADMAP.md` is the source of truth for planned milestone scope.
2. `CHANGELOG.md` is the source of truth for completed user-visible changes.
3. A roadmap checkbox is completed only after the implementation is on `main`.
4. Every meaningful user-visible addition, behavior change, fix, or removal should be added under `Unreleased` in the changelog when it lands, then moved into the version section when that package version is established.
5. Version numbers come from `Directory.Build.props`; documentation must not invent a second version source.
6. Package/release versions and the single `flunet.classic` language-contract identity must not be conflated.
7. Do not create separate progress documents for individual batches when the information belongs in this roadmap, the changelog, code comments, tests, or Git history.
