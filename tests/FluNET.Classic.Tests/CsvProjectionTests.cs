using FluNET.Classic.Csv;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class CsvProjectionTests
{
    [Test]
    public async Task Csv_documents_and_rows_expose_typed_projections()
    {
        var values = new Dictionary<string, string> { ["name"] = "Ada" };
        var row = new CsvRow(values);
        var document = new CsvDocument(new[] { "name" }, new[] { row });

        Assert.That(await new GetCsvHeaders(document).ExecuteAsync(null!), Is.EqualTo(new[] { "name" }));
        Assert.That(await new GetCsvRows(document).ExecuteAsync(null!), Is.EqualTo(new[] { row }));
        Assert.That(await new GetCsvRowValues(row).ExecuteAsync(null!), Is.SameAs(values));
    }
}
