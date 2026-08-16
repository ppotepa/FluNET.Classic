using FluNET.Keywords;

namespace FluNET.Syntax.Core;

public interface IVerb : IWord, IKeyword
{
    string[] Synonyms => Array.Empty<string>();
}

public interface IVerb<out TResult> : IVerb
{
}

public interface IVerb<TWhat, TFrom> : IVerb<TWhat>
{
    Func<TFrom, TWhat> Act { get; }

    TWhat Invoke();

    TFrom? Resolve(string value);
}

public interface IAsyncVerb<TResult> : IVerb<TResult>
{
    ValueTask<TResult> InvokeAsync(CancellationToken cancellationToken = default);
}
