namespace FluNET.Classic.Core;

[Verb("GET")]
[Alias("FETCH")]
[Alias("RETRIEVE")]
public abstract class Get<TResult, TFrom> : IVerb<TResult>, IGet, IWhat<TResult>, IFrom<TFrom>
{
    protected Get(TResult what, TFrom from)
    {
        From = from;
    }

    protected TFrom From { get; }

    protected abstract ValueTask<TResult> ActAsync(TFrom from, CancellationToken cancellationToken);

    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ActAsync(From, cancellationToken);
}

[Verb("LOAD")]
public abstract class Load<TResult, TFrom> : IVerb<TResult>, ILoad, IWhat<TResult>, IFrom<TFrom>
{
    protected Load(TResult what, TFrom from) => From = from;
    protected TFrom From { get; }
    protected abstract ValueTask<TResult> ActAsync(TFrom from, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ActAsync(From, cancellationToken);
}

[Verb("SAVE")]
public abstract class Save<TWhat, TTo> : IVerb<TWhat>, ISave, IWhat<TWhat>, ITo<TTo>
{
    protected Save(TWhat what, TTo to)
    {
        What = what;
        To = to;
    }

    protected TWhat What { get; }
    protected TTo To { get; }
    protected abstract ValueTask SaveAsync(TWhat what, TTo to, CancellationToken cancellationToken);

    public async ValueTask<TWhat> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await SaveAsync(What, To, cancellationToken).ConfigureAwait(false);
        return What;
    }
}

[Verb("DELETE")]
public abstract class Delete<TFrom> : IVerb<bool>, IDelete, IFrom<TFrom>
{
    protected Delete(TFrom from) => From = from;
    protected TFrom From { get; }
    protected abstract ValueTask<bool> DeleteAsync(TFrom from, CancellationToken cancellationToken);
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => DeleteAsync(From, cancellationToken);
}

[Verb("TRANSFORM")]
public abstract class Transform<TResult, TWhat, TUsing> : IVerb<TResult>, ITransform, IWhat<TWhat>, IUsing<TUsing>
{
    protected Transform(TWhat what, TUsing @using)
    {
        What = what;
        Using = @using;
    }

    protected TWhat What { get; }
    protected TUsing Using { get; }
    protected abstract ValueTask<TResult> TransformAsync(TWhat what, TUsing @using, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => TransformAsync(What, Using, cancellationToken);
}

[Verb("SAY")]
public abstract class Say<TWhat> : IVerb<TWhat>, ISay, IWhat<TWhat>
{
    protected Say(TWhat what) => What = what;
    protected TWhat What { get; }
    protected abstract ValueTask SayAsync(TWhat what, CancellationToken cancellationToken);

    public async ValueTask<TWhat> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await SayAsync(What, cancellationToken).ConfigureAwait(false);
        return What;
    }
}
