using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace FluNET.Classic.Binding;

public enum ResolutionSourceKind
{
    Unknown, Literal, Reference, Identifier, Interpolation
}
public enum ResolutionStatus
{
    Success, NotFound, Ambiguous
}

public sealed record ResolutionContext(
    Type ExpectedType,
    string? RoleName = null,
    string? VerbName = null,
    string? Qualifier = null,
    IServiceProvider? Services = null,
    IFormatProvider? FormatProvider = null,
    IReadOnlyDictionary<string, object?>? Variables = null,
    string? ModuleName = null,
    ResolutionSourceKind SourceKind = ResolutionSourceKind.Unknown);

public sealed record ResolutionCandidate(string Resolver, int Priority, object? Value);
public sealed record ResolutionResult
{
    public ResolutionStatus Status
    {
        get;
    }

    public object? Value
    {
        get;
    }

    public string? Resolver
    {
        get;
    }

    public int Priority
    {
        get;
    }

    public IReadOnlyList<ResolutionCandidate> Candidates
    {
        get;
    }

    public ResolutionResult(ResolutionStatus Status, object? Value, string? Resolver, int Priority, IReadOnlyList<ResolutionCandidate> Candidates)
    {
        this.Status = Status;
        this.Value = Value;
        this.Resolver = Resolver;
        this.Priority = Priority;
        this.Candidates = Array.AsReadOnly((Candidates ?? throw new ArgumentNullException(nameof(Candidates))).ToArray());
    }

    public bool Success => Status == ResolutionStatus.Success;
}

public interface IValueResolver
{
    Type TargetType
    {
        get;
    }
    bool TryResolve(string source, ResolutionContext context, out object? value);
}

public interface IValueResolver<T> : IValueResolver
{
    bool TryResolve(string source, ResolutionContext context, out T? value);
}

public interface IContextualValueResolver : IValueResolver
{
    bool CanResolve(ResolutionContext context);
}

public sealed class ValueResolverRegistry
{
    private readonly Dictionary<Type, List<ResolverEntry>> _resolvers = new();
    private long _sequence;

    public void Register<T>(IValueResolver<T> resolver, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        Type type = typeof(T);
        if (!_resolvers.TryGetValue(type, out List<ResolverEntry>? entries))
            _resolvers[type] = entries = [];
        string id = $"{resolver.GetType().AssemblyQualifiedName ?? resolver.GetType().FullName ?? resolver.GetType().Name}#{Interlocked.Increment(ref _sequence)}";
        entries.Add(new(id, resolver, priority));
        entries.Sort((a, b) => b.Priority != a.Priority ? b.Priority.CompareTo(a.Priority) : string.Compare(a.Id, b.Id, StringComparison.Ordinal));
    }

    public bool TryResolve(string source, Type targetType, ResolutionContext context, out object? value)
    {
        ResolutionResult result = Resolve(source, targetType, context);
        value = result.Value;
        return result.Success;
    }

    public ResolutionResult Resolve(string source, Type targetType, ResolutionContext context)
    {
        Type effective = Nullable.GetUnderlyingType(targetType) ?? targetType;
        ResolutionResult registered = ResolveRegistered(source, targetType, context);
        if (registered.Status != ResolutionStatus.NotFound)
            return registered;
        if (effective != targetType)
        {
            registered = ResolveRegistered(source, effective, context);
            if (registered.Status != ResolutionStatus.NotFound)
                return registered;
        }

        if (effective == typeof(string))
            return BuiltIn(source, "builtin:string");
        if (effective == typeof(FileInfo))
            return BuiltIn(new FileInfo(source), "builtin:file");
        if (effective == typeof(DirectoryInfo))
            return BuiltIn(new DirectoryInfo(source), "builtin:directory");
        if (effective == typeof(Uri))
        {
            bool ok = Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out Uri? uri);
            return ok ? BuiltIn(uri, "builtin:uri") : NotFound();
        }
        if (effective.IsEnum)
        {
            bool ok = Enum.TryParse(effective, source, true, out object? parsed);
            return ok ? BuiltIn(parsed, "builtin:enum") : NotFound();
        }
        if (TryParse(effective, source, context.FormatProvider ?? CultureInfo.InvariantCulture, out object? parsedValue))
            return BuiltIn(parsedValue, "builtin:parse");

        TypeConverter converter = TypeDescriptor.GetConverter(effective);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                object? converted = converter.ConvertFrom(null, context.FormatProvider as CultureInfo ?? CultureInfo.InvariantCulture, source);
                if (converted is not null)
                    return BuiltIn(converted, $"typeconverter:{effective.FullName}");
            }
            catch { }
        }

        ConstructorInfo? ctor = effective.GetConstructor(new[] { typeof(string) });
        if (ctor is not null)
        {
            try
            {
                return BuiltIn(ctor.Invoke(new object?[] { source }), $"string-ctor:{effective.FullName}");
            }
            catch { }
        }
        return NotFound();
    }

    private ResolutionResult ResolveRegistered(string source, Type targetType, ResolutionContext context)
    {
        if (!_resolvers.TryGetValue(targetType, out List<ResolverEntry>? entries) || entries.Count == 0)
            return NotFound();
        foreach (IGrouping<int, ResolverEntry> priorityGroup in entries.GroupBy(x => x.Priority).OrderByDescending(x => x.Key))
        {
            var successful = new List<ResolutionCandidate>();
            foreach (ResolverEntry entry in priorityGroup)
            {
                ResolutionContext effectiveContext = context with
                {
                    ExpectedType = targetType
                };
                if (entry.Resolver is IContextualValueResolver contextual && !contextual.CanResolve(effectiveContext))
                    continue;
                if (entry.Resolver.TryResolve(source, effectiveContext, out object? value))
                    successful.Add(new(entry.Id, entry.Priority, value));
            }
            if (successful.Count == 1)
            {
                ResolutionCandidate winner = successful[0];
                return new(ResolutionStatus.Success, winner.Value, winner.Resolver, winner.Priority, successful);
            }
            if (successful.Count > 1)
                return new(ResolutionStatus.Ambiguous, null, null, priorityGroup.Key, successful.OrderBy(x => x.Resolver, StringComparer.Ordinal).ToArray());
        }
        return NotFound();
    }

    private static ResolutionResult BuiltIn(object? value, string resolver) =>
        new(ResolutionStatus.Success, value, resolver, int.MinValue, new[] { new ResolutionCandidate(resolver, int.MinValue, value) });
    private static ResolutionResult NotFound() => new(ResolutionStatus.NotFound, null, null, int.MinValue, Array.Empty<ResolutionCandidate>());

    private static bool TryParse(Type type, string source, IFormatProvider provider, out object? value)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        MethodInfo? method = type.GetMethod("TryParse", flags, null, new[] { typeof(string), typeof(IFormatProvider), type.MakeByRefType() }, null);
        if (method is not null)
        {
            object?[] args = { source, provider, null };
            if (method.Invoke(null, args) is true)
            {
                value = args[2];
                return true;
            }
        }
        method = type.GetMethod("TryParse", flags, null, new[] { typeof(string), type.MakeByRefType() }, null);
        if (method is not null)
        {
            object?[] args = { source, null };
            if (method.Invoke(null, args) is true)
            {
                value = args[1];
                return true;
            }
        }
        method = type.GetMethod("Parse", flags, null, new[] { typeof(string) }, null);
        if (method is not null)
        {
            try
            {
                value = method.Invoke(null, new object?[] { source });
                return true;
            }
            catch { }
        }
        value = null;
        return false;
    }

    private sealed record ResolverEntry(string Id, IValueResolver Resolver, int Priority);
}
