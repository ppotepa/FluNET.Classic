using FluNET.Classic.Hosting;
using FluNET.Classic.Core;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Process;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.Json;

namespace FluNET.Classic.Tests;

public sealed class NativeProcessTests
{
    [Test]
    public async Task Runner_preserves_argument_boundaries_and_empty_values()
    {
        ProcessResult result = await RunFixtureAsync("arguments", "hello world", "", "-flag", "quote\"value");

        Assert.That(result.IsOk, Is.True);
        Assert.That(JsonSerializer.Deserialize<string[]>(result.StdOut), Is.EqualTo(new[] { "hello world", "", "-flag", "quote\"value" }));
    }

    [Test]
    public async Task Runner_captures_stdout_stderr_and_exit_code()
    {
        ProcessResult streams = await RunFixtureAsync("streams");
        ProcessResult exit = await RunFixtureAsync("exit", "7");

        Assert.That(streams.StdOut, Does.Contain("fixture stdout"));
        Assert.That(streams.StdErr, Does.Contain("fixture stderr"));
        Assert.That(exit.ExitCode, Is.EqualTo(7));
        Assert.That(exit.IsOk, Is.False);
    }

    [Test]
    public async Task Flu_sentence_runs_native_process_and_exposes_typed_result()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        RuntimeResult result = await engine.RunAsync("CREATE PROCESS FROM \"dotnet\" WITH \"--version\" INTO [spec], THEN RUN [spec] INTO [version].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("version", out object? value), Is.True);
        Assert.That(value, Is.TypeOf<ProcessResult>());
        Assert.That(((ProcessResult)value!).StdOut, Is.Not.Empty);
    }

    [Test]
    public void Formatter_uses_the_explicit_argument_surface()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        Assert.That(engine.Format("RUN {git} WITH \"status\", \"--short\" INTO [status]."), Is.EqualTo("RUN {git} WITH \"status\", \"--short\" INTO [status]."));
    }

    [Test]
    public void Background_process_uses_the_same_argument_surface_as_foreground_processes()
    {
        using ServiceProvider host = FluNetHost.Create();
        CheckResult check = host.GetRequiredService<ClassicEngine>().Check("RUN PROCESS {dotnet} USING BACKGROUND WITH ARGUMENTS \"--version\".");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));
    }

    [Test]
    public async Task Missing_executable_returns_a_stable_process_diagnostic()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        RuntimeResult result = await engine.RunAsync("CREATE PROCESS FROM \"flu-executable-that-does-not-exist\" INTO [spec], THEN RUN [spec].");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FLU-PROC-002"));
    }

    [Test]
    public async Task Runner_cancels_the_process_tree()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAsync<TaskCanceledException>(async () => await RunFixtureAsync(cancellation.Token, "sleep", "5000"));
    }

    [Test]
    public void Runner_reports_a_process_timeout_separately_from_cancellation()
    {
        ExecutionFailureException exception = Assert.ThrowsAsync<ExecutionFailureException>(async () => await RunFixtureAsync(TimeSpan.FromMilliseconds(250), "sleep", "5000"))!;
        Assert.That(exception.Code, Is.EqualTo("FLU-PROC-003"));
    }

    private static async Task<ProcessResult> RunFixtureAsync(params string[] arguments) => await RunFixtureAsync(CancellationToken.None, arguments);

    private static async Task<ProcessResult> RunFixtureAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "FluNET.Classic.ProcessFixture.dll");
        Assert.That(File.Exists(fixture), Is.True, $"Fixture was not copied to {fixture}.");
        var process = new RunProcess(new ProcessSpec("dotnet", string.Join(" ", new[] { fixture }.Concat(arguments).Select(Quote))));
        return await process.ExecuteAsync(new VerbExecutionContext(null, new Dictionary<string, object?>(), null), cancellationToken);
    }

    private static async Task<ProcessResult> RunFixtureAsync(TimeSpan timeout, params string[] arguments)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "FluNET.Classic.ProcessFixture.dll");
        Assert.That(File.Exists(fixture), Is.True, $"Fixture was not copied to {fixture}.");
        var process = new RunProcess(new ProcessSpec("dotnet", string.Join(" ", new[] { fixture }.Concat(arguments).Select(Quote)), Timeout: timeout));
        return await process.ExecuteAsync(new VerbExecutionContext(null, new Dictionary<string, object?>(), null));
    }

    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
}
