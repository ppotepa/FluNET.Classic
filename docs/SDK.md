# FluNET.Classic.SDK

`FluNET.Classic.SDK` is the public authoring surface for language-module developers. A module should be ordinary typed .NET code; the SDK validates that the CLR metadata compiles into an unambiguous FluNET language surface and can generate machine-readable and human-readable artifacts from the same `LanguageSnapshot`.

## Module validation

```csharp
var result = FluNetModuleTestHarness.Validate(new MyModule(), options =>
{
    options.Dependencies.Add(new RequiredModule());
    options.Examples.Add("GET THING FROM {source} INTO [thing].");
    options.ConfigureResolvers = resolvers => resolvers.Register(new MyResolver());
});

Assert.True(result.Success);
```

The harness validates the module dependency graph, language compilation, stable IDs, pattern-scoped surface-name collisions, example parsing/binding, and canonical formatter round-trips. Custom resolvers, converters, and predicates can be registered for module examples.

## Generated artifacts

```csharp
var generator = new ModuleArtifactGenerator();
ModuleArtifacts artifacts = generator.Generate(result.Snapshot!, module);
File.WriteAllText("module.json", artifacts.ManifestJson);
File.WriteAllText("MODULE.md", artifacts.DocumentationMarkdown);
```

The manifest exposes stable IDs, CLR implementations, qualifiers, semantic roles, accepted surface role names, capabilities, execution traits, and result types. Generated documentation is intentionally derived from the same metadata instead of being maintained as a second language definition.

## Design rule

Prefer adding a CLR value/resource type, an existing verb family, a sentence pattern, and the required resolver/converter over adding parser syntax. New global syntax should be exceptional; most ecosystem growth belongs in modules.
