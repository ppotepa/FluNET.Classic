using FluNET.Classic.Core;

namespace FluNET.Classic.Sql.PostgreSql;

public interface IPostgreSqlClientAdapter
{
    ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default);
    ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default);
}
public sealed class PostgreSqlExecutor : ISqlExecutor
{
    private readonly IPostgreSqlClientAdapter _client; public PostgreSqlExecutor(IPostgreSqlClientAdapter client) => _client = client;
    public ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default) => _client.QueryAsync(connection, query, cancellationToken);
    public ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default) => _client.ExecuteAsync(connection, command, cancellationToken);
}
public sealed class PostgreSqlModule : LanguageModule
{
    public override string Name => "sql.postgresql"; public override IReadOnlyCollection<string> Dependencies => new[] { "sql" };
}
