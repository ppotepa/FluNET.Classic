using FluNET.Classic.Hosting;
using FluNET.Classic.Tooling;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class ToolingScopeTests
{
    [Test]
    public void Rename_of_loop_iterator_does_not_touch_same_named_outer_variable()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService tooling = host.GetRequiredService<ClassicDocumentService>();
        string source = """
            CHECK IF true INTO [item].
            FOR EACH [item] IN [items] THEN {
                CHECK IF [item.Value] > 0.
            }
            CHECK IF [item] IS true.
            """;

        int iteratorPosition = source.IndexOf("[item] IN", StringComparison.Ordinal) + 2;
        IReadOnlyList<DocumentTextEdit> edits = tooling.Rename(source, iteratorPosition, "entry");

        Assert.That(edits, Has.Count.EqualTo(2));
        Assert.That(edits.All(x => x.NewText.StartsWith("[entry", StringComparison.Ordinal)), Is.True);
        Assert.That(edits.Any(x => x.Span.Start == source.IndexOf("[item].", StringComparison.Ordinal)), Is.False);
    }

    [Test]
    public void Definition_inside_loop_resolves_to_iterator_not_outer_variable()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicDocumentService tooling = host.GetRequiredService<ClassicDocumentService>();
        string source = """
            CHECK IF true INTO [item].
            FOR EACH [item] IN [items] THEN {
                CHECK IF [item.Value] > 0.
            }
            """;

        int referencePosition = source.IndexOf("[item.Value]", StringComparison.Ordinal) + 2;
        DocumentSymbolInfo? definition = tooling.Definition(source, referencePosition);

        int expected = source.IndexOf("[item] IN", StringComparison.Ordinal);
        Assert.That(definition, Is.Not.Null);
        Assert.That(definition!.Kind, Is.EqualTo("iterator"));
        Assert.That(definition.Span.Start, Is.EqualTo(expected));
    }
}
