using FluNET.Classic.Core;

namespace FluNET.Classic.Sql.SqlServer;

public interface ISqlServerClientAdapter
{
    ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default);
    ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default);
}
public sealed class SqlServerExecutor : ISqlExecutor
{
    private readonly ISqlServerClientAdapter _client; public SqlServerExecutor(ISqlServerClientAdapter client) => _client = client;
    public ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default) => _client.QueryAsync(connection, query, cancellationToken);
    public ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default) => _client.ExecuteAsync(connection, command, cancellationToken);
}
public sealed class SqlServerModule : LanguageModule { public override string Name => "sql.sqlserver"; public override IReadOnlyCollection<string> Dependencies => new[] { "sql" }; }
