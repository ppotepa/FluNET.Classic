using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Standard.Files;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LanguageTests
{
    [Test]
    public void Reflection_compiles_scalar_and_variadic_get_overloads()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
        VerbDescriptor get = language.GetVerb("GET");
        Assert.That(get.Implementations.Any(x => x.ImplementationType == typeof(GetText)), Is.True);
        var many = get.Implementations.Single(x => x.ImplementationType == typeof(GetTextMany));
        Assert.That(many.Patterns.Single().Roles.Single(x => x.Name == "FROM").Cardinality, Is.EqualTo(RoleCardinality.ZeroOrMore));
        Assert.That(many.Patterns.Single().Roles.Single(x => x.Name == "FROM").TypeShape.ElementType, Is.EqualTo(typeof(FileInfo)));
    }

    [Test]
    public void Language_exposes_modules_qualifiers_manifest_and_tooling_metadata()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
        Assert.That(language.Modules.Select(x => x.Name), Is.EquivalentTo(new[] { "files", "text", "json", "http" }));
        Assert.That(language.Qualifiers.Any(x => x.Name == "JSON"), Is.True);
        string manifest = host.GetRequiredService<LanguageIntrospectionService>().ToJson();
        Assert.That(manifest, Does.Contain("filesystem.read"));
        ClassicLanguageService tooling = host.GetRequiredService<ClassicLanguageService>();
        Assert.That(tooling.Complete("GE").Any(x => x.Label == "GET"), Is.True);
        Assert.That(tooling.Hover("GET")?.Detail, Does.Contain("overload"));
    }

    [Test]
    public void Module_graph_reports_cycles()
    {
        IReadOnlyList<LanguageDiagnostic> diagnostics = ModuleGraphValidator.Validate(new ILanguageModule[] { new CycleA(), new CycleB() });
        Assert.That(diagnostics.Any(x => x.Code == "FLU-LANG-031"), Is.True);
    }

    private sealed class CycleA : LanguageModule
    {
        public override string Name => "a";
        public override IReadOnlyCollection<string> Dependencies => new[] { "b" };
    }

    private sealed class CycleB : LanguageModule
    {
        public override string Name => "b";
        public override IReadOnlyCollection<string> Dependencies => new[] { "a" };
    }
}
