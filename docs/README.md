# Documentation

This directory contains maintained technical documentation for FluNET.Classic. Each document has one primary responsibility so the same contract is not manually described in several places.

## Documentation map

| Document | Owns |
| --- | --- |
| [`LANGUAGE.md`](LANGUAGE.md) | Human-oriented language surface: sentence shape, canonical roles, pipelines, conditions, definitions, predicates, and standard vocabulary. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Compiler/runtime architecture, dependency direction, binding/execution flow, and repository boundaries. |
| [`SDK.md`](SDK.md) | Module/extension authoring, canonical semantic roles, resolvers/converters/predicates, validation, and generated module artifacts. |
| [`TOOLING.md`](TOOLING.md) | Production document services, diagnostics, formatter behavior, and the language-server adapter built on them. |
| [`examples/language-surface.flu`](examples/language-surface.flu) | Executable documentation fixture for the maintained structural language surface. |
| [`../ROADMAP.md`](../ROADMAP.md) | Planned development milestones and completion criteria. |
| [`../CHANGELOG.md`](../CHANGELOG.md) | Completed user-visible changes by package version. |
| [`../README.md`](../README.md) | Concise project entry point, installation, quick start, and links to the documents above. |
| [`../demo/README.md`](../demo/README.md) | Runnable examples and demo-specific instructions. |

## Sources of truth

Documentation explains the product, but executable language behavior remains defined by the production implementation and tests:

- `src/Engine/FluNET.Classic.Core/LanguageRoleNames.cs` owns the canonical contextual role catalog.
- `src/Engine/FluNET.Classic.Core/LanguageSurfaceValidation.cs` owns compiler-level role/structural/transformation invariants.
- `src/Engine/FluNET.Classic.Syntax/ClassicGrammar.cs` owns the structural grammar.
- `LanguageSnapshot` owns the compiled vocabulary, roles, qualifiers, capabilities, predicates/operators/intrinsics, and sentence patterns for a host composition.
- `ClassicFormatter` owns canonical source rendering; round-trip/idempotence tests keep it aligned with production parsing.
- `Directory.Build.props` owns the package version.

If documentation and executable behavior disagree, fix the documentation or the implementation deliberately; do not create a second compatibility description to explain the mismatch.

## Documentation rules

1. Keep the root README short; detailed language and architecture explanations belong here.
2. Put extension/module-authoring material in `SDK.md`; do not recreate a separate extension guide.
3. Keep editor semantics in production tooling and describe them in `TOOLING.md`; protocol adapters must not redefine language behavior.
4. Prefer generated module documentation from `LanguageSnapshot` metadata over manually maintaining duplicated lists of module sentences.
5. Put future work in `ROADMAP.md` and landed user-visible work in `CHANGELOG.md`.
6. Avoid batch/progress documents whose information is already represented by roadmap status, changelog entries, tests, or Git history.
7. Keep examples canonical and executable against the current `main` language surface. Where a hand-written example is important enough to maintain as a contract, prefer an executable fixture under `docs/examples` and test it through the production engine.
