using FluNET.Classic.Core;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class QualifierMergeTests
{
    [Test]
    public void Conflicting_qualifier_types_are_not_presented_as_one_false_type()
    {
        LanguageBuildResult result = new LanguageCompiler().Build(modules: new ILanguageModule[] { new StringQualifierModule(), new IntegerQualifierModule() });

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.That(result.Snapshot!.TryGetQualifier("SHARED", out QualifierDescriptor descriptor), Is.True);
        Assert.That(descriptor.TargetType, Is.Null);
        Assert.That(descriptor.AllAliases, Is.EqualTo(new[] { "ALPHA", "BETA" }));
    }

    private sealed class StringQualifierModule : LanguageModule
    {
        public override string Name => "qualifier-string";
        public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:string", "SHARED", typeof(string), new[] { "BETA" }) };
    }

    private sealed class IntegerQualifierModule : LanguageModule
    {
        public override string Name => "qualifier-int";
        public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:integer", "SHARED", typeof(int), new[] { "ALPHA" }) };
    }
}
