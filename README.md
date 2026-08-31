# FluNET.Classic

FluNET.Classic is a typed, sentence-oriented scripting language and runtime for .NET. Its controlled natural-language surface is designed to stay readable while binding deterministically to typed CLR semantics.

**Current development package version:** `0.2.0-alpha.2`

FluNET.Classic is pre-1.0 and evolves directly on `main`. The project has one language contract (`flunet.classic`), one compiler/runtime path, and no compatibility-mode or legacy parser branch. Package versions describe releases; they do not select different language contracts.

## Quick start

The repository requires the .NET 8 SDK.

Install the `fluc` tool from the current checkout:

```powershell
# Windows PowerShell
.\install.ps1
```

```bash
# macOS / Linux
./install.sh
```

The installer creates or updates the separate `FluNET.Classic.Cli` global tool and does not replace an existing `flunet` installation.

Run the demo project:

```text
fluc check demo
fluc plan demo
fluc run demo
```

Or work with a single script:

```text
fluc check script.flu
fluc format script.flu
fluc plan script.flu
fluc explain script.flu
fluc run script.flu
```

Useful introspection commands are also available:

```text
fluc verbs
fluc verb GET
fluc qualifiers
fluc modules
fluc language
```

## Language at a glance

```flu
GET TEXT FROM {input.txt} INTO [text],
THEN TRANSFORM TO BINARY USING UTF8 INTO [bytes].

CHECK IF [response] IS OK INTO [ok].

IF [ok] IS true, THEN
    SAY "ok".
ELSE
    SAY "failed".
END IF.
```

`INTO` binds results. Canonical contextual roles are `WHAT`, `FROM`, `TO`, `USING`, `WITH`, `AS`, `IN`, `AT`, `FOR`, `UNTIL`, and `BY`. `, THEN` carries a typed pipeline result forward, and `.` terminates every complete statement. `THEN` is structural syntax, not a CLR sentence role.

The `0.2.0-alpha.2` surface makes these distinctions explicit: `TO` is a target representation/state, `USING` is a method/strategy, `INTO` is result binding only, and cross-role aliases are accepted only when a sentence pattern deliberately exposes them.

The authoritative grammar is implemented by `ClassicGrammar` in `src/Engine/FluNET.Classic.Syntax`; the complete human-oriented language guide is in [`docs/LANGUAGE.md`](docs/LANGUAGE.md).

## Repository structure

```text
src/Engine    language contracts, syntax, binding, and runtime
src/Modules   standard vocabulary, domain modules, providers, and bundles
src/Tooling   module SDK and document/editor services
src/Hosts     hosting, CLI, and language-server composition
tests         behavioral, integration, and architecture tests
demo          runnable FluNET programs
```

The dependency direction is intentionally one-way: Engine is independent of modules and hosts; Modules and Tooling build on Engine; Hosts compose the runnable applications.

## Documentation

- [`docs/README.md`](docs/README.md) — documentation map and ownership rules.
- [`docs/LANGUAGE.md`](docs/LANGUAGE.md) — language surface, roles, pipelines, conditions, definitions, and standard vocabulary.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — compiler/runtime architecture and repository boundaries.
- [`docs/SDK.md`](docs/SDK.md) — module authoring, extension points, validation, and generated artifacts.
- [`docs/TOOLING.md`](docs/TOOLING.md) — document services and the current language-server surface.
- [`docs/examples/language-surface.flu`](docs/examples/language-surface.flu) — executable documentation fixture covering the core structural surface.
- [`ROADMAP.md`](ROADMAP.md) — planned milestones and exit criteria.
- [`CHANGELOG.md`](CHANGELOG.md) — completed user-visible changes by package version.
- [`demo/README.md`](demo/README.md) — runnable examples.

## Development baseline

`Directory.Build.props` is the source of truth for the current package version. `ROADMAP.md` records intended work; `CHANGELOG.md` records work that has actually landed. Planned items should not be copied into the changelog, and completed changes should not remain described only in the roadmap.

The first explicit documentation baseline was established on 2026-08-31 at `0.2.0-alpha.1`. `0.2.0-alpha.2` completes the language-surface-consistency milestone and makes the canonical role/alias/transform/diagnostic conventions executable contracts.

## License

MIT. See [`LICENSE`](LICENSE).
