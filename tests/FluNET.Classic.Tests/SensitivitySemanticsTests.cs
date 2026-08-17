using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Text;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class SensitivitySemanticsTests
{
    [Test]
    public void Planner_marks_sensitive_interpolation_and_sentence_provenance()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan(
            "SAY \"token [secret]\".",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["secret"] = typeof(FakeSecret) });

        ExecutionPlanStep say = plan.Steps.SelectMany(x => x.Children).Single(x => x.Verb == "SAY");
        ExecutionPlanRole what = say.Roles.Single(x => x.Name == "WHAT");
        Assert.That(say.Sensitive, Is.True);
        Assert.That(what.Sensitive, Is.True);
        Assert.That(what.Values.Single().Sensitive, Is.True);
        Assert.That(what.Values.Single().Detail, Does.Not.Contain("SHOULD-NOT-LEAK"));
    }

    [Test]
    public async Task Sensitive_interpolation_is_redacted_before_output()
    {
        var writer = new CaptureOutputWriter();
        using ServiceProvider host = FluNetHost.Create(configure: services => services.AddSingleton<IOutputWriter>(writer));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("secret", new FakeSecret("SHOULD-NOT-LEAK"));

        RuntimeResult result = await engine.RunAsync("SAY \"token [secret]\".", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(writer.Lines, Is.EqualTo(new[] { "token ***" }));
        Assert.That(string.Join("\n", writer.Lines), Does.Not.Contain("SHOULD-NOT-LEAK"));
    }

    [Test]
    public void Sensitive_expression_marks_derived_check_result_in_plan()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan(
            "CHECK IF [secret] IS NOT null INTO [present].",
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) { ["secret"] = typeof(FakeSecret) });

        ExecutionPlanStep check = plan.Steps.SelectMany(x => x.Children).Single(x => x.Verb == "CHECK");
        Assert.That(check.Sensitive, Is.True);
    }

    private sealed class FakeSecret(string value) : ISensitiveValue
    {
        public string RedactedText => "***";
        public override string ToString() => value;
    }

    private sealed class CaptureOutputWriter : IOutputWriter
    {
        public List<string> Lines { get; } = [];
        public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default)
        {
            Lines.Add(text);
            return ValueTask.CompletedTask;
        }
    }
}
