using FluNET.Classic.Standard.Http;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class HttpValueValidationTests
{
    [TestCase(99)]
    [TestCase(600)]
    public void Http_status_rejects_codes_outside_the_HTTP_range(int code)
    {
        Assert.That(() => new HttpStatus(code, null), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ETag_rejects_empty_values()
    {
        Assert.That(() => new ETag("  "), Throws.TypeOf<ArgumentException>());
    }
}
