using FluNET.Classic.Binding;
using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ResolutionSourceKindTests
{
    [Test]
    public void Binder_propagates_reference_identifier_and_literal_source_kinds()
    {
        var resolver = new RecordingFileResolver();
        var options = new FluNetOptions
        {
            ConfigureResolvers = registry => registry.Register<FileInfo>(resolver, priority: 100)
        };
        using ServiceProvider host = FluNetHost.Create(options);
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        Assert.That(engine.Check("GET TEXT FROM {reference.txt} INTO [a].").Success, Is.True);
        Assert.That(engine.Check("GET TEXT FROM identifier.txt INTO [b].").Success, Is.True);
        Assert.That(engine.Check("GET TEXT FROM \"literal.txt\" INTO [c].").Success, Is.True);

        Assert.That(resolver.Kinds, Does.Contain(ResolutionSourceKind.Reference));
        Assert.That(resolver.Kinds, Does.Contain(ResolutionSourceKind.Identifier));
        Assert.That(resolver.Kinds, Does.Contain(ResolutionSourceKind.Literal));
    }

    [Test]
    public void Resolver_registry_does_not_split_one_string_into_collection_values()
    {
        var registry = new ValueResolverRegistry();
        var context = new ResolutionContext(typeof(string[]), SourceKind: ResolutionSourceKind.Literal);

        bool resolved = registry.TryResolve("a,b", typeof(string[]), context, out _);

        Assert.That(resolved, Is.False);
    }

    private sealed class RecordingFileResolver : IValueResolver<FileInfo>
    {
        public Type TargetType => typeof(FileInfo);
        public List<ResolutionSourceKind> Kinds { get; } = [];

        public bool TryResolve(string source, ResolutionContext context, out FileInfo? value)
        {
            Kinds.Add(context.SourceKind);
            value = new FileInfo(source);
            return true;
        }

        bool IValueResolver.TryResolve(string source, ResolutionContext context, out object? value)
        {
            bool result = TryResolve(source, context, out FileInfo? file);
            value = file;
            return result;
        }
    }
}
