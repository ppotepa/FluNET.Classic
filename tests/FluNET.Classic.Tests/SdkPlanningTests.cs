using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.SDK;
using FluNET.Classic.Standard.Files;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Json;
using FluNET.Classic.Standard.OS;
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
    public void Generated_module_manifest_is_deterministic()
    {
        var module = new TextModule();
        ModuleValidationResult validation = FluNetModuleTestHarness.Validate(module);
        var generator = new ModuleArtifactGenerator();

        string first = generator.GenerateManifest(validation.Snapshot!, module);
        string second = generator.GenerateManifest(validation.Snapshot!, module);

        Assert.That(second, Is.EqualTo(first));
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
        ExecutionPlan plan = engine.Plan("GET RESPONSE FROM {https://example.com} INTO [response], THEN GET STATUS FROM [response] INTO [status], THEN GET BODY FROM [response] INTO [body]. CHECK IF [response] IS OK INTO [ok].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        Assert.That(plan.RequiredCapabilities, Does.Contain("network"));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(HttpResponse).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(HttpStatus).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(byte[]).FullName), Is.True);
        Assert.That(steps.Any(x => x.Verb == "CHECK" && x.ResultType == typeof(bool).FullName), Is.True);
    }

    [Test]
    public void File_metadata_properties_bind_through_typed_sentence_projections()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("GET METADATA FROM {readme.md} INTO [metadata], THEN GET LENGTH FROM [metadata] INTO [length], THEN GET EXTENSION FROM [metadata] INTO [extension], THEN GET READONLY FROM [metadata] INTO [readonly].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(FileMetadata).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(long).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(string).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(bool).FullName), Is.True);
    }

    [Test]
    public void Http_status_and_content_type_projections_bind_through_typed_sentences()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("GET RESPONSE FROM {https://example.com} INTO [response], THEN GET STATUS FROM [response] INTO [status], THEN GET CODE FROM [status] INTO [code], THEN GET REASON FROM [status] INTO [reason], THEN GET CONTENTTYPE FROM [response] INTO [content].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(HttpStatus).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(int).FullName), Is.True);
        Assert.That(steps.Any(x => x.Implementation?.EndsWith("GetHttpStatusReason", StringComparison.Ordinal) == true), Is.True);
        Assert.That(steps.Any(x => x.Implementation?.EndsWith("GetHttpContentType", StringComparison.Ordinal) == true), Is.True);
    }

    [Test]
    public void Context_queries_support_typed_field_projections()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("GET OS INTO [os], THEN GET DESCRIPTION FROM [os] INTO [description], THEN GET ARCHITECTURE FROM [os] INTO [architecture]. GET USER INTO [user], THEN GET USERNAME FROM [user] INTO [username]. GET CWD INTO [cwd], THEN GET PATH FROM [cwd] INTO [path].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(OperatingSystemInfo).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(CurrentUserInfo).FullName), Is.True);
        Assert.That(steps.Any(x => x.ResultType == typeof(WorkingDirectory).FullName), Is.True);
        Assert.That(steps.Any(x => x.Implementation?.EndsWith("GetOperatingSystemDescription", StringComparison.Ordinal) == true), Is.True);
        Assert.That(steps.Any(x => x.Implementation?.EndsWith("GetCurrentUserName", StringComparison.Ordinal) == true), Is.True);
        Assert.That(steps.Any(x => x.Implementation?.EndsWith("GetWorkingDirectoryPath", StringComparison.Ordinal) == true), Is.True);
    }

    [Test]
    public void Json_property_and_item_fields_bind_through_typed_projections()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ExecutionPlan plan = engine.Plan("PARSE JSON FROM \"{\\\"name\\\":\\\"Alice\\\"}\" INTO [json], THEN LIST PROPERTIES FROM [json] INTO [properties], THEN GET NAME FROM [properties] INTO [name].");

        Assert.That(plan.Success, Is.True, string.Join("; ", plan.Diagnostics.Select(x => x.Message)));
        Assert.That(Flatten(plan.Steps).Any(x => x.Implementation?.EndsWith("GetJsonPropertyNames", StringComparison.Ordinal) == true), Is.True);
    }

    [Test]
    public void Directory_metadata_properties_bind_through_typed_sentence_projections()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        const string source = "GET METADATA FROM [directory] INTO [metadata], THEN GET FILECOUNT FROM [metadata] INTO [files], THEN GET DIRECTORYCOUNT FROM [metadata] INTO [directories], THEN GET EXISTS FROM [metadata] INTO [exists].";
        var variables = new Dictionary<string, Type> { ["directory"] = typeof(DirectoryInfo) };
        CheckResult check = engine.Check(source, variables);
        ExecutionPlan plan = engine.Plan(source, variables);

        Assert.That(plan.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.SelectMany(x => x.CandidateDetails ?? Array.Empty<CandidateDetail>()).Select(x => x.PatternId) ?? Array.Empty<string>()));
        ExecutionPlanStep[] steps = Flatten(plan.Steps).ToArray();
        Assert.That(steps.Any(x => x.ResultType == typeof(DirectoryMetadata).FullName), Is.True);
        Assert.That(steps.Count(x => x.ResultType == typeof(int).FullName), Is.EqualTo(2));
        Assert.That(steps.Any(x => x.ResultType == typeof(bool).FullName), Is.True);
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
            foreach (ExecutionPlanStep child in Flatten(step.Children))
                yield return child;
        }
    }
}
