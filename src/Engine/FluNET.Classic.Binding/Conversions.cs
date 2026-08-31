using System.Globalization;

namespace FluNET.Classic.Binding;

public enum ConversionKind
{
    Exact, Assignable, Registered, Numeric, Resolution
}
public enum ConversionSafety
{
    Lossless, PotentiallyLossy
}
public enum ConversionPlanningStatus
{
    Success, NotFound, Ambiguous
}

public sealed record ConversionResult(object? Value, ConversionKind Kind, int Cost, ConversionSafety Safety = ConversionSafety.Lossless);
public sealed record ConversionStep(Type SourceType, Type TargetType, ConversionKind Kind, int Cost, ConversionSafety Safety = ConversionSafety.Lossless, string? ConverterId = null);
public sealed record ConversionPlan(Type SourceType, Type TargetType, IReadOnlyList<ConversionStep> Steps, int Cost)
{
    public ConversionKind Kind => Steps.Count == 0 ? ConversionKind.Exact : Steps.Count == 1 ? Steps[0].Kind : ConversionKind.Registered;
    public ConversionSafety Safety => Steps.Any(x => x.Safety == ConversionSafety.PotentiallyLossy) ? ConversionSafety.PotentiallyLossy : ConversionSafety.Lossless;
    public string Signature => string.Join(" -> ", new[] { SourceType }.Concat(Steps.Select(x => x.TargetType)).Select(TypeName));
    private static string TypeName(Type type) => type.FullName ?? type.Name;
}
public sealed record ConversionPlanningResult(ConversionPlanningStatus Status, ConversionPlan? Plan, IReadOnlyList<ConversionPlan> Alternatives)
{
    public bool Success => Status == ConversionPlanningStatus.Success && Plan is not null;
}

public interface IValueConverter
{
    Type SourceType
    {
        get;
    }
    Type TargetType
    {
        get;
    }
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
        if (value is TSource source && TryConvert(source, out TTarget? converted))
        {
            result = converted;
            return true;
        }
        result = null;
        return false;
    }
}

public sealed class ValueConversionRegistry
{
    private readonly Dictionary<(Type Source, Type Target), List<ConverterEntry>> _converters = new();
    private readonly Dictionary<string, ConverterEntry> _byId = new(StringComparer.Ordinal);
    private long _registrationSequence;

    public int MaxPathLength { get; set; } = 4;
    public int MaxPathCost { get; set; } = 16;

    public void Register(IValueConverter converter, int priority = 0, ConversionSafety safety = ConversionSafety.Lossless)
    {
        ArgumentNullException.ThrowIfNull(converter);
        Type source = Normalize(converter.SourceType);
        Type target = Normalize(converter.TargetType);
        string id = $"{converter.GetType().AssemblyQualifiedName ?? converter.GetType().FullName ?? converter.GetType().Name}#{Interlocked.Increment(ref _registrationSequence)}";
        var entry = new ConverterEntry(id, converter, source, target, priority, safety);
        if (!_converters.TryGetValue((source, target), out List<ConverterEntry>? entries))
            _converters[(source, target)] = entries = [];
        entries.Add(entry);
        _byId[id] = entry;
    }

    public bool CanConvert(Type source, Type target, out ConversionKind kind, out int cost)
    {
        ConversionPlanningResult result = Plan(source, target);
        if (result.Success)
        {
            kind = result.Plan!.Kind;
            cost = result.Plan.Cost;
            return true;
        }
        kind = default;
        cost = int.MaxValue;
        return false;
    }

    public bool TryPlan(Type source, Type target, out ConversionPlan? plan)
    {
        ConversionPlanningResult result = Plan(source, target);
        plan = result.Plan;
        return result.Success;
    }

