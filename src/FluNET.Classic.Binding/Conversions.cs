using System.Globalization;

namespace FluNET.Classic.Binding;

public enum ConversionKind { Exact, Assignable, Registered, Numeric, Resolution }
public sealed record ConversionResult(object? Value, ConversionKind Kind, int Cost);
public sealed record ConversionStep(Type SourceType, Type TargetType, ConversionKind Kind, int Cost);
public sealed record ConversionPlan(Type SourceType, Type TargetType, IReadOnlyList<ConversionStep> Steps, int Cost)
{
    public ConversionKind Kind => Steps.Count == 0 ? ConversionKind.Exact : Steps.Count == 1 ? Steps[0].Kind : ConversionKind.Registered;
}

public interface IValueConverter
{
    Type SourceType { get; }
    Type TargetType { get; }
    bool TryConvert(object? value, out object? result);
}

public interface IValueConverter<TSource, TTarget> : IValueConverter
{
    bool TryConvert(TSource value, out TTarget? result);
}

public abstract class ValueConverter<TSource, TTarget> : IValueConverter<TSource, TTarget>
{
    public Type SourceType => typeof(TSource);
    public Type TargetType => typeof(TTarget);
    public abstract bool TryConvert(TSource value, out TTarget? result);
    bool IValueConverter.TryConvert(object? value, out object? result)
    {
        if (value is TSource source && TryConvert(source, out TTarget? converted)) { result = converted; return true; }
        result = null;
        return false;
    }
}

public sealed class ValueConversionRegistry
{
    private readonly Dictionary<(Type Source, Type Target), IValueConverter> _converters = new();
    public int MaxPathLength { get; set; } = 4;
    public int MaxPathCost { get; set; } = 12;

    public void Register(IValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters[(Normalize(converter.SourceType), Normalize(converter.TargetType))] = converter;
    }

    public bool CanConvert(Type source, Type target, out ConversionKind kind, out int cost)
    {
        if (TryPlan(source, target, out ConversionPlan? plan))
        {
            kind = plan!.Kind;
            cost = plan.Cost;
            return true;
        }
        kind = default;
        cost = int.MaxValue;
        return false;
    }

    public bool TryPlan(Type source, Type target, out ConversionPlan? plan)
    {
        Type s = Normalize(source);
        Type t = Normalize(target);
        if (s == t) { plan = new(s, t, Array.Empty<ConversionStep>(), 0); return true; }
        if (t.IsAssignableFrom(s)) { plan = new(s, t, new[] { new ConversionStep(s, t, ConversionKind.Assignable, 1) }, 1); return true; }
        if (_converters.ContainsKey((s, t))) { plan = new(s, t, new[] { new ConversionStep(s, t, ConversionKind.Registered, 2) }, 2); return true; }
        if (IsNumeric(s) && IsNumeric(t)) { plan = new(s, t, new[] { new ConversionStep(s, t, ConversionKind.Numeric, 3) }, 3); return true; }

        var queue = new PriorityQueue<PathState, int>();
        queue.Enqueue(new PathState(s, Array.Empty<ConversionStep>(), 0), 0);
        var best = new Dictionary<Type, int> { [s] = 0 };

        while (queue.TryDequeue(out PathState? state, out _))
        {
            if (state.Steps.Count >= MaxPathLength) continue;
            foreach (((Type from, Type to), _) in _converters)
            {
                if (from != state.Type) continue;
                int nextCost = state.Cost + 2;
                if (nextCost > MaxPathCost || (best.TryGetValue(to, out int known) && known <= nextCost)) continue;
                ConversionStep[] steps = state.Steps.Append(new ConversionStep(from, to, ConversionKind.Registered, 2)).ToArray();
                if (to == t || t.IsAssignableFrom(to))
                {
                    if (to != t) steps = steps.Append(new ConversionStep(to, t, ConversionKind.Assignable, 1)).ToArray();
                    int total = steps.Sum(x => x.Cost);
                    plan = new(s, t, steps, total);
                    return true;
                }
                if (IsNumeric(to) && IsNumeric(t))
                {
                    steps = steps.Append(new ConversionStep(to, t, ConversionKind.Numeric, 3)).ToArray();
                    int total = steps.Sum(x => x.Cost);
                    if (total <= MaxPathCost) { plan = new(s, t, steps, total); return true; }
                }
                best[to] = nextCost;
                queue.Enqueue(new PathState(to, steps, nextCost), nextCost);
            }
        }

        plan = null;
        return false;
    }

    public bool TryConvert(object? source, Type target, out ConversionResult? result)
    {
        if (source is null)
        {
            bool nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;
            result = nullable ? new(null, ConversionKind.Assignable, 1) : null;
            return nullable;
        }

        if (!TryPlan(source.GetType(), target, out ConversionPlan? plan)) { result = null; return false; }
        object? current = source;
        foreach (ConversionStep step in plan!.Steps)
        {
            switch (step.Kind)
            {
                case ConversionKind.Assignable:
                    break;
                case ConversionKind.Registered:
                    if (!_converters.TryGetValue((Normalize(step.SourceType), Normalize(step.TargetType)), out IValueConverter? converter) || !converter.TryConvert(current, out current))
                    { result = null; return false; }
                    break;
                case ConversionKind.Numeric:
                    try { current = Convert.ChangeType(current, Normalize(step.TargetType), CultureInfo.InvariantCulture); }
                    catch { result = null; return false; }
                    break;
            }
        }
        result = new(current, plan.Kind, plan.Cost);
        return true;
    }

    private static Type Normalize(Type type) => Nullable.GetUnderlyingType(type) ?? type;
    private static bool IsNumeric(Type type) => Type.GetTypeCode(Normalize(type)) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    private sealed record PathState(Type Type, IReadOnlyList<ConversionStep> Steps, int Cost);
}
