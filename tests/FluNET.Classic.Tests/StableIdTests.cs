using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;

namespace FluNET.Classic.Tests;

public class StableIdTests
{
    [Test]
    public void Explicit_stable_ids_define_public_language_identity()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
        VerbImplementationDescriptor implementation = language.GetVerb("SAY").Implementations.Single(x => x.StableId == "verb:say:boolean");
        Assert.That(implementation.ImplementationType.Name, Is.EqualTo("SayBoolean"));
        Assert.That(implementation.Constructors.Single().StableId, Is.EqualTo("ctor:say:boolean"));
        Assert.That(implementation.Patterns.Single().StableId, Is.EqualTo("pattern:say:boolean"));
        Assert.That(implementation.Patterns.Single().Roles.Single().StableId, Is.EqualTo("role:say:boolean:what"));
    }

    [Test]
    public void Duplicate_explicit_stable_ids_are_rejected_by_language_compiler()
    {
        LanguageBuildResult result = new LanguageCompiler().Build(modules: new ILanguageModule[] { new FirstModule(), new SecondModule() });
        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Any(x => x.Code == "FLU-LANG-041" && x.Message.Contains("module:duplicate", StringComparison.Ordinal)), Is.True);
    }

    [Test]
    public void Fallback_constructor_and_pattern_ids_use_deterministic_semantic_hashes()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
        VerbImplementationDescriptor implementation = language.GetVerb("SAY").Implementations.First(x => x.StableId != "verb:say:boolean");
        string constructorHash = implementation.Constructors.First().StableId.Split(':').Last();
        string patternHash = implementation.Patterns.First().StableId.Split(':').Last();
        Assert.That(constructorHash, Has.Length.EqualTo(16));
        Assert.That(patternHash, Has.Length.EqualTo(16));
        Assert.That(constructorHash.All(Uri.IsHexDigit), Is.True);
        Assert.That(patternHash.All(Uri.IsHexDigit), Is.True);
    }

    [StableId("module:duplicate")]
    private sealed class FirstModule : LanguageModule
    {
        public override string Name => "stable-test-a";
        public override IReadOnlyCollection<Assembly> Assemblies => Array.Empty<Assembly>();
    }

    [StableId("module:duplicate")]
    private sealed class SecondModule : LanguageModule
    {
        public override string Name => "stable-test-b";
        public override IReadOnlyCollection<Assembly> Assemblies => Array.Empty<Assembly>();
    }
}
