using FluNET.Classic.Core;

namespace FluNET.Classic.Sql;

public sealed record DatabaseConnection(string Name);
public sealed record SqlQuery(string Text, IReadOnlyDictionary<string, object?>? Parameters = null);
public sealed record SqlCommand(string Text, IReadOnlyDictionary<string, object?>? Parameters = null);
public sealed record SqlRow(IReadOnlyDictionary<string, object?> Values);
public sealed record SqlResult(int AffectedRows, IReadOnlyList<SqlRow> Rows) : IOkState
{
    public bool IsOk => AffectedRows >= 0;
}

public interface ISqlExecutor
{
    ValueTask<IReadOnlyList<SqlRow>> QueryAsync(DatabaseConnection connection, SqlQuery query, CancellationToken cancellationToken = default);
    ValueTask<SqlResult> ExecuteAsync(DatabaseConnection connection, SqlCommand command, CancellationToken cancellationToken = default);
}

public sealed class SqlModule : LanguageModule
{
    public override string Name => "sql";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:rows", "ROWS", typeof(SqlRow[])),
        new("qualifier:sql-result", "RESULT", typeof(SqlResult)),
        new("qualifier:affected-rows", "AFFECTEDROWS", typeof(int)),
        new("qualifier:row-values", "VALUES", typeof(IReadOnlyDictionary<string, object?>))
    };
}

[Verb("GET")]
[Qualifier("ROWS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetSqlResultRows : Get<SqlRow[], SqlResult>
{
    public GetSqlResultRows([From] SqlResult from) : base(from) { }

    protected override ValueTask<SqlRow[]> ActAsync(SqlResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Rows.ToArray());
}

[Verb("GET")]
[Qualifier("AFFECTEDROWS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetSqlAffectedRows : Get<int, SqlResult>
{
    public GetSqlAffectedRows([From] SqlResult from) : base(from) { }

    protected override ValueTask<int> ActAsync(SqlResult from, CancellationToken cancellationToken) => ValueTask.FromResult(from.AffectedRows);
}

[Verb("GET")]
[Qualifier("VALUES")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetSqlRowValues : Get<IReadOnlyDictionary<string, object?>, SqlRow>
{
    public GetSqlRowValues([From] SqlRow from) : base(from) { }

    protected override ValueTask<IReadOnlyDictionary<string, object?>> ActAsync(SqlRow from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Values);
}

[Verb("GET")]
[Qualifier("ROWS")]
[RequiresCapability(StandardCapabilities.SqlRead)]
[ExecutionTrait(ExecutionTrait.Retryable)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class GetSqlRows : IVerb<SqlRow[]>, IGet, IFrom<SqlQuery>, IUsing<DatabaseConnection>, IPipelineProducer<SqlRow[]>
{
    private readonly SqlQuery _query; private readonly DatabaseConnection _connection; private readonly ISqlExecutor _executor;
    public GetSqlRows([From] SqlQuery query, [Using] DatabaseConnection connection, [FromServices] ISqlExecutor executor)
    {
        _query = query;
        _connection = connection;
        _executor = executor;
    }
    public async ValueTask<SqlRow[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => (await _executor.QueryAsync(_connection, _query, cancellationToken).ConfigureAwait(false)).ToArray();
}

[Verb("RUN")]
[Qualifier("RESULT")]
[RequiresCapability(StandardCapabilities.SqlWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.Transactional)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class RunSqlCommand : IVerb<SqlResult>, IRun, IWhat<SqlCommand>, IUsing<DatabaseConnection>, IPipelineProducer<SqlResult>
{
    private readonly SqlCommand _command; private readonly DatabaseConnection _connection; private readonly ISqlExecutor _executor;
    public RunSqlCommand([What] SqlCommand command, [Using] DatabaseConnection connection, [FromServices] ISqlExecutor executor)
    {
        _command = command;
        _connection = connection;
        _executor = executor;
    }
    public ValueTask<SqlResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _executor.ExecuteAsync(_connection, _command, cancellationToken);
}
