using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ExpressionRoleTests
{
    [Test]
    public async Task Sentence_role_can_consume_a_full_boolean_expression()
    {
        var writer = new CaptureWriter();
        using ServiceProvider host = FluNetHost.Create(configure: services => services.AddSingleton<IOutputWriter>(writer));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("left", 7);
        state.SetVariable("right", 7);

        RuntimeResult result = await engine.RunAsync("SAY [left] IS [right].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(writer.Lines, Is.EqualTo(new[] { "true" }));
    }

    [Test]
    public void Parser_keeps_role_expression_as_expression_ast()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ParseResult parse = engine.Parse("SAY [left] IS [right].");

        Assert.That(parse.Success, Is.True, string.Join("; ", parse.Diagnostics.Select(x => x.Message)));
        var sentence = (SentenceNode)((PipelineNode)parse.Script.Statements.Single()).Stages.Single();
        Assert.That(sentence.Clauses.Single().Values.Single(), Is.TypeOf<BinaryExpression>());
    }

    [Test]
    public async Task Nested_item_selector_uses_dot_path_without_consuming_sentence_period()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("people", new[]
        {
            new Person("B", new Address("Zurich")),
            new Person("A", new Address("Amsterdam"))
        });

        RuntimeResult result = await engine.RunAsync("SORT [people] BY Address.City INTO [sorted].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("sorted", out object? sorted), Is.True);
        Assert.That(((Person[])sorted!).Select(x => x.Address.City), Is.EqualTo(new[] { "Amsterdam", "Zurich" }));
    }

    [Test]
    public void Nested_selector_typo_reports_nearest_property()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var variables = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["people"] = typeof(Person[]) };

        CheckResult result = engine.Check("SORT [people] BY Address.Cty INTO [sorted].", variables);

        BindingDiagnostic diagnostic = result.Bound!.Diagnostics.Single(x => x.Code == "FLU-BIND-150");
        Assert.That(diagnostic.Candidates, Does.Contain("City"));
    }

    private sealed record Address(string City);
    private sealed record Person(string Name, Address Address);

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
