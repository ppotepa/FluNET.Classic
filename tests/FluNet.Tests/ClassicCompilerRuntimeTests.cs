using FluNET.Binding;
using FluNET.Language;
using FluNET.Runtime;
using FluNET.Syntax.Parsing;
using FluNET.Syntax.Verbs;

namespace FluNET.Tests;

[TestFixture]
public sealed class ClassicCompilerRuntimeTests
{
    [Test]
    public void Compiler_exposes_get_metadata_without_creating_a_verb_instance()
    {
        LanguageSnapshot language = new LanguageCompiler().Compile([typeof(GetText).Assembly]);

        Assert.That(language.TryGetVerb("GET", out VerbDescriptor get), Is.True);
        Assert.That(language.TryGetVerb("FETCH", out VerbDescriptor fetch), Is.True);
        Assert.That(fetch, Is.SameAs(get));

        VerbImplementationDescriptor implementation = get.Implementations
            .Single(x => x.ImplementationType == typeof(GetText));
        SentencePattern pattern = implementation.Patterns
            .Single(x => x.Roles.Any(r => r.Name == "FROM"));

        Assert.That(pattern.Roles.Single(r => r.Name == "WHAT").Direction, Is.EqualTo(RoleDirection.Output));
        Assert.That(pattern.Roles.Single(r => r.Name == "FROM").ValueType, Is.EqualTo(typeof(FileInfo)));
    }

    [Test]
    public void Parser_and_binder_create_a_typed_output_variable()
    {
        LanguageSnapshot language = new LanguageCompiler().Compile([typeof(GetText).Assembly]);
        var parser = new ClassicParser(language);
        ClassicParseResult parsed = parser.Parse("GET [content] FROM {input.txt}");

        Assert.That(parsed.Diagnostics, Is.Empty);

        BoundScript bound = new SemanticBinder(language).Bind(parsed.Script);

        Assert.That(bound.Diagnostics, Is.Empty);
        Assert.That(bound.Variables["content"], Is.EqualTo(typeof(string[])));
        Assert.That(bound.Pipelines.Single().ResultType, Is.EqualTo(typeof(string[])));
    }

    [Test]
    public async Task Runtime_executes_bound_get_and_stores_the_output()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "alpha\nbeta");

        try
        {
            LanguageSnapshot language = new LanguageCompiler().Compile([typeof(GetText).Assembly]);
            var parser = new ClassicParser(language);
            BoundScript bound = new SemanticBinder(language).Bind(
                parser.Parse($"GET [content] FROM {{{path}}}").Script);

            var executor = new BoundExecutor(new VerbActivator());
            RuntimeResult result = await executor.ExecuteAsync(bound);

            Assert.That(result.Success, Is.True);
            Assert.That(result.State.TryGetVariable("content", out object? value), Is.True);
            Assert.That(value, Is.TypeOf<string[]>());
            Assert.That((string[])value!, Does.Contain("alpha"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
