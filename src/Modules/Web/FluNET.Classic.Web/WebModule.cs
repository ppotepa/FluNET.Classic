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
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:links", "LINKS", typeof(WebLink[])) };
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
