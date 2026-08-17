using FluNET.Binding;
using FluNET.Language;
using FluNET.Runtime;
using FluNET.Syntax.Parsing;

namespace FluNET;

public sealed record ClassicCompilation(
    LanguageSnapshot Language,
    ClassicParseResult Parse,
    BoundScript Bound)
{
    public bool Success => Parse.Diagnostics.Count == 0 && Bound.Diagnostics.Count == 0;
}

public sealed class ClassicEngine
{
    private readonly LanguageSnapshot _language;
    private readonly ClassicParser _parser;
    private readonly SemanticBinder _binder;
    private readonly BoundExecutor _executor;

    public ClassicEngine(
        LanguageSnapshot language,
        ClassicParser parser,
        SemanticBinder binder,
        BoundExecutor executor)
    {
        _language = language ?? throw new ArgumentNullException(nameof(language));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _binder = binder ?? throw new ArgumentNullException(nameof(binder));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public LanguageSnapshot Language => _language;

    public ClassicCompilation Compile(string source)
    {
        ClassicParseResult parse = _parser.Parse(source);
        BoundScript bound = _binder.Bind(parse.Script);
        return new ClassicCompilation(_language, parse, bound);
    }

    public async ValueTask<RuntimeResult> RunAsync(
        string source,
        RuntimeState? state = null,
        CancellationToken cancellationToken = default)
    {
        ClassicCompilation compilation = Compile(source);
        if (compilation.Parse.Diagnostics.Count > 0)
        {
            var diagnostics = compilation.Parse.Diagnostics
                .Select(d => new RuntimeDiagnostic(d.Code, d.Message))
                .ToArray();
            return new RuntimeResult(state ?? new RuntimeState(), diagnostics);
        }

        return await _executor.ExecuteAsync(compilation.Bound, state, cancellationToken)
            .ConfigureAwait(false);
    }
}
