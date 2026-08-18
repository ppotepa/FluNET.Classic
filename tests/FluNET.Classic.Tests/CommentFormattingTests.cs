using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class CommentFormattingTests
{
    [Test]
    public void Formatter_preserves_root_and_block_comments()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();
        string source = """
            # prepare value
            CHECK IF true INTO [ok]. # branch decision
            IF [ok] IS true THEN {
                # inside block
                CHECK IF true.
            }
            """;

        string formatted = engine.Format(source);

        Assert.That(formatted, Does.Contain("# prepare value"));
        Assert.That(formatted, Does.Contain("# branch decision"));
        Assert.That(formatted, Does.Contain("# inside block"));
        Assert.That(engine.Format(formatted), Is.EqualTo(formatted));
    }
}
