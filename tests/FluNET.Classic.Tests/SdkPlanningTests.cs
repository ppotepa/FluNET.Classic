using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.SDK;
using FluNET.Classic.Standard.Files;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class SdkPlanningTests
{
    [Test]
    public void Sdk_validates_examples_and_generates_module_artifacts()
    {
        var module = new TextModule();
        ModuleValidationResult validation = FluNetModuleTestHarness.Validate(module, options =>
        {
            options.Examples.Add("SAY \"hello\".");
        });

        Assert.That(validation.Success, Is.True, string.Join("; ", validation.Diagnostics.Select(x => x.Message)));
        var generator = new ModuleArtifactGenerator();
        ModuleArtifacts artifacts = generator.Generate(validation.Snapshot!, module);
        Assert.That(artifacts.ManifestJson, Does.Contain("\"name\": \"text\""));
        Assert.That(artifacts.DocumentationMarkdown, Does.Contain("## SAY"));
    }

    [Test]
    public void Planner_exposes_overload_resolution_capabilities_and_traits_without_execution()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("GET TEXT FROM {input.txt} INTO [lines], THEN TRANSFORM USING UPPER INTO [upper].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        Assert.That(plan.RequiredCapabilities, Does.Contain("filesystem.read"));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.Verb == "GET" && x.Implementation?.EndsWith("GetText", StringComparison.Ordinal) == true), Is.True);
        Assert.That(steps.Any(x => x.Verb == "TRANSFORM" && x.Traits.Contains(FluNET.Classic.Core.ExecutionTrait.Pure)), Is.True);
        Assert.That(steps.SelectMany(x => x.Roles).SelectMany(x => x.Values).Any(x => x.Kind == "resolved"), Is.True);
    }

    [Test]
    public void Http_response_resources_bind_through_the_same_sentence_grammar()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("GET RESPONSE FROM {https://example.com} INTO [response], THEN GET STATUS FROM [response] INTO [status]; CHECK IF [response] IS OK INTO [ok].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        Assert.That(plan.RequiredCapabilities, Does.Contain("network"));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(HttpResponse).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(HttpStatus).FullName), Is.True);
        Assert.That(steps.Any(x => x.Verb == "CHECK" && x.ResultType == typeof(bool).FullName), Is.True);
    }

    [Test]
    public void Tooling_service_reuses_the_compiler_for_analysis_completion_hover_and_planning()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService tooling = host.GetRequiredService<ClassicDocumentService>();

        Assert.That(tooling.Complete("GET TE", "GET TE".Length).Any(x => x.Label == "TEXT"), Is.True);
        Assert.That(tooling.Hover("GET TEXT FROM {input.txt}.", 1)?.Label, Is.EqualTo("GET"));

        DocumentAnalysis analysis = tooling.Analyze("GET TEXT FROM {input.txt} INTO [lines].");
        Assert.That(analysis.Success, Is.True, string.Join("; ", analysis.Diagnostics.Select(x => x.Message)));
        Assert.That(analysis.CanonicalSource, Does.Contain("INTO [lines]."));
        Assert.That(analysis.Plan.RequiredCapabilities, Does.Contain("filesystem.read"));

        DocumentAnalysis invalid = tooling.Analyze("BOGUS.");
        Assert.That(invalid.Success, Is.False);
        Assert.That(invalid.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public async Task File_metadata_is_a_semantic_result_type()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "hello");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET METADATA FROM {{{file}}} INTO [metadata].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("metadata", out object? value), Is.True);
            Assert.That(value, Is.TypeOf<FileMetadata>());
            Assert.That(((FileMetadata)value!).Length, Is.EqualTo(5));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task File_pattern_is_bound_as_a_semantic_input_type()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"flunet-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "a.log"), "a");
        await File.WriteAllTextAsync(Path.Combine(directory, "b.txt"), "b");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"LIST FILES IN {{{directory}}} WITH \"*.log\" INTO [files].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            result.State.TryGetVariable("files", out object? value);
            Assert.That(((FileInfo[])value!).Select(x => x.Name), Is.EqualTo(new[] { "a.log" }));
        }
        finally { Directory.Delete(directory, true); }
    }

    private static IEnumerable<ExecutionPlanStep> Flatten(IEnumerable<ExecutionPlanStep> steps)
    {
        foreach (ExecutionPlanStep step in steps)
        {
            yield return step;
            foreach (ExecutionPlanStep child in Flatten(step.Children)) yield return child;
        }
    }
}
