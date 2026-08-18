using System.Text.Json;
using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Runtime;

public sealed record CheckResult(ParseResult Parse, BoundScript? Bound)
{
    public bool Success => Parse.Success && Bound is not null && !Bound.HasErrors;
}

public sealed class ClassicEngine
{
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;
    private readonly BoundExecutor _executor;
    private readonly ClassicFormatter _formatter;
    private readonly ExecutionPlanner _planner;

    public ClassicEngine(ClassicParser parser, SemanticBinder binder, BoundExecutor executor, ClassicFormatter formatter, ExecutionPlanner planner)
    {
        _parser = parser;
        _binder = binder;
        _executor = executor;
        _formatter = formatter;
        _planner = planner;
    }

    public ParseResult Parse(string source) => _parser.Parse(source);

    public string Format(string source)
    {
        ParseResult parse = _parser.Parse(source);
        if (!parse.Success) throw new InvalidOperationException(string.Join(Environment.NewLine, parse.Diagnostics.Select(x => $"{x.Code}: {x.Message}")));
        return _formatter.Format(parse.Script);
    }

    public CheckResult Check(string source, IReadOnlyDictionary<string, Type>? variableTypes = null)
    {
        ParseResult parse = _parser.Parse(source);
        if (!parse.Success) return new(parse, null);
        return new(parse, _binder.Bind(parse.Script, variableTypes));
    }

    public ExecutionPlan Plan(string source, IReadOnlyDictionary<string, Type>? variableTypes = null) => _planner.Build(Check(source, variableTypes));

    public async ValueTask<RuntimeResult> RunAsync(string source, RuntimeState? state = null, CancellationToken cancellationToken = default)
    {
        state ??= new RuntimeState();
        Dictionary<string, Type> variableTypes = state.Variables.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value!.GetType(), StringComparer.OrdinalIgnoreCase);
        CheckResult check = Check(source, variableTypes);
        if (check.Bound is null)
            return new(state, check.Parse.Diagnostics.Select(x => new RuntimeDiagnostic(x.Code, x.Message)).ToArray());
        return await _executor.ExecuteAsync(check.Bound, state, cancellationToken).ConfigureAwait(false);
    }

    public string Explain(string source)
    {
        CheckResult check = Check(source);
        ExecutionPlan plan = _planner.Build(check);
        object result = new
        {
            canonicalSource = check.Parse.Success ? _formatter.Format(check.Parse.Script) : null,
            plan
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }
}
