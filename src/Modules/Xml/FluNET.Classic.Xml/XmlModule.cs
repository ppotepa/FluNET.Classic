using FluNET.Classic.Core;
using System.Xml.Linq;

namespace FluNET.Classic.Xml;

public sealed record XmlPath(string Value);
public sealed class XmlModule : LanguageModule
{
    public override string Name => "xml";
}

[Verb("PARSE")]
[Qualifier("XML")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseXml : IVerb<XDocument>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<XDocument>
{
    private readonly string _text; public ParseXml([From] string text) => _text = text;
    public ValueTask<XDocument> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(XDocument.Parse(_text));
}

[Verb("FORMAT")]
[Qualifier("XML")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatXml : IVerb<string>, IFormat, IWhat<XDocument>, IPipelineConsumer<XDocument>, IPipelineProducer<string>
{
    private readonly XDocument _document; public FormatXml([What] XDocument document) => _document = document;
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_document.ToString());
}

[Verb("GET")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetXmlElementText : IVerb<string?>, IGet, IFrom<XDocument>, IAt<XmlPath>, IPipelineProducer<string?>
{
    private readonly XDocument _document; private readonly XmlPath _path;
    public GetXmlElementText([From] XDocument document, [At] XmlPath path)
    {
        _document = document;
        _path = path;
    }
    public ValueTask<string?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        XElement? current = _document.Root;
        foreach (string segment in _path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            current = current?.Element(segment);
        return ValueTask.FromResult(current?.Value);
    }
}
