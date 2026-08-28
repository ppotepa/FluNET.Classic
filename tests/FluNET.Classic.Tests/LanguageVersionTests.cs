using FluNET.Classic.Core;
using FluNET.Classic.Hosting;
using FluNET.Classic.Syntax;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LanguageContractTests
{
    [Test]
    public void Formal_grammar_uses_the_single_language_contract()
    {
        using ServiceProvider host = FluNetHost.Create();
        _ = host.GetRequiredService<LanguageSnapshot>();

        Assert.That(ClassicGrammar.GrammarId, Is.EqualTo(ClassicLanguageContract.Id));
        Assert.That(ClassicGrammar.Ebnf, Does.Contain("result-binding"));
    }
}
