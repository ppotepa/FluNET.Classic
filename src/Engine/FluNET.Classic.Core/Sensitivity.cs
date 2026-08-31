namespace FluNET.Classic.Core;

public static class SensitiveValueMetadata
{
    public static bool IsSensitiveType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        Type effective = Nullable.GetUnderlyingType(type) ?? type;
        if (typeof(ISensitiveValue).IsAssignableFrom(effective))
            return true;
        Type? element = ClrTypeShape.GetElementType(effective);
        return element is not null && element != effective && IsSensitiveType(element);
    }

    public static bool IsSensitiveValue(object? value) => value is ISensitiveValue;
}
