using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class SyntaxEvolutionTests
{
    [Test]
    public void Lexer_recognizes_sentence_punctuation_without_breaking_decimal_or_references()
    {
        var lexer = new ClassicLexer();
        IReadOnlyList<SyntaxToken> tokens = lexer.Lex("CHECK IF 3.14 >= 3, THEN GET JSON FROM {https://api.example.com/v1.2}. ");

        Assert.That(tokens.Any(x => x.Kind == TokenKind.Number && Equals(x.Value, 3.14m)), Is.True);
        Assert.That(tokens.Any(x => x.Kind == TokenKind.Comma), Is.True);
        Assert.That(tokens.Any(x => x.Kind == TokenKind.Period), Is.True);
        Assert.That(tokens.Any(x => x.Kind == TokenKind.Reference && Equals(x.Value, "https://api.example.com/v1.2")), Is.True);
    }

    [Test]
    public void Parser_accepts_comma_variadics_multiline_then_and_period()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ParseResult parse = engine.Parse("GET TEXT FROM {a.txt}, {b.txt} INTO [lines],\nTHEN TRANSFORM USING UPPER INTO [upper].");

        Assert.That(parse.Success, Is.True, string.Join("; ", parse.Diagnostics.Select(x => x.Message)));
        PipelineNode pipeline = (PipelineNode)parse.Script.Statements.Single();
        Assert.That(pipeline.Stages, Has.Count.EqualTo(2));
        SentenceNode get = (SentenceNode)pipeline.Stages[0];
        Assert.That(get.Clauses.Single(x => x.RoleName == "FROM").Values, Has.Count.EqualTo(2));
        Assert.That(get.ResultAlias, Is.EqualTo("lines"));
    }

    [Test]
    public async Task Into_and_pipeline_value_execute_without_repeating_subject()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllLinesAsync(file, new[] { "one", "two" });
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{file}}},\nTHEN TRANSFORM USING UPPER INTO [upper].");

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("upper", out object? upper), Is.True);
            Assert.That((string[])upper!, Is.EqualTo(new[] { "ONE", "TWO" }));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void Semicolon_starts_an_independent_statement_instead_of_piping()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        CheckResult check = engine.Check("GET TEXT FROM {a.txt} INTO [lines]; TRANSFORM USING UPPER INTO [upper].");

        Assert.That(check.Success, Is.False);
        Assert.That(check.Bound?.Diagnostics.Any(x => x.Message.Contains("TRANSFORM", StringComparison.OrdinalIgnoreCase)), Is.True);
    }

    [Test]
    public async Task Legacy_as_result_binding_remains_compatible()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "hello");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{file}}} AS [lines]");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("lines", out _), Is.True);
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task As_is_a_representation_role_when_followed_by_a_value()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllTextAsync(file, "hello");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET {{{file}}} AS TEXT INTO [lines].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("lines", out object? lines), Is.True);
            Assert.That((string[])lines!, Is.EqualTo(new[] { "hello" }));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task Transform_distinguishes_to_using_and_into()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("text", "hello");

        RuntimeResult result = await engine.RunAsync("TRANSFORM [text] TO BINARY USING UTF8 INTO [bytes], THEN TRANSFORM TO TEXT USING UTF8 INTO [roundtrip].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("bytes", out object? bytes), Is.True);
        Assert.That(bytes, Is.TypeOf<byte[]>());
        Assert.That(result.State.TryGetVariable("roundtrip", out object? roundtrip), Is.True);
        Assert.That(roundtrip, Is.EqualTo("hello"));
    }

    [Test]
    public async Task Parse_format_and_transform_json_use_natural_representation_roles()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("text", "{\"name\":\"Ada\"}");

        RuntimeResult result = await engine.RunAsync("PARSE [text] AS JSON INTO [data], THEN FORMAT AS JSON INTO [json].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("json", out object? json), Is.True);
        Assert.That(json?.ToString(), Does.Contain("Ada"));
    }

    [Test]
    public async Task Check_if_reuses_boolean_expression_grammar_and_binds_result()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("status", 200);
        state.SetVariable("active", true);

        RuntimeResult result = await engine.RunAsync("CHECK IF [status] IS 200 AND [active] IS true INTO [ok].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("ok", out object? ok), Is.True);
        Assert.That(ok, Is.EqualTo(true));
    }

    [Test]
    public async Task Check_if_supports_typed_exists_predicate()
    {
        string file = Path.GetTempFileName();
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            var state = new RuntimeState();
            state.SetVariable("file", new FileInfo(file));

            RuntimeResult result = await engine.RunAsync("CHECK IF [file] EXISTS INTO [exists].", state);

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("exists", out object? exists), Is.True);
            Assert.That(exists, Is.EqualTo(true));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task Exists_can_infer_a_file_reference_without_an_intermediate_variable()
    {
        string file = Path.GetTempFileName();
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"CHECK IF {{{file}}} EXISTS INTO [exists].");

            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("exists", out object? exists), Is.True);
            Assert.That(exists, Is.EqualTo(true));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task In_and_at_are_first_class_contextual_roles()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, "a.txt");
        await File.WriteAllTextAsync(file, "a");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult list = await engine.RunAsync($"LIST FILES IN {{{directory}}} INTO [files].");
            Assert.That(list.Success, Is.True, string.Join("; ", list.Diagnostics.Select(x => x.Message)));
            Assert.That(list.State.TryGetVariable("files", out object? files), Is.True);
            Assert.That(((FileInfo[])files!).Select(x => x.Name), Does.Contain("a.txt"));

            RuntimeResult delete = await engine.RunAsync($"DELETE AT {{{file}}} INTO [deleted].");
            Assert.That(delete.Success, Is.True, string.Join("; ", delete.Diagnostics.Select(x => x.Message)));
            Assert.That(File.Exists(file), Is.False);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void And_then_is_a_surface_alias_for_pipeline_continuation()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        ParseResult parse = engine.Parse("GET TEXT FROM {a.txt}, AND THEN TRANSFORM USING UPPER INTO [upper].");

        Assert.That(parse.Success, Is.True, string.Join("; ", parse.Diagnostics.Select(x => x.Message)));
        Assert.That(((PipelineNode)parse.Script.Statements.Single()).Stages, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Named_ok_and_valid_predicates_are_typed_and_extensible()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("operation", new PredicateState(true, true));

        RuntimeResult result = await engine.RunAsync("CHECK IF [operation] IS OK AND [operation] IS VALID INTO [accepted].", state);

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("accepted", out object? accepted), Is.True);
        Assert.That(accepted, Is.EqualTo(true));
    }

    [Test]
    public void Http_at_is_a_pattern_scoped_surface_alias_for_from()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("GET JSON AT {https://api.example.com/users} INTO [users].");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));
    }

    [Test]
    public async Task Date_time_domain_supports_parse_format_and_pipeline_implicit_subject()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("PARSE DATE FROM \"2026-08-17\" INTO [date], THEN FORMAT USING \"yyyy-MM-dd\" INTO [text].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("text", out object? text), Is.True);
        Assert.That(text, Is.EqualTo("2026-08-17"));
    }

    [Test]
    public async Task Os_domain_exposes_readable_system_sentences()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("GET OS INTO [os]; GET CWD INTO [cwd].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("os", out _), Is.True);
        Assert.That(result.State.TryGetVariable("cwd", out _), Is.True);
    }

    [Test]
    public void Process_list_uses_an_omitted_output_role()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("LIST PROCESSES INTO [processes].");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));
    }

    [Test]
    public void Process_domain_binds_run_result_and_typed_ok_predicate()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("RUN {dotnet} WITH \"--version\" INTO [result], THEN CHECK IF [result] IS OK INTO [ok].");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));
    }

    [Test]
    public void Formatter_normalizes_legacy_result_alias_and_pipeline_style()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        string formatted = engine.Format("GET TEXT FROM {input.txt} AS [lines] THEN TRANSFORM [lines] USING UPPER AS [upper]");

        Assert.That(formatted, Does.Contain("GET TEXT FROM {input.txt} INTO [lines]"));
        Assert.That(formatted, Does.Contain("THEN TRANSFORM [lines] USING UPPER INTO [upper]."));
    }

    private sealed record PredicateState(bool IsOk, bool IsValid) : IOkState, IValidState;
}
