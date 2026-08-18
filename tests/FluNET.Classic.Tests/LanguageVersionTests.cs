using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LanguageVersionTests
{
    [Test]
    public void Snapshot_and_formal_grammar_share_the_0_2_contract()
    {
        using ServiceProvider host = FluNetHost.Create();
        LanguageSnapshot snapshot = host.GetRequiredService<LanguageSnapshot>();

        Assert.That(snapshot.LanguageVersion.Name, Is.EqualTo("0.2"));
        Assert.That(snapshot.LanguageVersion.GrammarId, Is.EqualTo(ClassicGrammar.GrammarId));
        Assert.That(ClassicGrammar.Ebnf, Does.Contain("result-binding"));
        Assert.DoesNotThrow(() => ClassicGrammar.EnsureCompatible(snapshot));
    }
}
