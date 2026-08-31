using FluNET.Classic.Core;
using System.Globalization;

namespace FluNET.Classic.Standard.DateTime;

public interface IClock
{
    DateTimeOffset Now { get; }
    DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
    public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) => delay <= TimeSpan.Zero ? ValueTask.CompletedTask : new ValueTask(Task.Delay(delay, cancellationToken));
}

public enum TimeZoneTarget { UTC, LOCAL }
public enum DateBoundary { START_OF_DAY, END_OF_DAY, START_OF_WEEK, END_OF_WEEK, START_OF_MONTH, END_OF_MONTH, START_OF_YEAR, END_OF_YEAR }

public sealed class TimeZoneSpec
{
    private TimeZoneSpec(TimeZoneInfo zone) => Zone = zone;
    public TimeZoneInfo Zone { get; }
    public string Id => Zone.Id;
    public static bool TryParse(string value, out TimeZoneSpec? result)
    {
        result = null;
        if (value.Equals("UTC", StringComparison.OrdinalIgnoreCase) || value.Equals("LOCAL", StringComparison.OrdinalIgnoreCase)) return false;
        try { result = new(TimeZoneInfo.FindSystemTimeZoneById(value)); return true; } catch (TimeZoneNotFoundException) { return false; } catch (InvalidTimeZoneException) { return false; }
    }
    public override string ToString() => Id;
}

public sealed record DateRange(DateTimeOffset Start, DateTimeOffset End) : IValidState
{
    public bool IsValid => Start <= End;
    public TimeSpan Duration => End - Start;
    public bool Contains(DateTimeOffset value) => IsValid && value >= Start && value <= End;
}

public sealed class DateTimeModule : LanguageModule
{
    public override string Name => "datetime";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:now", "NOW", typeof(DateTimeOffset)), new("qualifier:today", "TODAY", typeof(DateOnly)), new("qualifier:date", "DATE", typeof(DateOnly)),
        new("qualifier:time", "TIME", typeof(TimeOnly)), new("qualifier:datetime", "DATETIME", typeof(DateTimeOffset)), new("qualifier:duration", "DURATION", typeof(TimeSpan)),
        new("qualifier:date-range", "RANGE", typeof(DateRange)), new("qualifier:range-start", "START", typeof(DateTimeOffset)), new("qualifier:range-end", "END", typeof(DateTimeOffset))
    };
}

[Verb("GET"), Qualifier("NOW")]
public sealed class GetNow : IVerb<DateTimeOffset>, IGet, IWhat<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    private readonly IClock _clock; public GetNow([What] DateTimeOffset what, [FromServices] IClock clock) => _clock = clock;
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_clock.Now);
}

[Verb("GET"), Qualifier("TODAY")]
public sealed class GetToday : IVerb<DateOnly>, IGet, IWhat<DateOnly>, IPipelineProducer<DateOnly>
{
    private readonly IClock _clock; public GetToday([What] DateOnly what, [FromServices] IClock clock) => _clock = clock;
    public ValueTask<DateOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_clock.Today);
}

