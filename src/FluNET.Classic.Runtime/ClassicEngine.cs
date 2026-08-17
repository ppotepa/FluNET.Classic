using System.Text.Json;
using FluNET.Classic.Binding;
using FluNET.Classic.Core;
using FluNET.Classic.Syntax;

namespace FluNET.Classic.Runtime;

public sealed record CheckResult(ParseResult Parse, BoundScript? Bound)
{
    public bool Success => Parse.Success && Bound is not null && Bound.Diagnostics.Count == 0;
}

public sealed class ClassicEngine
{
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;
    private readonly BoundExecutor _executor;
    private readonly ClassicFormatter _formatter;

    public ClassicEngine(ClassicParser parser, SemanticBinder binder, BoundExecutor executor, ClassicFormatter formatter)
    {
        _parser = parser;
        _binder = binder;
        _executor = executor;
        _formatter = formatter;
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
        object result = new
        {
            parseDiagnostics = check.Parse.Diagnostics,
            bindingDiagnostics = check.Bound?.Diagnostics,
            statements = check.Bound?.Statements.Select(DescribeStatement)
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object DescribeStatement(BoundStatement statement) => statement switch
    {
        BoundPipeline pipeline => new { kind = "pipeline", resultType = pipeline.ResultType?.FullName, stages = pipeline.Stages.Select(DescribeStage) },
        BoundIf conditional => new { kind = "if", conditionType = conditional.Condition.Type.FullName, thenStatements = conditional.Then.Statements.Count, elseStatements = conditional.Else?.Statements.Count ?? 0 },
        BoundForEach loop => new { kind = "forEach", variable = loop.Variable, elementType = loop.ElementType.FullName, bodyStatements = loop.Body.Statements.Count },
        _ => new { kind = statement.GetType().Name }
    };

    private static object DescribeStage(BoundStage stage) => stage switch
    {
        BoundSentence sentence => new { kind = "sentence", verb = sentence.Verb.Name, implementation = sentence.Implementation.ImplementationType.FullName, pattern = sentence.Pattern.StableId, cost = sentence.Cost, resultType = sentence.ResultType.FullName, capabilities = sentence.Implementation.Capabilities },
        BoundFilter filter => new { kind = "filter", elementType = filter.ElementType.FullName, resultType = filter.ResultType.FullName },
        BoundCheck => new { kind = "check", resultType = typeof(bool).FullName },
        _ => new { kind = stage.GetType().Name }
    };
}
