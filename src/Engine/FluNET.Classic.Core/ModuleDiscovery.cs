using System.Reflection;

namespace FluNET.Classic.Core;

public static class ModuleDiscovery
{
    public static IReadOnlyList<ILanguageModule> Discover(IEnumerable<Assembly> assemblies)
    {
        var modules = new List<ILanguageModule>();
        foreach (Assembly assembly in assemblies.Distinct())
        {
            foreach (Type type in GetLoadableTypes(assembly).Where(t => !t.IsAbstract && !t.IsInterface && typeof(ILanguageModule).IsAssignableFrom(t)))
            {
                ConstructorInfo? ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor is not null && ctor.Invoke(null) is ILanguageModule module) modules.Add(module);
            }
        }
        return modules.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.Last()).ToArray();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }
}
