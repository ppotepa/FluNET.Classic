using FluNET.Language;
using FluNET.Syntax.Core;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Language;

[TestFixture]
public sealed class LanguageCompilerTests
{
    [Test]
    public void GetText_IsCompiledWithoutInstantiatingForMetadata()
    {
        var compiler = new LanguageCompiler();

        VerbImplementationDescriptor descriptor = compiler.CompileVerb(typeof(GetText))!;

        Assert.That(descriptor.Name, Is.EqualTo("GET"));
        Assert.That(descriptor.Aliases, Does.Contain("FETCH"));
        Assert.That(descriptor.Aliases, Does.Contain("RETRIEVE"));
        Assert.That(descriptor.ResultType, Is.EqualTo(typeof(string[])));

        SentencePattern pattern = descriptor.Patterns.Single(p => p.Roles.Count == 2);
        RoleSlotDescriptor what = pattern.Roles.Single(r => r.Name == "WHAT");
        RoleSlotDescriptor from = pattern.Roles.Single(r => r.Name == "FROM");

        Assert.Multiple(() =>
        {
            Assert.That(what.ValueType, Is.EqualTo(typeof(string[])));
            Assert.That(what.Direction, Is.EqualTo(RoleDirection.Output));
            Assert.That(from.ValueType, Is.EqualTo(typeof(FileInfo)));
            Assert.That(from.Direction, Is.EqualTo(RoleDirection.Input));
        });
    }

    [Test]
    public void ParamsCollection_IsVariadicWhileRetainingClrCollectionShape()
    {
        var compiler = new LanguageCompiler();

        VerbImplementationDescriptor descriptor = compiler.CompileVerb(typeof(GetManyFiles))!;
        RoleSlotDescriptor from = descriptor.Patterns.Single().Roles.Single(r => r.Name == "FROM");

        Assert.Multiple(() =>
        {
            Assert.That(from.Cardinality, Is.EqualTo(RoleCardinality.ZeroOrMore));
            Assert.That(from.TypeShape.IsCollection, Is.True);
            Assert.That(from.TypeShape.IsArray, Is.True);
            Assert.That(from.TypeShape.ElementType, Is.EqualTo(typeof(FileInfo)));
            Assert.That(from.ValueType, Is.EqualTo(typeof(FileInfo[])));
        });
    }

    private sealed class GetManyFiles : Get<string, FileInfo[]>
    {
        public GetManyFiles(string what, params FileInfo[] from)
            : base(what, from)
        {
        }

        public override Func<FileInfo[], string> Act => files => string.Join(",", files.Select(f => f.FullName));

        public override bool Validate(IWord word) => true;

        public override FileInfo[]? Resolve(string value) => [new FileInfo(value)];
    }
}
