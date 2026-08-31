using FluNET.Classic.Core;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ModuleGraphValidatorTests
{
    [Test]
    public void Validation_is_deterministic_and_returns_a_read_only_snapshot()
    {
        ILanguageModule[] modules = { new MissingDependencyModule(), new CycleB(), new CycleA() };

        IReadOnlyList<LanguageDiagnostic> first = ModuleGraphValidator.Validate(modules);
        IReadOnlyList<LanguageDiagnostic> second = ModuleGraphValidator.Validate(modules.AsEnumerable().Reverse());

        Assert.That(first.Select(diagnostic => diagnostic.Code + diagnostic.Message), Is.EqualTo(second.Select(diagnostic => diagnostic.Code + diagnostic.Message)));
        Assert.That(() => ((IList<LanguageDiagnostic>)first).Clear(), Throws.TypeOf<NotSupportedException>());
    }

    [Test]
    public void Validation_rejects_a_null_module_sequence()
    {
        Assert.That(() => ModuleGraphValidator.Validate(null!), Throws.TypeOf<ArgumentNullException>());
    }

    private sealed class MissingDependencyModule : LanguageModule
    {
        public override string Name => "missing";
        public override IReadOnlyCollection<string> Dependencies => new[] { "not-installed" };
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
