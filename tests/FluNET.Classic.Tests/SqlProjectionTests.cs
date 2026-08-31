using FluNET.Classic.Sql;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class SqlProjectionTests
{
    [Test]
    public async Task Sql_results_and_rows_expose_typed_projections()
    {
        var values = new Dictionary<string, object?> { ["id"] = 7, ["name"] = "Ada" };
        var row = new SqlRow(values);
        var result = new SqlResult(1, new[] { row });

        Assert.That(await new GetSqlResultRows(result).ExecuteAsync(null!), Is.EqualTo(new[] { row }));
        Assert.That(await new GetSqlAffectedRows(result).ExecuteAsync(null!), Is.EqualTo(1));
        Assert.That(await new GetSqlRowValues(row).ExecuteAsync(null!), Is.SameAs(values));
    }
}
