using FluNET.Language;
using FluNET.Syntax.Ast;
using FluNET.Syntax.Parsing;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests.Syntax;

[TestFixture]
public sealed class ClassicParserTests
{
    [Test]
    public void ParsesTypedSentenceAndThenPipeline()
    {
        LanguageSnapshot language = CreateGetLanguage();
        var parser = new ClassicParser(language);

        ClassicParseResult result = parser.Parse(
            "GET TEXT [content] FROM {file.txt}\nTHEN GET [second] FROM other.txt");

        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.Script, Is.Not.Null);
        Assert.That(result.Script!.Pipelines, Has.Count.EqualTo(1));
        Assert.That(result.Script.Pipelines[0].Sentences, Has.Count.EqualTo(2));

        SentenceNode first = result.Script.Pipelines[0].Sentences[0];
        Assert.Multiple(() =>
        {
            Assert.That(first.Verb, Is.EqualTo("GET"));
            Assert.That(first.Qualifier, Is.EqualTo("TEXT"));
            Assert.That(first.Clauses.Single(c => c.RoleName == "WHAT").Values.Single(), Is.TypeOf<VariableExpression>());
            Assert.That(first.Clauses.Single(c => c.RoleName == "FROM").Values.Single(), Is.TypeOf<ReferenceExpression>());
        });
    }

    [Test]
    public void ParsesPropertyAccessAsExpressionTree()
    {
        LanguageSnapshot language = CreateGetLanguage();
        var parser = new ClassicParser(language);

        ClassicParseResult result = parser.Parse("GET [user.name] FROM file.txt");

        ExpressionNode expression = result.Script!.Pipelines[0].Sentences[0]
            .Clauses.Single(c => c.RoleName == "WHAT")
            .Values.Single();

        Assert.That(expression, Is.TypeOf<PropertyExpression>());
        var property = (PropertyExpression)expression;
        Assert.Multiple(() =>
        {
            Assert.That(property.Property, Is.EqualTo("name"));
            Assert.That(property.Target, Is.TypeOf<VariableExpression>());
            Assert.That(((VariableExpression)property.Target).Name, Is.EqualTo("user"));
        });
    }

    private static LanguageSnapshot CreateGetLanguage()
    {
        var compiler = new LanguageCompiler();
        VerbImplementationDescriptor implementation = compiler.CompileVerb(typeof(GetText))!;
        var verb = new VerbDescriptor(
            implementation.Name,
            implementation.Aliases,
            [implementation]);
        return new LanguageSnapshot([verb]);
    }
}
