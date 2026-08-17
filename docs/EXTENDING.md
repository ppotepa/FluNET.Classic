# Extending FluNET.Classic

An extension is ordinary .NET code. CLR types describe data shape, role interfaces/attributes describe semantics, constructor parameters describe sentence binding, and reflection compiles the result into `LanguageSnapshot` metadata.

```csharp
[Verb("GET")]
[Qualifier("TEXT")]
public sealed class GetMySource : Get<string, MySource>
{
    public GetMySource([What] string what, [From] MySource from) : base(what, from) { }
    protected override ValueTask<string> ActAsync(MySource from, CancellationToken ct) => ValueTask.FromResult(from.Read());
}
```

Create an `ILanguageModule`/`LanguageModule` and expose the assembly. Modules may declare dependencies and qualifiers. `ModuleDiscovery.Discover(...)` can discover parameterless module types from assemblies loaded from NuGet packages or host plugins.

Custom string-to-CLR resolution is registered through `IValueResolver<T>`. CLR-to-CLR conversion is registered through `IValueConverter<TSource,TTarget>`. Host capabilities are enforced by `ICapabilityPolicy`.