    public ConversionPlanningResult Plan(Type source, Type target)
    {
        Type s = Normalize(source);
        Type t = Normalize(target);
        if (s == t)
            return Success(new ConversionPlan(s, t, Array.Empty<ConversionStep>(), 0));

        var queue = new PriorityQueue<PathState, int>();
        queue.Enqueue(new PathState(s, Array.Empty<ConversionStep>(), 0), 0);
        var best = new Dictionary<Type, int> { [s] = 0 };
        var seenPaths = new HashSet<string>(StringComparer.Ordinal) { PathSignature(s, Array.Empty<ConversionStep>()) };
        var completed = new List<ConversionPlan>();
        int bestTargetCost = int.MaxValue;

        while (queue.TryDequeue(out PathState? state, out int priority))
        {
            if (priority > bestTargetCost || state.Cost > MaxPathCost)
                break;
            if (state.Type == t)
            {
                bestTargetCost = Math.Min(bestTargetCost, state.Cost);
                if (state.Cost == bestTargetCost)
                    completed.Add(new(s, t, state.Steps, state.Cost));
                continue;
            }
            if (state.Steps.Count >= MaxPathLength)
                continue;

            foreach (ConversionStep edge in EdgesFrom(state.Type, t))
            {
                int nextCost = state.Cost + edge.Cost;
                if (nextCost > MaxPathCost || nextCost > bestTargetCost)
                    continue;
                Type nextType = edge.TargetType;
                if (best.TryGetValue(nextType, out int known) && known < nextCost)
                    continue;
                if (!best.TryGetValue(nextType, out known) || nextCost < known)
                    best[nextType] = nextCost;
                ConversionStep[] steps = state.Steps.Append(edge).ToArray();
                string signature = PathSignature(s, steps);
                if (!seenPaths.Add(signature))
                    continue;
                queue.Enqueue(new PathState(nextType, steps, nextCost), nextCost);
            }
        }

        ConversionPlan[] shortest = completed
            .Where(x => x.Cost == bestTargetCost)
            .GroupBy(x => PlanSignature(x), StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => PlanSignature(x), StringComparer.Ordinal)
            .ToArray();
        if (shortest.Length == 0)
            return new(ConversionPlanningStatus.NotFound, null, Array.Empty<ConversionPlan>());
        if (shortest.Length > 1)
            return new(ConversionPlanningStatus.Ambiguous, null, shortest);
        return Success(shortest[0]);
    }

    public bool TryConvert(object? source, Type target, out ConversionResult? result)
    {
        if (source is null)
        {
            bool nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;
            result = nullable ? new(null, ConversionKind.Assignable, 1) : null;
            return nullable;
        }
        ConversionPlanningResult planning = Plan(source.GetType(), target);
        if (!planning.Success)
        {
            result = null;
            return false;
        }
        return TryConvert(source, planning.Plan!, out result);
    }

    public bool TryConvert(object? source, ConversionPlan plan, out ConversionResult? result)
    {
        if (source is null)
        {
            bool nullable = !plan.TargetType.IsValueType || Nullable.GetUnderlyingType(plan.TargetType) is not null;
            result = nullable ? new(null, ConversionKind.Assignable, 1) : null;
            return nullable;
        }

        object? current = source;
        foreach (ConversionStep step in plan.Steps)
        {
            switch (step.Kind)
            {
                case ConversionKind.Assignable:
                    break;
                case ConversionKind.Registered:
                    if (step.ConverterId is null || !_byId.TryGetValue(step.ConverterId, out ConverterEntry? entry) || !entry.Converter.TryConvert(current, out current))
                    {
                        result = null;
                        return false;
                    }
                    break;
                case ConversionKind.Numeric:
                    try
                    {
                        current = Convert.ChangeType(current, Normalize(step.TargetType), CultureInfo.InvariantCulture);
                    }
                    catch { result = null; return false; }
                    break;
            }
        }
        result = new(current, plan.Kind, plan.Cost, plan.Safety);
        return true;
    }

