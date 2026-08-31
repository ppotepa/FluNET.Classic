using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class BindingDiagnosticsTests
{
    [Test]
    public void Ambiguous_conversion_is_a_binding_diagnostic_not_registration_order()
    {
        var options = new FluNetOptions
        {
            ConfigureConverters = conversions =>
            {
                conversions.Register(new SourceToText("one"), priority: 5);
                conversions.Register(new SourceToText("two"), priority: 5);
            }
        };
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult result = engine.Check("SAY [value].", new Dictionary<string, Type> { ["value"] = typeof(Source) });

        BindingDiagnostic diagnostic = result.Bound!.Diagnostics.Single(x => x.Code == "FLU-BIND-170");
        Assert.That(diagnostic.Candidates, Has.Count.EqualTo(2));
    }

    [Test]
    public void Ambiguous_resolution_is_a_binding_diagnostic_with_resolver_candidates()
    {
        var options = new FluNetOptions
        {
            ConfigureResolvers = resolvers =>
            {
                resolvers.Register(new FileResolver("one"), priority: 5);
                resolvers.Register(new FileResolver("two"), priority: 5);
            }
        };
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult result = engine.Check("GET TEXT FROM {anything.txt} INTO [lines].");

        BindingDiagnostic diagnostic = result.Bound!.Diagnostics.First(x => x.Code == "FLU-BIND-171");
        Assert.That(diagnostic.Candidates, Has.Count.EqualTo(2));
    }

    [Test]
    public void No_overload_diagnostic_contains_structured_candidate_rejections()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult result = engine.Check("RUN {dotnet} WITH \"--version\" WITH \"extra\".");

        BindingDiagnostic diagnostic = result.Bound!.Diagnostics.Single(x => x.Code == "FLU-BIND-010");
        Assert.That(diagnostic.CandidateDetails, Is.Not.Null.And.Not.Empty);
        Assert.That(diagnostic.CandidateDetails!.Any(x => !string.IsNullOrWhiteSpace(x.PatternId)), Is.True);
        Assert.That(diagnostic.CandidateDetails.Any(x => x.RoleFailures is { Count: > 0 }), Is.True);
    }

    [Test]
    public void Execution_plan_exposes_each_selected_conversion_step()
    {
        var options = new FluNetOptions
        {
            ConfigureConverters = conversions =>
            {
                conversions.Register(new SourceToIntermediate());
                conversions.Register(new IntermediateToText());
            }
        };
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        ExecutionPlan plan = engine.Plan("SAY [value].", new Dictionary<string, Type> { ["value"] = typeof(Source) });

        ExecutionPlanValue value = plan.Steps.Single().Children.Single().Roles.Single().Values.Single();
        Assert.That(value.Kind, Is.EqualTo("conversion"));
        Assert.That(value.ConversionSteps, Has.Count.EqualTo(2));
    }

    private sealed record Source(string Value);
    private sealed record Intermediate(string Value);

    private sealed class SourceToText(string value) : ValueConverter<Source, string>
    {
        public override bool TryConvert(Source source, out string? result)
        {
            result = value;
            return true;
        }
    }

    private sealed class SourceToIntermediate : ValueConverter<Source, Intermediate>
    {
        public override bool TryConvert(Source value, out Intermediate? result)
        {
            result = new Intermediate(value.Value);
            return true;
        }
    }

    private sealed class IntermediateToText : ValueConverter<Intermediate, string>
    {
        public override bool TryConvert(Intermediate value, out string? result)
        {
            result = value.Value;
            return true;
        }
    }

    private sealed class FileResolver(string suffix) : IValueResolver<FileInfo>
    {
        public Type TargetType => typeof(FileInfo);
        public bool TryResolve(string source, ResolutionContext context, out FileInfo? value)
        {
            value = new FileInfo(source + suffix);
            return true;
        }
        bool IValueResolver.TryResolve(string source, ResolutionContext context, out object? value)
        {
            bool ok = TryResolve(source, context, out FileInfo? typed);
            value = typed;
            return ok;
        }
    }
}
