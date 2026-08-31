using FluNET.Classic.Standard.Http;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class HttpHeadersTests
{
    [Test]
    public void Header_lookup_is_case_insensitive_for_any_input_dictionary()
    {
        var source = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["content-type"] = ["text/plain"],
            ["CONTENT-TYPE"] = ["charset=utf-8"]
        };

        var headers = new HttpHeaders(source);

        Assert.That(headers.TryGet("Content-Type", out IReadOnlyList<string> values), Is.True);
        Assert.That(values, Is.EqualTo(new[] { "text/plain", "charset=utf-8" }));

        source["content-type"][0] = "mutated";
        Assert.That(values, Is.EqualTo(new[] { "text/plain", "charset=utf-8" }));
    }
}
