using System.Globalization;

namespace FluNET.Classic.Binding;

public enum ConversionKind
{
    Exact,
    Assignable,
    Registered,
    Numeric,
    Resolution
}

public sealed record ConversionResult(object? Value, ConversionKind Kind, int Cost);

public interface IValueConverter
{
    Type SourceType { get; }
    Type TargetType { get; }
    bool TryConvert(object? value, out object? result);
}

public abstract class ValueConverter<TSource, TTarget> : IValueConverter
{
    public Type SourceType => typeof(TSource);
    public Type TargetType => typeof(TTarget);
    public abstract bool TryConvert(TSource value, out TTarget? result);
    bool IValueConverter.TryConvert(object? value, out object? result)
    {
        if (value is TSource source && TryConvert(source, out TTarget? converted)) { result = converted; return true; }
        result = null; return false;
    }
}

public sealed class ValueConversionRegistry
{
    private readonly Dictionary<(Type Source, Type Target), IValueConverter> _converters = new();
    public void Register(IValueConverter converter) => _converters[(converter.SourceType, converter.TargetType)] = converter;

    public bool CanConvert(Type source, Type target, out ConversionKind kind, out int cost)
    {
        Type effectiveTarget = Nullable.GetUnderlyingType(target) ?? target;
        Type effectiveSource = Nullable.GetUnderlyingType(source) ?? source;
        if (effectiveSource == effectiveTarget) { kind = ConversionKind.Exact; cost = 0; return true; }
        if (target.IsAssignableFrom(source) || effectiveTarget.IsAssignableFrom(effectiveSource)) { kind = ConversionKind.Assignable; cost = 1; return true; }
        if (_converters.ContainsKey((source, target)) || _converters.ContainsKey((effectiveSource, effectiveTarget))) { kind = ConversionKind.Registered; cost = 2; return true; }
        if (IsNumeric(effectiveSource) && IsNumeric(effectiveTarget)) { kind = ConversionKind.Numeric; cost = 3; return true; }
        kind = default; cost = int.MaxValue; return false;
    }

    public bool TryConvert(object? source, Type target, out ConversionResult? result)
    {
        if (source is null)
        {
            bool nullable = !target.IsValueType || Nullable.GetUnderlyingType(target) is not null;
            result = nullable ? new(null, ConversionKind.Assignable, 1) : null;
            return nullable;
        }
        Type sourceType = source.GetType();
        if (!CanConvert(sourceType, target, out ConversionKind kind, out int cost)) { result = null; return false; }
        if (kind is ConversionKind.Exact or ConversionKind.Assignable) { result = new(source, kind, cost); return true; }
        Type effectiveTarget = Nullable.GetUnderlyingType(target) ?? target;
        if (kind == ConversionKind.Registered)
        {
            IValueConverter converter = _converters.TryGetValue((sourceType, target), out IValueConverter? exact) ? exact : _converters[(sourceType, effectiveTarget)];
            if (converter.TryConvert(source, out object? converted)) { result = new(converted, kind, cost); return true; }
        }
        if (kind == ConversionKind.Numeric)
        {
            try { result = new(Convert.ChangeType(source, effectiveTarget, CultureInfo.InvariantCulture), kind, cost); return true; } catch { }
        }
        result = null; return false;
    }

    private static bool IsNumeric(Type type) => Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal;
}
