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

Use semantic roles such as `[What]`, `[From]`, `[To]`, `[Using]`, `[With]`, `[As]`, `[In]`, `[At]`, `[For]`, and `[Until]`. A role may expose contextual surface aliases without changing its stable semantic identity:

```csharp
public ListFiles([In, RoleAlias("FROM")] DirectoryInfo directory) { ... }
```

The parser preserves the accepted surface spelling, and the binder normalizes it against each candidate sentence pattern during overload resolution. `INTO` is reserved by the language for result binding and cannot be declared as a role alias.

For transformations, keep target, method, and result separate. `TransformTo<...>` models `TO`, while `TransformToUsing<...>` models both `TO` and `USING`; the language-level `INTO` binding remains independent of the CLR verb constructor.

Create an `ILanguageModule`/`LanguageModule` and expose the assembly. Modules may declare dependencies and qualifiers. `ModuleDiscovery.Discover(...)` can discover parameterless module types from assemblies loaded from NuGet packages or host plugins.

Custom string-to-CLR resolution is registered through `IValueResolver<T>`. CLR-to-CLR conversion is registered through `IValueConverter<TSource,TTarget>`. Named boolean predicates can be added through `PredicateRegistry`/`IValuePredicate`. Host capabilities are enforced by `ICapabilityPolicy`.
