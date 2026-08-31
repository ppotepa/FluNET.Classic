using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using FluNET.Classic.Standard.Text;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class RuntimeTests
{
    [Test]
    public async Task Get_text_binds_and_executes_without_an_implicit_legacy_model()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllLinesAsync(file, new[] { "one", "two" });
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{file}}} INTO [lines].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            Assert.That(result.State.TryGetVariable("lines", out object? value), Is.True);
            Assert.That((string[])value!, Is.EqualTo(new[] { "one", "two" }));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task Typed_then_selects_transform_lines_overload()
    {
        string file = Path.GetTempFileName();
        await File.WriteAllLinesAsync(file, new[] { "one", "two" });
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{file}}} INTO [lines], THEN TRANSFORM [lines] USING UPPER INTO [upper].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            result.State.TryGetVariable("upper", out object? upper);
            Assert.That((string[])upper!, Is.EqualTo(new[] { "ONE", "TWO" }));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task Multiple_from_values_select_variadic_overload()
    {
        string a = Path.GetTempFileName();
        string b = Path.GetTempFileName();
        await File.WriteAllTextAsync(a, "a");
        await File.WriteAllTextAsync(b, "b");
        try
        {
            using ServiceProvider host = FluNetHost.Create();
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{a}}}, {{{b}}} INTO [lines].");
            Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
            result.State.TryGetVariable("lines", out object? lines);
            Assert.That(((string[])lines!).Length, Is.EqualTo(2));
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Test]
    public async Task Filter_where_is_typed_and_returns_filtered_array()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("users", new[] { new Person("A", true, 20), new Person("B", false, 30), new Person("C", true, 17) });
        RuntimeResult result = await engine.RunAsync("FILTER [users] WHERE Active IS true AND Age >= 18 INTO [active].", state);
        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        result.State.TryGetVariable("active", out object? active);
        Assert.That(((Person[])active!).Select(x => x.Name), Is.EqualTo(new[] { "A" }));
    }

    [Test]
    public async Task If_else_and_for_each_execute_blocks()
    {
        var writer = new CaptureWriter();
        using ServiceProvider host = FluNetHost.Create(configure: services => services.AddSingleton<IOutputWriter>(writer));
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var state = new RuntimeState();
        state.SetVariable("users", new[] { "A", "B" });
        RuntimeResult result = await engine.RunAsync("IF true, THEN\nSAY \"yes\".\nELSE\nSAY \"no\".\nEND IF.\nFOR EACH [user] IN [users], DO\nSAY \"user=[user]\".\nEND FOR.", state);
        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(writer.Lines, Is.EqualTo(new[] { "yes", "user=A", "user=B" }));
    }

    [Test]
    public async Task Capability_policy_can_deny_file_access()
    {
        string file = Path.GetTempFileName();
        try
        {
            var options = new FluNetOptions { AllowedCapabilities = new HashSet<string>() };
            using ServiceProvider host = FluNetHost.Create(options);
            ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
            RuntimeResult result = await engine.RunAsync($"GET TEXT FROM {{{file}}} INTO [x].");
            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Any(x => x.Message.Contains("filesystem.read")), Is.True);
        }
        finally { File.Delete(file); }
    }

    [Test]
    public void Parser_builds_control_flow_ast_directly()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        var parse = engine.Parse("IF true, THEN\nSAY \"ok\".\nELSE\nSAY \"bad\".\nEND IF.\nFOR EACH [x] IN [xs], DO\nSAY \"[x]\".\nEND FOR.");
        Assert.That(parse.Success, Is.True);
        Assert.That(parse.Script.Statements[0], Is.TypeOf<IfNode>());
        Assert.That(parse.Script.Statements[1], Is.TypeOf<ForEachNode>());
    }

    public sealed record Person(string Name, bool Active, int Age);
    private sealed class CaptureWriter : IOutputWriter
    {
        public List<string> Lines { get; } = []; public ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default)
        {
            Lines.Add(text);
            return ValueTask.CompletedTask;
        }
    }
}
