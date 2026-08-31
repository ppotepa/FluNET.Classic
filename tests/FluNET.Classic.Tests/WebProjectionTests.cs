using FluNET.Classic.Web;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class WebProjectionTests
{
    [Test]
    public async Task Html_and_link_projections_return_typed_fields()
    {
        var document = new HtmlDocument("<a href=\"/docs\">Docs</a>");
        var links = new[] { new WebLink("/docs", "Docs"), new WebLink("/api") };

        Assert.That(await new GetHtmlSource(document).ExecuteAsync(null!), Is.EqualTo(document.Source));
        Assert.That(await new GetHtmlValidity(document).ExecuteAsync(null!), Is.True);
        Assert.That(await new GetWebLinkHref(links[0]).ExecuteAsync(null!), Is.EqualTo("/docs"));
        Assert.That(await new GetWebLinkHrefs(links).ExecuteAsync(null!), Is.EqualTo(new[] { "/docs", "/api" }));
        Assert.That(await new GetWebLinkText(links[0]).ExecuteAsync(null!), Is.EqualTo("Docs"));
        Assert.That(await new GetWebLinkTexts(links).ExecuteAsync(null!), Is.EqualTo(new string?[] { "Docs", null }));
    }
}
