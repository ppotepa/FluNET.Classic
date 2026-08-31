using System.Reflection;
using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.SDK;
using FluNET.Classic.Standard.Files;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Storage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LanguageSurfaceConsistencyTests
{
    [Test]
    public void Canonical_role_catalog_keeps_structural_words_out_of_semantic_roles()
    {
        Assert.That(LanguageRoleNames.Contextual, Does.Contain(LanguageRoleNames.What));
        Assert.That(LanguageRoleNames.Contextual, Does.Contain(LanguageRoleNames.To));
        Assert.That(LanguageRoleNames.Contextual, Does.Contain(LanguageRoleNames.Using));
        Assert.That(LanguageRoleNames.Contextual, Does.Not.Contain("THEN"));
        Assert.That(LanguageRoleNames.Contextual, Does.Not.Contain("INTO"));
        Assert.That(LanguageRoleNames.StructuralOnly, Does.Contain("THEN"));
        Assert.That(LanguageRoleNames.StructuralOnly, Does.Contain("INTO"));
    }

    [Test]
    public void Standard_snapshot_uses_only_canonical_contextual_roles()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();

        RoleSlotDescriptor[] roles = snapshot.Verbs
            .SelectMany(x => x.Implementations)
            .SelectMany(x => x.Patterns)
            .SelectMany(x => x.Roles)
            .ToArray();

        Assert.That(roles, Is.Not.Empty);
        Assert.That(roles.All(role => LanguageRoleNames.IsContextual(role.Name)), Is.True,
            string.Join("; ", roles.Where(role => !LanguageRoleNames.IsContextual(role.Name)).Select(role => role.Name)));
        Assert.That(roles.SelectMany(role => role.AllSurfaceNames).Any(LanguageRoleNames.StructuralOnly.Contains), Is.False);

        ModuleQualityIssue[] errors = new ModuleQualityAnalyzer().Analyze(snapshot)
            .Where(issue => issue.Severity == LanguageDiagnosticSeverity.Error)
            .ToArray();
        Assert.That(errors, Is.Empty, string.Join("; ", errors.Select(x => $"{x.Code}: {x.Message}")));
    }

    [Test]
    public void Transform_surface_separates_target_method_and_result_binding()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();

        VerbDescriptor transform = snapshot.GetVerb("TRANSFORM");
        SentencePattern[] patterns = transform.Implementations.SelectMany(x => x.Patterns).ToArray();

        Assert.That(patterns, Is.Not.Empty);
        Assert.That(patterns.SelectMany(x => x.Roles).SelectMany(x => x.AllSurfaceNames), Does.Not.Contain("INTO"));
        Assert.That(patterns.SelectMany(x => x.Roles).SelectMany(x => x.AllSurfaceNames), Does.Not.Contain("AS"));
        Assert.That(patterns.SelectMany(x => x.Roles)
            .Where(role => role.Name is "TO" or "USING")
            .All(role => role.Direction != RoleDirection.Output), Is.True);
    }

    [Test]
    public void File_and_storage_resource_roles_have_one_canonical_surface()
    {
        AssertCanonicalRole(typeof(ListFiles), typeof(DirectoryInfo), LanguageRoleNames.In);
        AssertCanonicalRole(typeof(DeleteFile), typeof(FileInfo), LanguageRoleNames.At);
        AssertCanonicalRole(typeof(ListFilesScoped), typeof(DirectoryInfo), LanguageRoleNames.In);
        AssertCanonicalRole(typeof(ListDirectories), typeof(DirectoryInfo), LanguageRoleNames.In);
        AssertCanonicalRole(typeof(DeleteDirectory), typeof(DirectoryInfo), LanguageRoleNames.At);
        AssertCanonicalRole(typeof(CreateDirectory), typeof(DirectoryInfo), LanguageRoleNames.At);
        AssertCanonicalRole(typeof(ListStorageObjects), typeof(StorageContainer), LanguageRoleNames.In);
        AssertCanonicalRole(typeof(DeleteStorageObject), typeof(StorageKey), LanguageRoleNames.At);
    }

    [Test]
    public void Http_network_resource_roles_use_HttpEndpoint_instead_of_raw_Uri()
    {
        Assert.That(RoleParameter<GetJsonHttp, FromAttribute>().ParameterType, Is.EqualTo(typeof(HttpEndpoint)));
        Assert.That(RoleParameter<DownloadFile, FromAttribute>().ParameterType, Is.EqualTo(typeof(HttpEndpoint)));
        Assert.That(RoleParameter<PostJson, ToAttribute>().ParameterType, Is.EqualTo(typeof(HttpEndpoint)));
    }

    [TestCaseSource(nameof(CanonicalRoundTripSources))]
    public void Formatter_is_idempotent_for_every_structural_language_construct(string source)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string canonical = engine.Format(source);
        string second = engine.Format(canonical);

        Assert.That(second, Is.EqualTo(canonical));
        Assert.That(engine.Parse(canonical).Success, Is.True);
    }

    [Test]
    public async Task Documented_language_surface_example_executes_on_the_production_engine()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "docs", "examples", "language-surface.flu");
        Assert.That(File.Exists(path), Is.True, $"Missing copied documentation example: {path}");
        string source = await File.ReadAllTextAsync(path);

        var writer = new CaptureWriter();
        using ServiceProvider host = FluNetHost.Create(configure: services => services.AddSingleton<IOutputWriter>(writer));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        RuntimeResult result = await engine.RunAsync(source);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        Assert.That(result.State.TryGetVariable("normalized", out object? normalized), Is.True);
        Assert.That(normalized, Is.EqualTo("HELLO"));
        Assert.That(result.State.TryGetVariable("status", out object? status), Is.True);
        Assert.That(status, Is.TypeOf<FluRecord>());
        Assert.That(writer.Lines, Does.Contain("surface-ok"));
        Assert.That(writer.Lines, Does.Contain("cleanup"));
    }

    private static IEnumerable<string> CanonicalRoundTripSources => new[]
    {
        "SAY \"hello\" INTO [value].",
        "TRANSFORM \"hello\" USING UPPER INTO [upper], THEN TRANSFORM TO BINARY USING UTF8 INTO [bytes].",
        "CHECK IF true AND NOT false INTO [ok].",
        "FILTER [users] WHERE Active IS true AND Age >= 18 INTO [active].",
        "IF true, THEN\nSAY \"yes\".\nELSE\nSAY \"no\".\nEND IF.",
        "FOR EACH [item] IN [items], PARALLEL 2, DO\nSAY [item].\nEND FOR.",
        "TRY, DO\nSAY \"work\".\nON FAILURE\nSAY \"failed\".\nFINALLY\nSAY \"done\".\nEND TRY.",
        "DEFINE FUNCTION NORMALIZE, WHAT [value] AS TEXT, RETURNING TEXT, DO\nRETURN [value].\nEND FUNCTION.",
        "DEFINE TASK REPORT, WHAT [value] AS TEXT, RETURNING TEXT, DO\nRETURN [value].\nEND TASK.",
        "DEFINE RECORD USER, NAME AS TEXT, AGE AS INTEGER.\nMAKE USER WITH \"Ada\", 42 INTO [user].",
        "SORT [items] BY NAME USING ASCENDING INTO [sorted].\nGROUP [items] BY NAME INTO [groups].\nTAKE 2 FROM [items] INTO [first].\nSKIP 1 FROM [items] INTO [rest].\nDISTINCT [items] USING DEFAULT INTO [unique].\nCOUNT [items] INTO [count]."
    };

    private static void AssertCanonicalRole(Type implementation, Type parameterType, string expectedRole)
    {
        ParameterInfo parameter = implementation.GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(constructor => constructor.GetParameters())
            .Single(candidate => candidate.ParameterType == parameterType && candidate.GetCustomAttributes().OfType<RoleAttribute>().Any());
        RoleAttribute role = parameter.GetCustomAttributes().OfType<RoleAttribute>().Single();
        Assert.That(role.Name, Is.EqualTo(expectedRole));
        Assert.That(parameter.GetCustomAttributes<RoleAliasAttribute>(), Is.Empty);
    }

    private static ParameterInfo RoleParameter<TImplementation, TRole>() where TRole : RoleAttribute
    {
        return typeof(TImplementation).GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(constructor => constructor.GetParameters())
            .Single(parameter => parameter.GetCustomAttribute<TRole>() is not null);
    }

    private sealed class CaptureWriter : IOutputWriter
    {
        public List<string> Lines { get; } = [];
        public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default)
        {
            Lines.Add(text);
            return ValueTask.CompletedTask;
        }
    }
}
