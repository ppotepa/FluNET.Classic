using System.Globalization;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.DateTime;

public enum TimeZoneTarget { UTC, LOCAL }

public sealed class DateTimeModule : LanguageModule
{
    public override string Name => "datetime";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:now", "NOW", typeof(DateTimeOffset)),
        new("qualifier:today", "TODAY", typeof(DateOnly)),
        new("qualifier:date", "DATE", typeof(DateOnly)),
        new("qualifier:time", "TIME", typeof(TimeOnly)),
        new("qualifier:datetime", "DATETIME", typeof(DateTimeOffset)),
        new("qualifier:duration", "DURATION", typeof(TimeSpan))
    };
}

[Verb("GET")]
[Qualifier("NOW")]
public sealed class GetNow : IVerb<DateTimeOffset>, IGet, IWhat<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    public GetNow([What] DateTimeOffset what) { }
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(DateTimeOffset.Now);
}

[Verb("GET")]
[Qualifier("TODAY")]
public sealed class GetToday : IVerb<DateOnly>, IGet, IWhat<DateOnly>, IPipelineProducer<DateOnly>
{
    public GetToday([What] DateOnly what) { }
    public ValueTask<DateOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(DateOnly.FromDateTime(System.DateTime.Today));
}

[Verb("PARSE")]
[Qualifier("DATE")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDate : IVerb<DateOnly>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<DateOnly>
{
    private readonly string _text;
    public ParseDate([From] string text) => _text = text;
    public ValueTask<DateOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(DateOnly.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("PARSE")]
[Qualifier("TIME")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseTime : IVerb<TimeOnly>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<TimeOnly>
{
    private readonly string _text;
    public ParseTime([From] string text) => _text = text;
    public ValueTask<TimeOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(TimeOnly.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("PARSE")]
[Qualifier("DATETIME")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDateTime : IVerb<DateTimeOffset>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<DateTimeOffset>
{
    private readonly string _text;
    public ParseDateTime([From] string text) => _text = text;
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(DateTimeOffset.Parse(_text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}

[Verb("PARSE")]
[Qualifier("DURATION")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDuration : IVerb<TimeSpan>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<TimeSpan>
{
    private readonly string _text;
    public ParseDuration([From] string text) => _text = text;
    public ValueTask<TimeSpan> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(TimeSpan.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("FORMAT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatDate : IVerb<string>, IFormat, IWhat<DateOnly>, IUsing<string>, IPipelineConsumer<DateOnly>, IPipelineProducer<string>
{
    private readonly DateOnly _value;
    private readonly string _format;
    public FormatDate([What] DateOnly value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[Verb("FORMAT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatTime : IVerb<string>, IFormat, IWhat<TimeOnly>, IUsing<string>, IPipelineConsumer<TimeOnly>, IPipelineProducer<string>
{
    private readonly TimeOnly _value;
    private readonly string _format;
    public FormatTime([What] TimeOnly value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[Verb("FORMAT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatDateTime : IVerb<string>, IFormat, IWhat<DateTimeOffset>, IUsing<string>, IPipelineConsumer<DateTimeOffset>, IPipelineProducer<string>
{
    private readonly DateTimeOffset _value;
    private readonly string _format;
    public FormatDateTime([What] DateTimeOffset value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformDateTimeZone : TransformTo<DateTimeOffset, DateTimeOffset, TimeZoneTarget>
{
    public TransformDateTimeZone([What] DateTimeOffset what, [To] TimeZoneTarget to) : base(what, to) { }
    protected override ValueTask<DateTimeOffset> TransformAsync(DateTimeOffset what, TimeZoneTarget to, CancellationToken cancellationToken) =>
        ValueTask.FromResult(to == TimeZoneTarget.UTC ? what.ToUniversalTime() : what.ToLocalTime());
}

[Verb("WAIT")]
[ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class WaitUntil : IVerb<DateTimeOffset>, IWait, IUntil<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateTimeOffset _deadline;
    public WaitUntil([Until] DateTimeOffset deadline) => _deadline = deadline;
    public async ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        TimeSpan delay = _deadline - DateTimeOffset.Now;
        if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        return _deadline;
    }
}
