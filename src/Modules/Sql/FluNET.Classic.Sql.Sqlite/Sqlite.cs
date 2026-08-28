using FluNET.Classic.Core;
using FluNET.Classic.Sql;

namespace FluNET.Classic.Sql.Sqlite;

public interface ISqliteClientAdapter
{
    ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default);
    ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default);
}
public sealed class SqliteExecutor : ISqlExecutor
{
    private readonly ISqliteClientAdapter _client; public SqliteExecutor(ISqliteClientAdapter client) => _client = client;
    public ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default) => _client.QueryAsync(connection, query, cancellationToken);
    public ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default) => _client.ExecuteAsync(connection, command, cancellationToken);
}
public sealed class SqliteModule : LanguageModule { public override string Name => "sql.sqlite"; public override IReadOnlyCollection<string> Dependencies => new[] { "sql" }; }
