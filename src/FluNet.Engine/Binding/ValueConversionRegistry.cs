using System.Globalization;

namespace FluNET.Binding;

public enum ConversionKind
{
    Exact,
    Assignable,
    Registered,
    Numeric,
    Resolution
}

public sealed record ConversionResult(
    object? Value,
    ConversionKind Kind,
    int Cost);

public interface IValueConverter
{
    Type SourceType { get; }
    Type TargetType { get; }
    bool TryConvert(object? source, out object? value);
}

public interface IValueConverter<TSource, TTarget> : IValueConverter
{
    bool TryConvert(TSource source, out TTarget? value);
}

public sealed class ValueConversionRegistry
{
    private readonly Dictionary<(Type Source, Type Target), IValueConverter> _converters = new();

    public void Register(IValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converters[(converter.SourceType, converter.TargetType)] = converter;
    }

    public bool TryConvert(object? source, Type targetType, out ConversionResult? result)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        if (source is null)
        {
            bool nullable = !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
            result = nullable ? new ConversionResult(null, ConversionKind.Assignable, 1) : null;
            return nullable;
        }

        Type sourceType = source.GetType();
        Type effectiveTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (sourceType == targetType || sourceType == effectiveTarget)
        {
            result = new ConversionResult(source, ConversionKind.Exact, 0);
            return true;
        }

        if (targetType.IsAssignableFrom(sourceType) || effectiveTarget.IsAssignableFrom(sourceType))
        {
            result = new ConversionResult(source, ConversionKind.Assignable, 1);
            return true;
        }

        if (_converters.TryGetValue((sourceType, targetType), out IValueConverter? converter) ||
            _converters.TryGetValue((sourceType, effectiveTarget), out converter))
        {
            if (converter.TryConvert(source, out object? converted))
            {
                result = new ConversionResult(converted, ConversionKind.Registered, 2);
                return true;
            }
        }

        if (IsNumeric(sourceType) && IsNumeric(effectiveTarget))
        {
            try
            {
                object converted = Convert.ChangeType(source, effectiveTarget, CultureInfo.InvariantCulture);
                result = new ConversionResult(converted, ConversionKind.Numeric, 3);
                return true;
            }
            catch (Exception) when (source is IConvertible)
            {
            }
        }

        result = null;
        return false;
    }

    private static bool IsNumeric(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return Type.GetTypeCode(type) is
            TypeCode.Byte or TypeCode.SByte or
            TypeCode.Int16 or TypeCode.UInt16 or
            TypeCode.Int32 or TypeCode.UInt32 or
            TypeCode.Int64 or TypeCode.UInt64 or
            TypeCode.Single or TypeCode.Double or
            TypeCode.Decimal;
    }
}
