using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.OutputProjectionFixture;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class NullabilityBindingTests
{
    [Test]
    public void Null_is_rejected_for_non_nullable_role()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("SAY null.");

        Assert.That(check.Success, Is.False);
        Assert.That(check.Bound!.Diagnostics.Any(x => x.Code == "FLU-BIND-010" && x.Candidates?.Any(c => c.Contains("does not accept null", StringComparison.OrdinalIgnoreCase)) == true), Is.True);
    }

    [Test]
    public async Task Null_is_accepted_for_nullable_reference_role_as_real_null()
    {
        using ServiceProvider host = CreateFixtureHost();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        RuntimeResult result = await engine.RunAsync("ACCEPTNULL null INTO [result].");

        Assert.That(result.Success, Is.True, string.Join("; ", result.Diagnostics.Select(x => x.Message)));
        Assert.That(result.State.Variables["result"], Is.EqualTo("<null>"));
    }

    [Test]
    public void Null_does_not_fall_through_to_text_resolvers_as_empty_string()
    {
        var resolver = new RecordingStringResolver();
        var options = new FluNetOptions
        {
            ConfigureResolvers = registry => registry.Register<string>(resolver, priority: 100)
        };
        options.Modules.Add(new ProjectionFixtureModule());
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check("ACCEPTNULL null INTO [result].");

        Assert.That(check.Success, Is.True, string.Join("; ", check.Bound!.Diagnostics.Select(x => x.Message)));
        Assert.That(resolver.Calls, Is.EqualTo(0));
    }

    private static ServiceProvider CreateFixtureHost()
    {
        var options = new FluNetOptions();
        options.Modules.Add(new ProjectionFixtureModule());
        return FluNetHost.Create(options);
    }

    private sealed class RecordingStringResolver : IValueResolver<string>
    {
        public Type TargetType => typeof(string);
        public int Calls
        {
            get; private set;
        }
        public bool TryResolve(string source, ResolutionContext context, out string? value)
        {
            Calls++;
            value = source;
            return true;
        }
        bool IValueResolver.TryResolve(string source, ResolutionContext context, out object? value)
        {
            bool ok = TryResolve(source, context, out string? typed);
            value = typed;
            return ok;
        }
    }
}
