using FluNET.Classic.Core;
using System.Text;

namespace FluNET.Classic.Csv;

public sealed record CsvRow(IReadOnlyDictionary<string, string> Values);
public sealed record CsvDocument(IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows);
public sealed record CsvOptions(char Delimiter = ',', bool HasHeader = true);
public sealed class CsvModule : LanguageModule
{
    public override string Name => "csv";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:csv-document", "CSV", typeof(CsvDocument)),
        new("qualifier:csv-headers", "HEADERS", typeof(string[])),
        new("qualifier:csv-rows", "ROWS", typeof(CsvRow[])),
        new("qualifier:csv-values", "VALUES", typeof(IReadOnlyDictionary<string, string>))
    };
}

[Verb("GET")]
[Qualifier("HEADERS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCsvHeaders : Get<string[], CsvDocument>
{
    public GetCsvHeaders([From] CsvDocument from) : base(from) { }

    protected override ValueTask<string[]> ActAsync(CsvDocument from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Headers.ToArray());
}

[Verb("GET")]
[Qualifier("ROWS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCsvRows : Get<CsvRow[], CsvDocument>
{
    public GetCsvRows([From] CsvDocument from) : base(from) { }

    protected override ValueTask<CsvRow[]> ActAsync(CsvDocument from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Rows.ToArray());
}

[Verb("GET")]
[Qualifier("VALUES")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetCsvRowValues : Get<IReadOnlyDictionary<string, string>, CsvRow>
{
    public GetCsvRowValues([From] CsvRow from) : base(from) { }

    protected override ValueTask<IReadOnlyDictionary<string, string>> ActAsync(CsvRow from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Values);
}

[Verb("PARSE")]
[Qualifier("CSV")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseCsv : IVerb<CsvDocument>, IParse, IFrom<string>, IWith<CsvOptions>, IPipelineConsumer<string>, IPipelineProducer<CsvDocument>
{
    private readonly string _text; private readonly CsvOptions _options;
    public ParseCsv([From] string text, [With] CsvOptions? options = null)
    {
        _text = text;
        _options = options ?? new();
    }
    public ValueTask<CsvDocument> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        string[][] lines = _text.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => ParseLine(x, _options.Delimiter).ToArray()).ToArray();
        if (lines.Length == 0)
            return ValueTask.FromResult(new CsvDocument(Array.Empty<string>(), Array.Empty<CsvRow>()));
        string[] headers = _options.HasHeader ? lines[0] : Enumerable.Range(1, lines.Max(x => x.Length)).Select(i => $"Column{i}").ToArray();
        int start = _options.HasHeader ? 1 : 0;
        CsvRow[] rows = lines.Skip(start).Select(values => new CsvRow(headers.Select((h, i) => (h, Value: i < values.Length ? values[i] : string.Empty)).ToDictionary(x => x.h, x => x.Value, StringComparer.OrdinalIgnoreCase))).ToArray();
        return ValueTask.FromResult(new CsvDocument(headers, rows));
    }
    private static IEnumerable<string> ParseLine(string line, char delimiter)
    {
        var value = new StringBuilder();
        bool quote = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (quote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                    quote = !quote;
            }
            else if (c == delimiter && !quote)
            {
                yield return value.ToString();
                value.Clear();
            }
            else
                value.Append(c);
        }
        yield return value.ToString();
    }
}

[Verb("FORMAT")]
[Qualifier("CSV")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatCsv : IVerb<string>, IFormat, IWhat<CsvDocument>, IWith<CsvOptions>, IPipelineConsumer<CsvDocument>, IPipelineProducer<string>
{
    private readonly CsvDocument _document; private readonly CsvOptions _options;
    public FormatCsv([What] CsvDocument document, [With] CsvOptions? options = null)
    {
        _document = document;
        _options = options ?? new();
    }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        string Escape(string s) => s.Contains(_options.Delimiter) || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var lines = new List<string>();
        if (_options.HasHeader)
            lines.Add(string.Join(_options.Delimiter, _document.Headers.Select(Escape)));
        lines.AddRange(_document.Rows.Select(row => string.Join(_options.Delimiter, _document.Headers.Select(h => Escape(row.Values.TryGetValue(h, out string? value) ? value : string.Empty)))));
        return ValueTask.FromResult(string.Join(Environment.NewLine, lines));
    }
}
