using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LanguageSurfaceTests
{
    [Test]
    public void Standard_snapshot_exposes_predicates_operators_and_collection_intrinsics()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();

        Assert.That(language.TryGetPredicate("EXISTS", out PredicateDescriptor exists), Is.True);
        Assert.That(exists.Syntax, Is.EqualTo(PredicateSyntaxKind.Postfix));
        Assert.That(language.TryGetPredicate("EMPTY", out _), Is.True);
        Assert.That(language.TryGetOperator("STARTS WITH", out OperatorDescriptor starts), Is.True);
        Assert.That(starts.Precedence, Is.EqualTo(3));
        Assert.That(language.TryGetIntrinsic("SORT", out IntrinsicDescriptor sort), Is.True);
        Assert.That(sort.Syntax, Is.EqualTo(IntrinsicSyntaxKind.CollectionBy));
        Assert.That(language.TryGetIntrinsic("COUNT", out _), Is.True);
    }

    [Test]
    public void Parser_discovers_custom_predicate_operator_and_intrinsic_from_snapshot_metadata()
    {
        var ready = new PredicateDescriptor("predicate:test:ready", "READY", PredicateSyntaxKind.IsState);
        var overlaps = new OperatorDescriptor("operator:test:overlaps", "OVERLAPS", 3);
        var top = new IntrinsicDescriptor("intrinsic:test:top", "TOP", IntrinsicSyntaxKind.CollectionAmountFrom);
        var snapshot = new LanguageSnapshot(
            Array.Empty<VerbDescriptor>(),
            StandardQualifiers.All,
            Array.Empty<ModuleDescriptor>(),
            StandardLanguageSurface.Predicates.Append(ready),
            StandardLanguageSurface.Operators.Append(overlaps),
            new[] { top });
        var parser = new ClassicParser(snapshot);

        ParseResult predicate = parser.Parse("CHECK IF true IS READY INTO [ready].");
        ParseResult @operator = parser.Parse("CHECK IF \"alpha\" OVERLAPS \"a\" INTO [match].");
        ParseResult intrinsic = parser.Parse("TOP 2 FROM [items] INTO [top].");

        Assert.That(predicate.Success, Is.True, string.Join("; ", predicate.Diagnostics.Select(x => x.Message)));
        Assert.That(((CheckStageNode)((PipelineNode)predicate.Script.Statements.Single()).Stages.Single()).Condition, Is.TypeOf<PredicateExpression>());
        Assert.That(@operator.Success, Is.True, string.Join("; ", @operator.Diagnostics.Select(x => x.Message)));
        Assert.That(((CheckStageNode)((PipelineNode)@operator.Script.Statements.Single()).Stages.Single()).Condition, Is.TypeOf<BinaryExpression>());
        Assert.That(intrinsic.Success, Is.True, string.Join("; ", intrinsic.Diagnostics.Select(x => x.Message)));
        Assert.That(((CollectionStageNode)((PipelineNode)intrinsic.Script.Statements.Single()).Stages.Single()).Operation, Is.EqualTo("TOP"));
    }

    [Test]
    public void Language_manifest_includes_semantic_surface_metadata()
    {
        using ServiceProvider host = FluNetHost.Create();
        string manifest = host.GetRequiredService<LanguageIntrospectionService>().ToJson();

        Assert.That(manifest, Does.Contain("\"predicates\""));
        Assert.That(manifest, Does.Contain("\"operators\""));
        Assert.That(manifest, Does.Contain("\"intrinsics\""));
        Assert.That(manifest, Does.Contain("STARTS WITH"));
        Assert.That(manifest, Does.Contain("SORT"));
    }

    [Test]
    public void Language_snapshot_collections_are_not_mutable_through_common_collection_interfaces()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot language = host.GetRequiredService<LanguageSnapshot>();
        IList<VerbDescriptor> verbs = (IList<VerbDescriptor>)language.Verbs;

        Assert.That(verbs.IsReadOnly, Is.True);
        Assert.That(() => verbs.Add(new VerbDescriptor("test", "TEST", Array.Empty<string>(), Array.Empty<VerbImplementationDescriptor>())),
            Throws.TypeOf<NotSupportedException>());
        ISet<string> literalWords = (ISet<string>)language.LiteralWords;
        ISet<string> reservedWords = (ISet<string>)language.ReservedWords;
        Assert.That(literalWords.IsReadOnly, Is.True);
        Assert.That(reservedWords.IsReadOnly, Is.True);
        Assert.That(() => literalWords.Add("TEST"), Throws.TypeOf<NotSupportedException>());
        Assert.That(() => reservedWords.Add("TEST"), Throws.TypeOf<NotSupportedException>());
    }
}