    private IEnumerable<ConversionStep> EdgesFrom(Type source, Type finalTarget)
    {
        Type s = Normalize(source);
        Type t = Normalize(finalTarget);

        if (t.IsAssignableFrom(s) && s != t)
            yield return new(s, t, ConversionKind.Assignable, 1);

        foreach (((Type from, Type to), List<ConverterEntry> entries) in _converters.OrderBy(x => TypeName(x.Key.Target), StringComparer.Ordinal))
        {
            if (from != s)
                continue;
            int maxPriority = entries.Max(x => x.Priority);
            foreach (ConverterEntry entry in entries.Where(x => x.Priority == maxPriority).OrderBy(x => x.Id, StringComparer.Ordinal))
                yield return new(s, to, ConversionKind.Registered, 2, entry.Safety, entry.Id);
        }

        HashSet<Type> usefulTypes = _converters.Keys.SelectMany(x => new[] { x.Source, x.Target }).Append(t).ToHashSet();
        foreach (Type candidate in usefulTypes.OrderBy(TypeName, StringComparer.Ordinal))
        {
            if (candidate == s)
                continue;
            if (candidate.IsAssignableFrom(s))
                yield return new(s, candidate, ConversionKind.Assignable, 1);
            else if (IsNumeric(s) && IsNumeric(candidate))
            {
                bool widening = IsWideningNumeric(s, candidate);
                yield return new(s, candidate, ConversionKind.Numeric, widening ? 3 : 6, widening ? ConversionSafety.Lossless : ConversionSafety.PotentiallyLossy);
            }
        }
    }

    private static bool IsWideningNumeric(Type source, Type target)
    {
        Type s = Normalize(source);
        Type t = Normalize(target);
        if (s == t)
            return true;
        if (s == typeof(float) && t == typeof(double))
            return true;
        if (IsInteger(s) && t == typeof(decimal))
            return true;
        if (!IsInteger(s) || !IsInteger(t))
            return false;
        (int Bits, bool Signed) a = IntegerShape(s);
        (int Bits, bool Signed) b = IntegerShape(t);
        if (a.Signed == b.Signed)
            return b.Bits >= a.Bits;
        if (!a.Signed && b.Signed)
            return b.Bits > a.Bits;
        return false;
    }

    private static bool IsInteger(Type type) => Type.GetTypeCode(Normalize(type)) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64;
    private static (int Bits, bool Signed) IntegerShape(Type type) => Type.GetTypeCode(Normalize(type)) switch
    {
        TypeCode.SByte => (8, true),
        TypeCode.Byte => (8, false),
        TypeCode.Int16 => (16, true),
        TypeCode.UInt16 => (16, false),
        TypeCode.Int32 => (32, true),
        TypeCode.UInt32 => (32, false),
        TypeCode.Int64 => (64, true),
        TypeCode.UInt64 => (64, false),
        _ => (0, false)
    };
    private static Type Normalize(Type type) => Nullable.GetUnderlyingType(type) ?? type;
    private static bool IsNumeric(Type type) => Type.GetTypeCode(Normalize(type)) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
    private static string TypeName(Type type) => type.FullName ?? type.Name;
    private static string PathSignature(Type source, IReadOnlyList<ConversionStep> steps) => TypeName(source) + "|" + string.Join("|", steps.Select(StepSignature));
    private static string PlanSignature(ConversionPlan plan) => PathSignature(plan.SourceType, plan.Steps);
    private static string StepSignature(ConversionStep step) => $"{TypeName(step.SourceType)}>{TypeName(step.TargetType)}:{step.Kind}:{step.ConverterId ?? "builtin"}";
    private static ConversionPlanningResult Success(ConversionPlan plan) => new(ConversionPlanningStatus.Success, plan, new[] { plan });

    private sealed record ConverterEntry(string Id, IValueConverter Converter, Type Source, Type Target, int Priority, ConversionSafety Safety);
    private sealed record PathState(Type Type, IReadOnlyList<ConversionStep> Steps, int Cost);
}
