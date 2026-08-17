namespace FluNET.Classic.Core;

public sealed class ScopedCapabilitySetPolicy : IScopedCapabilityPolicy
{
    private readonly HashSet<string> _unscoped = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Func<object?, bool>>> _scopes = new(StringComparer.OrdinalIgnoreCase);

    public ScopedCapabilitySetPolicy Allow(string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        _unscoped.Add(capability);
        return this;
    }

    public ScopedCapabilitySetPolicy Allow(string capability, Func<object?, bool> scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        ArgumentNullException.ThrowIfNull(scope);
        if (!_scopes.TryGetValue(capability, out List<Func<object?, bool>>? scopes))
            _scopes[capability] = scopes = [];
        scopes.Add(scope);
        return this;
    }

    // Generic preflight: a scoped grant is sufficient to let binding/execution continue
    // until concrete resources have been materialized.
    public bool IsAllowed(string capability) => _unscoped.Contains(capability) || _scopes.ContainsKey(capability);

    // A null resource deliberately means "unscoped grant only". Runtime uses this
    // to distinguish broad permission from a permission that still needs resource checks.
    public bool IsAllowed(string capability, object? resource)
    {
        if (_unscoped.Contains(capability)) return true;
        if (resource is null || !_scopes.TryGetValue(capability, out List<Func<object?, bool>>? scopes)) return false;
        return scopes.Any(scope => scope(resource));
    }
}

public static class CapabilityScopes
{
    public static Func<object?, bool> FileSystemUnder(DirectoryInfo root)
    {
        ArgumentNullException.ThrowIfNull(root);
        string rootPath = NormalizeDirectory(root.FullName);
        return resource => resource switch
        {
            FileSystemInfo info => IsUnder(info.FullName, rootPath),
            _ => false
        };
    }

    public static Func<object?, bool> UriHost(params string[] hosts)
    {
        var allowed = new HashSet<string>(hosts ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return resource => resource is Uri uri && !string.IsNullOrWhiteSpace(uri.Host) && allowed.Contains(uri.Host);
    }

    public static Func<object?, bool> OfType<T>(Func<T, bool>? predicate = null) => resource =>
        resource is T value && (predicate is null || predicate(value));

    private static string NormalizeDirectory(string path)
    {
        string full = Path.GetFullPath(path);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }

    private static bool IsUnder(string path, string root)
    {
        string full = Path.GetFullPath(path);
        string rootWithoutSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return full.Equals(rootWithoutSeparator, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
