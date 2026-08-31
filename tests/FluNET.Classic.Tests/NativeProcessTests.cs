using FluNET.Classic.Hosting;
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
        Assert.That(JsonSerializer.Deserialize<string[]>(result.Output), Is.EqualTo(new[] { "hello world", "", "-flag", "quote\"value" }));
    }

    [Test]
    public async Task Runner_captures_stdout_stderr_and_exit_code()
    {
        ProcessResult streams = await RunFixtureAsync("streams");
        ProcessResult exit = await RunFixtureAsync("exit", "7");

        Assert.That(streams.Output, Does.Contain("fixture stdout"));
        Assert.That(streams.Error, Does.Contain("fixture stderr"));
        Assert.That(exit.ExitCode, Is.EqualTo(7));
        Assert.That(exit.IsOk, Is.False);
    }

    [Test]
    public async Task Flu_sentence_runs_native_process_and_exposes_typed_result()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        RuntimeResult result = await engine.RunAsync("RUN {dotnet} WITH ARGUMENTS \"--version\" INTO [version].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.TryGetVariable("version", out object? value), Is.True);
        Assert.That(value, Is.TypeOf<ProcessResult>());
        Assert.That(((ProcessResult)value!).Output, Is.Not.Empty);
    }

    [Test]
    public void Formatter_uses_the_explicit_argument_surface()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        Assert.That(engine.Format("RUN {git} WITH \"status\", \"--short\" INTO [status]."), Is.EqualTo("RUN {git} WITH ARGUMENTS \"status\", \"--short\" INTO [status]."));
    }

    [Test]
    public async Task Missing_executable_returns_a_stable_process_diagnostic()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        RuntimeResult result = await engine.RunAsync("RUN {flu-executable-that-does-not-exist}.");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("FLU-PROC-002"));
    }

    [Test]
    public async Task Runner_cancels_the_process_tree()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAsync<TaskCanceledException>(async () => await RunFixtureAsync(cancellation.Token, "sleep", "5000"));
    }

    private static async Task<ProcessResult> RunFixtureAsync(params string[] arguments) => await RunFixtureAsync(CancellationToken.None, arguments);

    private static async Task<ProcessResult> RunFixtureAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "FluNET.Classic.ProcessFixture.dll");
        Assert.That(File.Exists(fixture), Is.True, $"Fixture was not copied to {fixture}.");
        var runner = new SystemProcessRunner(new SystemExecutableResolver());
        return await runner.RunAsync(new ProcessRequest(new Executable("dotnet"), new ProcessArguments(new[] { fixture }.Concat(arguments).ToArray())), cancellationToken);
    }
}
