using FluNET.Classic.Core;
using System.Text.RegularExpressions;

namespace FluNET.Classic.Web;

public sealed record HtmlDocument(string Source) : IValidState
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Source);
}
public sealed record WebLink(string Href, string? Text = null);
public sealed record HtmlSelector(string Value);

public sealed class WebModule : LanguageModule
{
    public override string Name => "web";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:links", "LINKS", typeof(WebLink[])),
        new("qualifier:html-source", "SOURCE", typeof(string)),
        new("qualifier:html-valid", "VALID", typeof(bool)),
        new("qualifier:link-href", "HREF", typeof(string)),
        new("qualifier:link-text", "TEXT", typeof(string))
    };
}

[Verb("GET")]
[Qualifier("SOURCE")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHtmlSource : Get<string, HtmlDocument>
{
    public GetHtmlSource([From] HtmlDocument from) : base(from) { }

    protected override ValueTask<string> ActAsync(HtmlDocument from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Source);
}

[Verb("GET")]
[Qualifier("VALID")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetHtmlValidity : Get<bool, HtmlDocument>
{
    public GetHtmlValidity([From] HtmlDocument from) : base(from) { }

    protected override ValueTask<bool> ActAsync(HtmlDocument from, CancellationToken cancellationToken) => ValueTask.FromResult(from.IsValid);
}

[Verb("GET")]
[Qualifier("HREF")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetWebLinkHref : Get<string, WebLink>
{
    public GetWebLinkHref([From] WebLink from) : base(from) { }

    protected override ValueTask<string> ActAsync(WebLink from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Href);
}

[Verb("GET")]
[Qualifier("HREF")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetWebLinkHrefs : Get<string[], WebLink[]>
{
    public GetWebLinkHrefs([From] WebLink[] from) : base(from) { }

    protected override ValueTask<string[]> ActAsync(WebLink[] from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Select(link => link.Href).ToArray());
}

[Verb("GET")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetWebLinkText : Get<string?, WebLink>
{
    public GetWebLinkText([From] WebLink from) : base(from) { }

    protected override ValueTask<string?> ActAsync(WebLink from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Text);
}

[Verb("GET")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetWebLinkTexts : Get<string?[], WebLink[]>
{
    public GetWebLinkTexts([From] WebLink[] from) : base(from) { }

    protected override ValueTask<string?[]> ActAsync(WebLink[] from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Select(link => link.Text).ToArray());
}

[Verb("PARSE")]
[Qualifier("HTML")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseHtml : IVerb<HtmlDocument>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<HtmlDocument>
{
    private readonly string _html; public ParseHtml([From] string html) => _html = html;
    public ValueTask<HtmlDocument> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new HtmlDocument(_html));
}

[Verb("FORMAT")]
[Qualifier("HTML")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatHtml : IVerb<string>, IFormat, IWhat<HtmlDocument>, IPipelineConsumer<HtmlDocument>, IPipelineProducer<string>
{
    private readonly HtmlDocument _document; public FormatHtml([What] HtmlDocument document) => _document = document;
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_document.Source);
}

[Verb("LIST")]
[Qualifier("LINKS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ListHtmlLinks : IVerb<WebLink[]>, IListVerb, IFrom<HtmlDocument>, IPipelineConsumer<HtmlDocument>, IPipelineProducer<WebLink[]>
{
    private static readonly Regex LinkPattern = new("<a\\s+[^>]*href\\s*=\\s*[\"'](?<href>[^\"']+)[\"'][^>]*>(?<text>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly HtmlDocument _document; public ListHtmlLinks([From] HtmlDocument document) => _document = document;
    public ValueTask<WebLink[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(LinkPattern.Matches(_document.Source).Select(x => new WebLink(x.Groups["href"].Value, Regex.Replace(x.Groups["text"].Value, "<.*?>", string.Empty))).ToArray());
}
