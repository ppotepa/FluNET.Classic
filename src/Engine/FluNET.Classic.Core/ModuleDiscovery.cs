using System.Reflection;

namespace FluNET.Classic.Core;

public static class ModuleDiscovery
{
    public static IReadOnlyList<ILanguageModule> Discover(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var modules = new List<ILanguageModule>();
        foreach (Assembly assembly in assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .OrderBy(AssemblyIdentity, StringComparer.Ordinal))
        {
            foreach (Type type in GetLoadableTypes(assembly)
                .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ILanguageModule).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor is not null && ctor.Invoke(null) is ILanguageModule module)
                    modules.Add(module);
            }
        }
        return Array.AsReadOnly(modules
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Last())
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.GetType().FullName, StringComparer.Ordinal)
            .ToArray());
    }

    private static string AssemblyIdentity(Assembly assembly) => assembly.FullName ?? assembly.GetName().Name ?? string.Empty;

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }
}
