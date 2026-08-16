using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace FluNET.Binding;

public sealed class ValueResolverRegistry
{
    private readonly Dictionary<Type, IValueResolver> _resolvers = new();

    public void Register<T>(IValueResolver<T> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolvers[typeof(T)] = resolver;
    }

    public bool TryResolve(string source, Type targetType, ResolutionContext context, out object? value)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentNullException.ThrowIfNull(context);

        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (_resolvers.TryGetValue(targetType, out IValueResolver? resolver) ||
            _resolvers.TryGetValue(effectiveType, out resolver))
        {
            return resolver.TryResolve(source, context, out value);
        }

        if (effectiveType == typeof(string))
        {
            value = source;
            return true;
        }

        if (effectiveType == typeof(FileInfo))
        {
            value = new FileInfo(source);
            return true;
        }

        if (effectiveType == typeof(DirectoryInfo))
        {
            value = new DirectoryInfo(source);
            return true;
        }

        if (effectiveType == typeof(Uri))
        {
            bool success = Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out Uri? uri);
            value = uri;
            return success;
        }

        if (effectiveType.IsEnum)
        {
            bool success = Enum.TryParse(effectiveType, source, ignoreCase: true, out object? parsed);
            value = parsed;
            return success;
        }

        if (TryParse(effectiveType, source, context.FormatProvider, out value))
        {
            return true;
        }

        TypeConverter converter = TypeDescriptor.GetConverter(effectiveType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                value = converter.ConvertFrom(null, context.FormatProvider as CultureInfo ?? CultureInfo.InvariantCulture, source);
                return value is not null;
            }
            catch (Exception) when (converter is not null)
            {
            }
        }

        ConstructorInfo? stringConstructor = effectiveType.GetConstructor([typeof(string)]);
        if (stringConstructor is not null)
        {
            try
            {
                value = stringConstructor.Invoke([source]);
                return true;
            }
            catch (TargetInvocationException)
            {
            }
        }

        value = null;
        return false;
    }

    private static bool TryParse(Type targetType, string source, IFormatProvider? formatProvider, out object? value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

        MethodInfo? providerTryParse = targetType.GetMethod(
            "TryParse",
            flags,
            binder: null,
            [typeof(string), typeof(IFormatProvider), targetType.MakeByRefType()],
            modifiers: null);

        if (providerTryParse is not null)
        {
            object?[] args = [source, formatProvider ?? CultureInfo.InvariantCulture, null];
            if (providerTryParse.Invoke(null, args) is true)
            {
                value = args[2];
                return true;
            }
        }

        MethodInfo? tryParse = targetType.GetMethod(
            "TryParse",
            flags,
            binder: null,
            [typeof(string), targetType.MakeByRefType()],
            modifiers: null);

        if (tryParse is not null)
        {
            object?[] args = [source, null];
            if (tryParse.Invoke(null, args) is true)
            {
                value = args[1];
                return true;
            }
        }

        MethodInfo? parse = targetType.GetMethod(
            "Parse",
            flags,
            binder: null,
            [typeof(string)],
            modifiers: null);

        if (parse is not null)
        {
            try
            {
                value = parse.Invoke(null, [source]);
                return true;
            }
            catch (TargetInvocationException)
            {
            }
        }

        value = null;
        return false;
    }
}
