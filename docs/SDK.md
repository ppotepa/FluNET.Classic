# FluNET.Classic SDK and module authoring

`FluNET.Classic.SDK` is the public authoring surface for FluNET module developers. Extensions should remain ordinary typed .NET code: CLR types describe data shape, semantic roles describe sentence meaning, constructor parameters describe sentence binding, and the language compiler turns that metadata into an immutable `LanguageSnapshot`.

The default design rule is simple: extend the typed language model before extending parser syntax.

## A typed sentence extension

```csharp
[Verb("GET")]
[Qualifier("TEXT")]
public sealed class GetMySource : Get<string, MySource>
{
    public GetMySource([What] string what, [From] MySource from)
        : base(what, from)
    {
    }

    protected override ValueTask<string> ActAsync(
        MySource from,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(from.Read());
}
```

Use the canonical role attributes `[What]`, `[From]`, `[To]`, `[Using]`, `[With]`, `[As]`, `[In]`, `[At]`, `[For]`, `[Until]`, and `[By]`. They map to the shared `LanguageRoleNames` catalog used by the compiler and parser.

`INTO` and `THEN` are not CLR roles. `INTO` belongs to language-level result binding and `THEN` belongs to structural pipeline/control-flow syntax.

## Contextual surface aliases

A role can expose a contextual surface alias without changing its semantic role when the alternate wording is genuinely natural for that specific pattern:

```csharp
public sealed record RemoteEndpoint(string Value);

public sealed class ReadRemote
{
    public ReadRemote([From, RoleAlias("AT")] RemoteEndpoint endpoint)
    {
        // FROM is the stable semantic role; AT is a deliberate pattern-scoped surface.
    }
}
```

The parser preserves the source spelling and the binder resolves it against the candidate sentence pattern. Aliases are therefore pattern-scoped rather than global synonyms.

Do not add aliases merely to make every contextual word interchangeable. In particular, avoid adding `FROM` to an `IN` container or `AT` location just to accept more spellings. The standard Files/Storage modules intentionally use `LIST ... IN ...` and `DELETE ... AT ...` as their canonical surfaces.

Structural-only words such as `INTO`, `THEN`, `ELSE`, `IF`, `WHERE`, `DO`, and `END` must never be reused as role names or aliases. `LanguageSurfaceValidation` enforces these constraints during language compilation.

## Transformation semantics

Keep target, method, and result separate:

```text
TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes].
```

- `TO` describes the target representation or state.
- `USING` describes the method, strategy, algorithm, or encoding.
- `INTO` names the produced result and is not part of the CLR sentence constructor.
- `AS` is not a `TRANSFORM` role.

Use the existing transformation abstractions (`Transform<...>`, `TransformTo<...>`, `TransformToUsing<...>`, or their current equivalents) rather than inventing parser-specific syntax for a domain conversion. The compiler and SDK reject transformation patterns that use `AS` or model `TO`/`USING` as output slots.

## Prefer typed domain values

Use semantic CLR types when a value carries domain meaning. `FilePattern`, `FileMetadata`, `HttpEndpoint`, HTTP response/status/header types, process descriptors, storage keys/containers, and similar values let overload resolution, diagnostics, planning, and tooling reason about the program more precisely than raw strings can.

A useful extension usually consists of some combination of:

- a typed value/resource,
- an existing verb family,
- one or more sentence patterns,
- an `IValueResolver<T>` when source text must resolve to a domain value,
- an `IValueConverter<TSource, TTarget>` when CLR values can convert between typed representations,
- an `IValuePredicate`/predicate registration for reusable boolean semantics,
- capability metadata for externally effectful operations.

New global grammar syntax should be exceptional and reserved for genuinely structural language features.

## Modules and discovery

Create an `ILanguageModule`/`LanguageModule` and expose the assembly. Modules can declare dependencies, qualifiers, sentence providers, predicates/operators/intrinsics, and other language metadata required by the host.

`ModuleDiscovery.Discover(...)` can discover eligible module types from assemblies loaded from packages or host plugins. The module boundary should remain independent from CLI, editor, or host-specific behavior.

## Module validation

Use `FluNetModuleTestHarness` to validate a module against the same compiler pipeline used by production hosts:

```csharp
var result = FluNetModuleTestHarness.Validate(new MyModule(), options =>
{
    options.Dependencies.Add(new RequiredModule());
    options.Examples.Add("GET THING FROM {source} INTO [thing].");
    options.ConfigureResolvers = resolvers =>
        resolvers.Register(new MyResolver());
});

Assert.True(result.Success);
```

The harness validates, as applicable:

- module dependency composition,
- language compilation and canonical role-surface validation,
- stable metadata identifiers,
- pattern-scoped surface-role collisions,
- module-quality rules with explicit `Info` / `Warning` / `Error` severity,
- example parsing and semantic binding,
- binding diagnostic severity,
- canonical formatter parse/format round trips and formatter idempotence,
- custom resolver/converter/predicate composition used by examples.

Module examples should be executable language contracts rather than illustrative pseudo-syntax.

`ModuleQualityAnalyzer` complements hard compiler validation. Compiler-invalid role/structural/transform surfaces fail language compilation; quality analysis can additionally report intentional cross-role aliases and execution/capability/streaming design concerns without turning every observation into an error.

## Generated artifacts

Generate machine-readable and human-readable artifacts from the compiled `LanguageSnapshot` instead of maintaining a second manual definition:

```csharp
var generator = new ModuleArtifactGenerator();
ModuleArtifacts artifacts = generator.Generate(result.Snapshot!, module);

File.WriteAllText("module.json", artifacts.ManifestJson);
File.WriteAllText("MODULE.md", artifacts.DocumentationMarkdown);
```

Generated metadata can expose sentence implementations, qualifiers, semantic roles, accepted contextual surface names, capabilities, execution traits, result types, and other stable module information represented by the snapshot.

## Capabilities and host policy

Operations with external effects should declare the capabilities they require. Hosts enforce those requirements through `ICapabilityPolicy`; module code should not bypass the policy by reaching around the runtime composition boundary.

The CLI can run with deny-by-default capability selection and explicit `--allow` values, and project manifests can declare capabilities required by a project.

## SDK compatibility rule

`FluNET.Classic.SDK` is the intended module-authoring boundary. Do not require module authors to depend on CLI, language-server, or host implementation projects. During the pre-1.0 `0.2.x` line the SDK may still be refined, but each change should move the public surface toward a smaller and more intentional long-term contract.

When adding a new extension point, prefer one that can be validated, introspected, planned, formatted, and documented from the same metadata the runtime consumes. If an ordinary new sentence needs a parser branch rather than canonical role metadata, first treat that as a design smell; grammar changes should represent genuinely new structural constructs.
