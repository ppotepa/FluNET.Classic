using FluNET.Classic.Binding;
using FluNET.Classic.Cache;
using FluNET.Classic.Core;
using FluNET.Classic.Csv;
using FluNET.Classic.Ecosystem;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.SDK;
using FluNET.Classic.Secrets;
using FluNET.Classic.Storage;
using FluNET.Classic.Storage.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class LanguageDepthTests
{
    [Test]
    public async Task Typed_collection_stages_preserve_element_values()
    {
        using ServiceProvider host = FluNetHost.Create(); ClassicEngine engine = host.GetRequiredService<ClassicEngine>(); var state = new RuntimeState();
        state.SetVariable("users", new[] { new User("Charlie", 32, true), new User("Alice", 19, true), new User("Bob", 19, false), new User("Alice", 41, true) });

        RuntimeResult sorted = await engine.RunAsync("SORT [users] BY Age INTO [sorted].", state); Assert.That(sorted.Success, Is.True); Assert.That(((User[])sorted.Result!).Select(x => x.Age), Is.EqualTo(new[] { 19, 19, 32, 41 }));
        RuntimeResult count = await engine.RunAsync("COUNT [users] INTO [count].", state); Assert.That(count.Result, Is.EqualTo(4));
        RuntimeResult take = await engine.RunAsync("TAKE 2 FROM [users] INTO [first].", state); Assert.That(((User[])take.Result!).Length, Is.EqualTo(2));
        RuntimeResult distinct = await engine.RunAsync("DISTINCT [users] BY Name INTO [unique].", state); Assert.That(((User[])distinct.Result!).Select(x => x.Name), Is.EqualTo(new[] { "Charlie", "Alice", "Bob" }));
        RuntimeResult grouped = await engine.RunAsync("GROUP [users] BY Active INTO [groups].", state); Assert.That(((CollectionGroup<bool, User>[])grouped.Result!).Length, Is.EqualTo(2));
    }

    [TestCase("CHECK IF \"abcdef\" CONTAINS \"cde\" INTO [ok].")]
    [TestCase("CHECK IF \"abcdef\" STARTS WITH \"abc\" INTO [ok].")]
    [TestCase("CHECK IF \"abcdef\" ENDS WITH \"def\" INTO [ok].")]
    [TestCase("CHECK IF \"abc123\" MATCHES \"^[a-z]+[0-9]+$\" INTO [ok].")]
    [TestCase("CHECK IF 5 BETWEEN 1 AND 10 INTO [ok].")]
    [TestCase("CHECK IF \"\" IS EMPTY INTO [ok].")]
    public async Task Extended_expression_operators_are_shared_by_check(string script)
    {
        using ServiceProvider host = FluNetHost.Create(); RuntimeResult result = await host.GetRequiredService<ClassicEngine>().RunAsync(script); Assert.That(result.Success, Is.True); Assert.That(result.Result, Is.EqualTo(true));
    }

    [Test]
    public async Task In_and_temporal_comparisons_are_typed()
    {
        using ServiceProvider host = FluNetHost.Create(); ClassicEngine engine = host.GetRequiredService<ClassicEngine>(); var state = new RuntimeState(); state.SetVariable("numbers", new[] { 1m, 2m, 3m }); state.SetVariable("first", DateTimeOffset.Parse("2026-01-01T00:00:00Z")); state.SetVariable("second", DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        Assert.That((await engine.RunAsync("CHECK IF 2 IN [numbers] INTO [ok].", state)).Result, Is.EqualTo(true)); Assert.That((await engine.RunAsync("CHECK IF [first] BEFORE [second] INTO [ok].", state)).Result, Is.EqualTo(true)); Assert.That((await engine.RunAsync("CHECK IF [second] AFTER [first] INTO [ok].", state)).Result, Is.EqualTo(true));
    }

    [Test]
    public void Conversion_registry_finds_bounded_multi_hop_paths()
    {
        var conversions = new ValueConversionRegistry(); conversions.Register(new AToB()); conversions.Register(new BToC()); Assert.That(conversions.TryPlan(typeof(A), typeof(C), out ConversionPlan? plan), Is.True); Assert.That(plan!.Steps.Count, Is.EqualTo(2)); Assert.That(conversions.TryConvert(new A("value"), typeof(C), out ConversionResult? result), Is.True); Assert.That(((C)result!.Value!).Value, Is.EqualTo("value"));
    }

    [Test]
    public void Resolver_priority_is_deterministic()
    {
        var resolvers = new ValueResolverRegistry(); resolvers.Register(new ResolvedResolver("low"), 1); resolvers.Register(new ResolvedResolver("high"), 10); Assert.That(resolvers.TryResolve("x", typeof(Resolved), new ResolutionContext(typeof(Resolved)), out object? value), Is.True); Assert.That(((Resolved)value!).Value, Is.EqualTo("high"));
    }

    [Test]
    public void Full_ecosystem_profile_has_a_closed_module_graph()
    {
        IReadOnlyList<LanguageDiagnostic> diagnostics = ModuleGraphValidator.Validate(EcosystemModules.All()); Assert.That(diagnostics.Any(x => x.Severity == LanguageDiagnosticSeverity.Error), Is.False);
    }

    [Test]
    public async Task File_system_storage_provider_cannot_escape_root()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "flunet-storage-" + Guid.NewGuid().ToString("N")); var root = new DirectoryInfo(rootPath); var provider = new FileSystemStorageProvider(root);
        try { await provider.SaveAsync(new StorageKey("folder/value.bin"), new byte[] { 1, 2, 3 }); Assert.That(await provider.GetAsync(new StorageKey("folder/value.bin")), Is.EqualTo(new byte[] { 1, 2, 3 })); Assert.Throws<UnauthorizedAccessException>(() => provider.GetAsync(new StorageKey("../escape.bin")).AsTask().GetAwaiter().GetResult()); }
        finally { if (root.Exists) root.Delete(true); }
    }

    [Test]
    public async Task Memory_cache_respects_expiration()
    {
        var cache = new MemoryCacheProvider(); var key = new CacheKey("key"); await cache.SetAsync(key, new CacheValue(new byte[] { 1 }), new Expiration(TimeSpan.FromMilliseconds(1))); await Task.Delay(10); Assert.That(await cache.GetAsync(key), Is.Null);
    }

    [Test]
    public void Sensitive_values_are_redacted_by_default()
    {
        var secret = new SecretValue("super-secret"); Assert.That(secret.ToString(), Is.EqualTo("***")); Assert.That(SensitiveValueFormatter.Format(secret), Is.EqualTo("***"));
    }

    [Test]
    public async Task Csv_round_trip_preserves_rows()
    {
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null); CsvDocument document = await new ParseCsv("name,age\nAlice,30\nBob,20").ExecuteAsync(context); string text = await new FormatCsv(document).ExecuteAsync(context); CsvDocument again = await new ParseCsv(text).ExecuteAsync(context); Assert.That(again.Rows.Count, Is.EqualTo(2)); Assert.That(again.Rows[0].Values["name"], Is.EqualTo("Alice"));
    }

    [Test]
    public void Sdk_compatibility_analyzer_is_stable_for_identical_snapshots()
    {
        using ServiceProvider host = FluNetHost.Create(); LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>(); LanguageCompatibilityReport report = new LanguageCompatibilityAnalyzer().Compare(snapshot, snapshot); Assert.That(report.IsCompatible, Is.True); Assert.That(report.Changes, Is.Empty);
    }

    private sealed record User(string Name, int Age, bool Active);
    private sealed record A(string Value); private sealed record B(string Value); private sealed record C(string Value); private sealed record Resolved(string Value);
    private sealed class AToB : ValueConverter<A, B> { public override bool TryConvert(A value, out B? result) { result = new B(value.Value); return true; } }
    private sealed class BToC : ValueConverter<B, C> { public override bool TryConvert(B value, out C? result) { result = new C(value.Value); return true; } }
    private sealed class ResolvedResolver(string result) : IValueResolver<Resolved>
    {
        public Type TargetType => typeof(Resolved); public bool TryResolve(string source, ResolutionContext context, out Resolved? value) { value = new Resolved(result); return true; }
        bool IValueResolver.TryResolve(string source, ResolutionContext context, out object? value) { bool ok = TryResolve(source, context, out Resolved? typed); value = typed; return ok; }
    }
}
