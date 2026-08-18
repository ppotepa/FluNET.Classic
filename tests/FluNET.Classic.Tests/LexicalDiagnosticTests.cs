using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class LexicalDiagnosticTests
{
    [TestCase("SAY \"unterminated", "FLU-LEX-003")]
    [TestCase("CHECK IF [value IS true.", "FLU-LEX-002")]
    [TestCase("GET TEXT FROM {file.txt", "FLU-LEX-001")]
    [TestCase("SAY \"bad\\qescape\".", "FLU-LEX-004")]
    public void Lexer_errors_are_reported_through_engine_check(string source, string code)
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        CheckResult check = engine.Check(source);

        Assert.That(check.Success, Is.False);
        Assert.That(check.Parse.Diagnostics.Any(x => x.Code == code), Is.True);
    }
}
