using FluNET.Classic.Binding;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ConversionResolutionDeterminismTests
{
    [Test]
    public void Equal_cost_conversion_paths_are_reported_as_ambiguous()
    {
        var registry = new ValueConversionRegistry();
        registry.Register(new AToB());
        registry.Register(new BToD());
        registry.Register(new AToC());
        registry.Register(new CToD());

        ConversionPlanningResult result = registry.Plan(typeof(A), typeof(D));

        Assert.That(result.Status, Is.EqualTo(ConversionPlanningStatus.Ambiguous));
        Assert.That(result.Alternatives, Has.Count.EqualTo(2));
        Assert.That(result.Alternatives.Select(x => x.Cost).Distinct().Single(), Is.EqualTo(4));
    }

    [Test]
    public void Higher_priority_converter_wins_for_the_same_edge()
    {
        var registry = new ValueConversionRegistry();
        registry.Register(new AToB("low"), priority: 0);
        registry.Register(new AToB("high"), priority: 10);

        Assert.That(registry.TryConvert(new A("source"), typeof(B), out ConversionResult? converted), Is.True);
        Assert.That(((B)converted!.Value!).Value, Is.EqualTo("high"));
    }

    [Test]
    public void Equal_priority_successful_converters_for_same_edge_are_ambiguous()
    {
        var registry = new ValueConversionRegistry();
        registry.Register(new AToB("one"), priority: 5);
        registry.Register(new AToB("two"), priority: 5);

        ConversionPlanningResult result = registry.Plan(typeof(A), typeof(B));

        Assert.That(result.Status, Is.EqualTo(ConversionPlanningStatus.Ambiguous));
        Assert.That(result.Alternatives, Has.Count.EqualTo(2));
    }

    [Test]
    public void Numeric_widening_is_cheaper_and_lossless_relative_to_narrowing()
    {
        var registry = new ValueConversionRegistry();

        ConversionPlan widening = registry.Plan(typeof(int), typeof(long)).Plan!;
        ConversionPlan narrowing = registry.Plan(typeof(long), typeof(int)).Plan!;

        Assert.That(widening.Cost, Is.LessThan(narrowing.Cost));
        Assert.That(widening.Safety, Is.EqualTo(ConversionSafety.Lossless));
        Assert.That(narrowing.Safety, Is.EqualTo(ConversionSafety.PotentiallyLossy));
    }

    [Test]
    public void Equal_priority_successful_resolvers_are_reported_as_ambiguous()
    {
        var registry = new ValueResolverRegistry();
        registry.Register(new TestResolver("one"), priority: 5);
        registry.Register(new TestResolver("two"), priority: 5);

        ResolutionResult result = registry.Resolve("value", typeof(Resolved), new ResolutionContext(typeof(Resolved)));

        Assert.That(result.Status, Is.EqualTo(ResolutionStatus.Ambiguous));
        Assert.That(result.Candidates, Has.Count.EqualTo(2));
    }

    [Test]
    public void Higher_priority_resolver_wins()
    {
        var registry = new ValueResolverRegistry();
        registry.Register(new TestResolver("low"), priority: 0);
        registry.Register(new TestResolver("high"), priority: 10);

        ResolutionResult result = registry.Resolve("value", typeof(Resolved), new ResolutionContext(typeof(Resolved)));

        Assert.That(result.Success, Is.True);
        Assert.That(((Resolved)result.Value!).Value, Is.EqualTo("high"));
    }

    [Test]
    public void Contextual_resolver_can_decline_a_context_before_resolution()
    {
        var registry = new ValueResolverRegistry();
        registry.Register(new VerbScopedResolver("get-only", "GET"), priority: 10);
        registry.Register(new TestResolver("fallback"), priority: 0);

        ResolutionResult result = registry.Resolve("value", typeof(Resolved), new ResolutionContext(typeof(Resolved), VerbName: "SAVE"));

        Assert.That(result.Success, Is.True);
        Assert.That(((Resolved)result.Value!).Value, Is.EqualTo("fallback"));
    }

    [Test]
    public void Explicit_resolver_and_converter_ids_are_stable_and_explainable()
    {
        var resolvers = new ValueResolverRegistry();
        resolvers.Register(new TestResolver("value"), priority: 5, id: "fixture.resolver");
        ResolutionResult resolution = resolvers.Resolve("source", typeof(Resolved), new ResolutionContext(typeof(Resolved)));

        var converters = new ValueConversionRegistry();
        converters.Register(new AToB(), priority: 5, id: "fixture.converter");
        ConversionPlan plan = converters.Plan(typeof(A), typeof(B)).Plan!;

        Assert.That(resolution.Resolver, Is.EqualTo("fixture.resolver"));
        Assert.That(plan.Steps.Single().ConverterId, Is.EqualTo("fixture.converter"));
    }

    [Test]
    public void Default_registration_ids_do_not_depend_on_registration_order_counter()
    {
        var first = new ValueResolverRegistry();
        first.Register(new TestResolver("first"));
        var second = new ValueResolverRegistry();
        second.Register(new TestResolver("second"));

        ResolutionResult firstResult = first.Resolve("source", typeof(Resolved), new ResolutionContext(typeof(Resolved)));
        ResolutionResult secondResult = second.Resolve("source", typeof(Resolved), new ResolutionContext(typeof(Resolved)));

        Assert.That(secondResult.Resolver, Is.EqualTo(firstResult.Resolver));
    }

    private sealed record A(string Value);
    private sealed record B(string Value);
    private sealed record C(string Value);
    private sealed record D(string Value);
    private sealed record Resolved(string Value);

    private sealed class AToB(string value = "b") : ValueConverter<A, B>
    {
        public override bool TryConvert(A valueIn, out B? result)
        {
            result = new B(value);
            return true;
        }
    }
    private sealed class BToD : ValueConverter<B, D>
    {
        public override bool TryConvert(B value, out D? result)
        {
            result = new D(value.Value);
            return true;
        }
    }
    private sealed class AToC : ValueConverter<A, C>
    {
        public override bool TryConvert(A value, out C? result)
        {
            result = new C(value.Value);
            return true;
        }
    }
    private sealed class CToD : ValueConverter<C, D>
    {
        public override bool TryConvert(C value, out D? result)
        {
            result = new D(value.Value);
            return true;
        }
    }

    private class TestResolver(string value) : IValueResolver<Resolved>
    {
        public Type TargetType => typeof(Resolved);
        public virtual bool TryResolve(string source, ResolutionContext context, out Resolved? resolved)
        {
            resolved = new Resolved(value);
            return true;
        }
        bool IValueResolver.TryResolve(string source, ResolutionContext context, out object? resolved)
        {
            bool ok = TryResolve(source, context, out Resolved? typed);
            resolved = typed;
            return ok;
        }
    }

    private sealed class VerbScopedResolver(string value, string verb) : TestResolver(value), IContextualValueResolver
    {
        public bool CanResolve(ResolutionContext context) => string.Equals(context.VerbName, verb, StringComparison.OrdinalIgnoreCase);
    }
}