[Verb("CREATE"), Qualifier("RANGE"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class CreateDateRange : IVerb<DateRange>, ICreate, IFrom<DateTimeOffset>, ITo<DateTimeOffset>, IPipelineProducer<DateRange>
{
    private readonly DateTimeOffset _start; private readonly DateTimeOffset _end;
    public CreateDateRange([From] DateTimeOffset start, [To] DateTimeOffset end) { _start = start; _end = end; }
    public ValueTask<DateRange> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new DateRange(_start, _end));
}

[Verb("GET"), Qualifier("START"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRangeStart : IVerb<DateTimeOffset>, IGet, IFrom<DateRange>, IPipelineConsumer<DateRange>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateRange _range; public GetRangeStart([From] DateRange range) => _range = range;
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_range.Start);
}

[Verb("GET"), Qualifier("END"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRangeEnd : IVerb<DateTimeOffset>, IGet, IFrom<DateRange>, IPipelineConsumer<DateRange>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateRange _range; public GetRangeEnd([From] DateRange range) => _range = range;
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_range.End);
}

[Verb("PARSE"), Qualifier("DATE"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDate : IVerb<DateOnly>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<DateOnly>
{
    private readonly string _text; public ParseDate([From] string text) => _text = text;
    public ValueTask<DateOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(DateOnly.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("PARSE"), Qualifier("TIME"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseTime : IVerb<TimeOnly>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<TimeOnly>
{
    private readonly string _text; public ParseTime([From] string text) => _text = text;
    public ValueTask<TimeOnly> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(TimeOnly.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("PARSE"), Qualifier("DATETIME"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDateTime : IVerb<DateTimeOffset>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<DateTimeOffset>
{
    private readonly string _text; public ParseDateTime([From] string text) => _text = text;
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(DateTimeOffset.Parse(_text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}

[Verb("PARSE"), Qualifier("DURATION"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ParseDuration : IVerb<TimeSpan>, IParse, IFrom<string>, IPipelineConsumer<string>, IPipelineProducer<TimeSpan>
{
    private readonly string _text; public ParseDuration([From] string text) => _text = text;
    public ValueTask<TimeSpan> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(TimeSpan.Parse(_text, CultureInfo.InvariantCulture));
}

[Verb("FORMAT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatDate : IVerb<string>, IFormat, IWhat<DateOnly>, IUsing<string>, IPipelineConsumer<DateOnly>, IPipelineProducer<string>
{
    private readonly DateOnly _value; private readonly string _format; public FormatDate([What] DateOnly value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[Verb("FORMAT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatTime : IVerb<string>, IFormat, IWhat<TimeOnly>, IUsing<string>, IPipelineConsumer<TimeOnly>, IPipelineProducer<string>
{
    private readonly TimeOnly _value; private readonly string _format; public FormatTime([What] TimeOnly value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[Verb("FORMAT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatDateTime : IVerb<string>, IFormat, IWhat<DateTimeOffset>, IUsing<string>, IPipelineConsumer<DateTimeOffset>, IPipelineProducer<string>
{
    private readonly DateTimeOffset _value; private readonly string _format; public FormatDateTime([What] DateTimeOffset value, [Using] string format) { _value = value; _format = format; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value.ToString(_format, CultureInfo.InvariantCulture));
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformDateTimeZone : TransformTo<DateTimeOffset, DateTimeOffset, TimeZoneTarget>
{
    public TransformDateTimeZone([What] DateTimeOffset what, [To] TimeZoneTarget to) : base(what, to) { }
    protected override ValueTask<DateTimeOffset> TransformAsync(DateTimeOffset what, TimeZoneTarget to, CancellationToken cancellationToken) => ValueTask.FromResult(to == TimeZoneTarget.UTC ? what.ToUniversalTime() : what.ToLocalTime());
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformDateTimeNamedZone : TransformTo<DateTimeOffset, DateTimeOffset, TimeZoneSpec>
{
    public TransformDateTimeNamedZone([What] DateTimeOffset what, [To] TimeZoneSpec to) : base(what, to) { }
    protected override ValueTask<DateTimeOffset> TransformAsync(DateTimeOffset what, TimeZoneSpec to, CancellationToken cancellationToken) => ValueTask.FromResult(TimeZoneInfo.ConvertTime(what, to.Zone));
}

[Verb("TRANSFORM"), Qualifier("DATETIME"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformDateTimeByDuration : IVerb<DateTimeOffset>, ITransform, IWhat<DateTimeOffset>, IUsing<TimeSpan>, IPipelineConsumer<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateTimeOffset _value; private readonly TimeSpan _duration; public TransformDateTimeByDuration([What] DateTimeOffset value, [Using] TimeSpan duration) { _value = value; _duration = duration; }
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_value + _duration);
}

[Verb("TRANSFORM"), Qualifier("DATETIME"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformDateBoundary : IVerb<DateTimeOffset>, ITransform, IWhat<DateTimeOffset>, IUsing<DateBoundary>, IPipelineConsumer<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateTimeOffset _value; private readonly DateBoundary _boundary; public TransformDateBoundary([What] DateTimeOffset value, [Using] DateBoundary boundary) { _value = value; _boundary = boundary; }
    public ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        DateTimeOffset startOfDay = new(_value.Year, _value.Month, _value.Day, 0, 0, 0, _value.Offset);
        int mondayOffset = ((int)startOfDay.DayOfWeek + 6) % 7;
        DateTimeOffset result = _boundary switch
        {
            DateBoundary.START_OF_DAY => startOfDay,
            DateBoundary.END_OF_DAY => startOfDay.AddDays(1).AddTicks(-1),
            DateBoundary.START_OF_WEEK => startOfDay.AddDays(-mondayOffset),
            DateBoundary.END_OF_WEEK => startOfDay.AddDays(-mondayOffset + 7).AddTicks(-1),
            DateBoundary.START_OF_MONTH => new(_value.Year, _value.Month, 1, 0, 0, 0, _value.Offset),
            DateBoundary.END_OF_MONTH => new DateTimeOffset(_value.Year, _value.Month, 1, 0, 0, 0, _value.Offset).AddMonths(1).AddTicks(-1),
            DateBoundary.START_OF_YEAR => new(_value.Year, 1, 1, 0, 0, 0, _value.Offset),
            DateBoundary.END_OF_YEAR => new DateTimeOffset(_value.Year, 1, 1, 0, 0, 0, _value.Offset).AddYears(1).AddTicks(-1),
            _ => _value
        };
        return ValueTask.FromResult(result);
    }
}

[Verb("GET"), Qualifier("DURATION"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRangeDuration : IVerb<TimeSpan>, IGet, IFrom<DateRange>, IPipelineConsumer<DateRange>, IPipelineProducer<TimeSpan>
{
    private readonly DateRange _range; public GetRangeDuration([From] DateRange range) => _range = range;
    public ValueTask<TimeSpan> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_range.Duration);
}

[Verb("WAIT"), ExecutionTrait(ExecutionTrait.LongRunning)]
public sealed class WaitUntil : IVerb<DateTimeOffset>, IWait, IUntil<DateTimeOffset>, IPipelineProducer<DateTimeOffset>
{
    private readonly DateTimeOffset _deadline; private readonly IClock _clock; public WaitUntil([Until] DateTimeOffset deadline, [FromServices] IClock clock) { _deadline = deadline; _clock = clock; }
    public async ValueTask<DateTimeOffset> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) { TimeSpan delay = _deadline - _clock.Now; if (delay > TimeSpan.Zero) await _clock.DelayAsync(delay, cancellationToken).ConfigureAwait(false); return _deadline; }
}
