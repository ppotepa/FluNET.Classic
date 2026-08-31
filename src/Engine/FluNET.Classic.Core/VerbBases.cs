namespace FluNET.Classic.Core;

[Verb("GET")]
[Alias("FETCH")]
[Alias("RETRIEVE")]
public abstract class Get<TResult, TFrom> : IVerb<TResult>, IGet, IWhat<TResult>, IFrom<TFrom>, IPipelineProducer<TResult>
{
    protected Get(TResult what, TFrom from) => From = from;
    protected TFrom From
    {
        get;
    }
    protected abstract ValueTask<TResult> ActAsync(TFrom from, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ActAsync(From, cancellationToken);
}

[Verb("LOAD")]
public abstract class Load<TResult, TFrom> : IVerb<TResult>, ILoad, IWhat<TResult>, IFrom<TFrom>, IPipelineProducer<TResult>
{
    protected Load(TResult what, TFrom from) => From = from;
    protected TFrom From
    {
        get;
    }
    protected abstract ValueTask<TResult> ActAsync(TFrom from, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ActAsync(From, cancellationToken);
}

[Verb("SAVE")]
public abstract class Save<TWhat, TTo> : IVerb<TWhat>, ISave, IWhat<TWhat>, ITo<TTo>, IPipelineConsumer<TWhat>, IPipelineProducer<TWhat>
{
    protected Save(TWhat what, TTo to)
    {
        What = what;
        To = to;
    }
    protected TWhat What
    {
        get;
    }
    protected TTo To
    {
        get;
    }
    protected abstract ValueTask SaveAsync(TWhat what, TTo to, CancellationToken cancellationToken);
    public async ValueTask<TWhat> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await SaveAsync(What, To, cancellationToken).ConfigureAwait(false);
        return What;
    }
}

[Verb("DELETE")]
public abstract class Delete<TFrom> : IVerb<bool>, IDelete, IFrom<TFrom>, IPipelineProducer<bool>
{
    protected Delete(TFrom from) => From = from;
    protected TFrom From
    {
        get;
    }
    protected abstract ValueTask<bool> DeleteAsync(TFrom from, CancellationToken cancellationToken);
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => DeleteAsync(From, cancellationToken);
}

[Verb("TRANSFORM")]
public abstract class Transform<TResult, TWhat, TUsing> : IVerb<TResult>, ITransform, IWhat<TWhat>, IUsing<TUsing>, IPipelineConsumer<TWhat>, IPipelineProducer<TResult>
{
    protected Transform(TWhat what, TUsing @using)
    {
        What = what;
        Using = @using;
    }
    protected TWhat What
    {
        get;
    }
    protected TUsing Using
    {
        get;
    }
    protected abstract ValueTask<TResult> TransformAsync(TWhat what, TUsing @using, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => TransformAsync(What, Using, cancellationToken);
}

[Verb("TRANSFORM")]
public abstract class TransformTo<TResult, TWhat, TTo> : IVerb<TResult>, ITransform, IWhat<TWhat>, ITo<TTo>, IPipelineConsumer<TWhat>, IPipelineProducer<TResult>
{
    protected TransformTo(TWhat what, TTo to)
    {
        What = what;
        To = to;
    }
    protected TWhat What
    {
        get;
    }
    protected TTo To
    {
        get;
    }
    protected abstract ValueTask<TResult> TransformAsync(TWhat what, TTo to, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => TransformAsync(What, To, cancellationToken);
}

[Verb("TRANSFORM")]
public abstract class TransformToUsing<TResult, TWhat, TTo, TUsing> : IVerb<TResult>, ITransform, IWhat<TWhat>, ITo<TTo>, IUsing<TUsing>, IPipelineConsumer<TWhat>, IPipelineProducer<TResult>
{
    protected TransformToUsing(TWhat what, TTo to, TUsing @using)
    {
        What = what;
        To = to;
        Using = @using;
    }
    protected TWhat What
    {
        get;
    }
    protected TTo To
    {
        get;
    }
    protected TUsing Using
    {
        get;
    }
    protected abstract ValueTask<TResult> TransformAsync(TWhat what, TTo to, TUsing @using, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => TransformAsync(What, To, Using, cancellationToken);
}

[Verb("PARSE")]
public abstract class Parse<TResult, TWhat> : IVerb<TResult>, IParse, IWhat<TWhat>, IPipelineConsumer<TWhat>, IPipelineProducer<TResult>
{
    protected Parse(TWhat what) => What = what;
    protected TWhat What
    {
        get;
    }
    protected abstract ValueTask<TResult> ParseAsync(TWhat what, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ParseAsync(What, cancellationToken);
}

[Verb("FORMAT")]
public abstract class Format<TResult, TWhat, TAs> : IVerb<TResult>, IFormat, IWhat<TWhat>, IAs<TAs>, IPipelineConsumer<TWhat>, IPipelineProducer<TResult>
{
    protected Format(TWhat what, TAs @as)
    {
        What = what;
        As = @as;
    }
    protected TWhat What
    {
        get;
    }
    protected TAs As
    {
        get;
    }
    protected abstract ValueTask<TResult> FormatAsync(TWhat what, TAs @as, CancellationToken cancellationToken);
    public ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => FormatAsync(What, As, cancellationToken);
}

[Verb("SAY")]
public abstract class Say<TWhat> : IVerb<TWhat>, ISay, IWhat<TWhat>, IPipelineConsumer<TWhat>, IPipelineProducer<TWhat>
{
    protected Say(TWhat what) => What = what;
    protected TWhat What
    {
        get;
    }
    protected abstract ValueTask SayAsync(TWhat what, CancellationToken cancellationToken);
    public async ValueTask<TWhat> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await SayAsync(What, cancellationToken).ConfigureAwait(false);
        return What;
    }
}
