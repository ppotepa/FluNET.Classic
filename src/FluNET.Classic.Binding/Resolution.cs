using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using FluNET.Classic.Core;

namespace FluNET.Classic.Binding;

public sealed record ResolutionContext(
    Type ExpectedType,
    string? RoleName = null,
    string? VerbName = null,
    string? Qualifier = null,
    IServiceProvider? Services = null,
    IFormatProvider? FormatProvider = null,
    IReadOnlyDictionary<string, object?>? Variables = null);

public interface IValueResolver
{
    Type TargetType { get; }
    bool TryResolve(string source, ResolutionContext context, out object? value);
}

public interface IValueResolver<T> : IValueResolver
{
    bool TryResolve(string source, ResolutionContext context, out T? value);
}

public sealed class ValueResolverRegistry
{
    private readonly Dictionary<Type, IValueResolver> _resolvers = new();

    public void Register<T>(IValueResolver<T> resolver) => _resolvers[typeof(T)] = resolver;

    public bool TryResolve(string source, Type targetType, ResolutionContext context, out object? value)
    {
        Type effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (_resolvers.TryGetValue(targetType, out IValueResolver? resolver) || _resolvers.TryGetValue(effective, out resolver))
            return resolver.TryResolve(source, context, out value);

        if (TryResolveCollection(source, targetType, context, out value)) return true;
        if (effective == typeof(string)) { value = source; return true; }
        if (effective == typeof(FileInfo)) { value = new FileInfo(source); return true; }
        if (effective == typeof(DirectoryInfo)) { value = new DirectoryInfo(source); return true; }
        if (effective == typeof(Uri)) { bool ok = Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out Uri? uri); value = uri; return ok; }
        if (effective.IsEnum) { bool ok = Enum.TryParse(effective, source, true, out object? parsed); value = parsed; return ok; }
        if (TryParse(effective, source, context.FormatProvider ?? CultureInfo.InvariantCulture, out value)) return true;

        TypeConverter converter = TypeDescriptor.GetConverter(effective);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try { value = converter.ConvertFrom(null, context.FormatProvider as CultureInfo ?? CultureInfo.InvariantCulture, source); return value is not null; }
            catch { }
        }

        ConstructorInfo? ctor = effective.GetConstructor(new[] { typeof(string) });
        if (ctor is not null)
        {
            try { value = ctor.Invoke(new object?[] { source }); return true; }
            catch { }
        }

        value = null;
        return false;
    }

    private bool TryResolveCollection(string source, Type targetType, ResolutionContext context, out object? value)
    {
        Type? elementType = ClrTypeShape.GetElementType(targetType);
        if (elementType is null) { value = null; return false; }
        string[] parts = source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var items = new object?[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!TryResolve(parts[i], elementType, context with { ExpectedType = elementType }, out items[i])) { value = null; return false; }
        }
        Array array = Array.CreateInstance(elementType, items.Length);
        for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
        if (targetType.IsArray || targetType.IsAssignableFrom(array.GetType())) { value = array; return true; }
        if (targetType.IsGenericType)
        {
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList list = (IList)Activator.CreateInstance(listType)!;
            foreach (object? item in items) list.Add(item);
            if (targetType.IsAssignableFrom(listType)) { value = list; return true; }
        }
        value = array;
        return true;
    }

    private static bool TryParse(Type type, string source, IFormatProvider provider, out object? value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        MethodInfo? method = type.GetMethod("TryParse", flags, null, new[] { typeof(string), typeof(IFormatProvider), type.MakeByRefType() }, null);
        if (method is not null)
        {
            object?[] args = { source, provider, null };
            if (method.Invoke(null, args) is true) { value = args[2]; return true; }
        }
        method = type.GetMethod("TryParse", flags, null, new[] { typeof(string), type.MakeByRefType() }, null);
        if (method is not null)
        {
            object?[] args = { source, null };
            if (method.Invoke(null, args) is true) { value = args[1]; return true; }
        }
        method = type.GetMethod("Parse", flags, null, new[] { typeof(string) }, null);
        if (method is not null)
        {
            try { value = method.Invoke(null, new object?[] { source }); return true; } catch { }
        }
        value = null;
        return false;
    }
}
