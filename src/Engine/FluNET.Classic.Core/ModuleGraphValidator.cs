namespace FluNET.Classic.Core;

public static class ModuleGraphValidator
{
    public static IReadOnlyList<LanguageDiagnostic> Validate(IEnumerable<ILanguageModule> modules)
    {
        ILanguageModule[] source = (modules ?? Array.Empty<ILanguageModule>()).ToArray();
        var diagnostics = new List<LanguageDiagnostic>();
        var byName = new Dictionary<string, ILanguageModule>(StringComparer.OrdinalIgnoreCase);

        foreach (ILanguageModule module in source)
        {
            if (!byName.TryAdd(module.Name, module))
                diagnostics.Add(new("FLU-LANG-029", $"Duplicate module name '{module.Name}'.", LanguageDiagnosticSeverity.Error));
        }

        foreach (ILanguageModule module in source)
        foreach (string dependency in module.Dependencies)
            if (!byName.ContainsKey(dependency))
                diagnostics.Add(new("FLU-LANG-030", $"Module '{module.Name}' requires missing module '{dependency}'.", LanguageDiagnosticSeverity.Error));

        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = new Stack<string>();
        var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string module in byName.Keys)
            Visit(module);

        return diagnostics;

        void Visit(string name)
        {
            if (state.TryGetValue(name, out int current))
            {
                if (current != 1) return;
                string[] cycle = path.Reverse().SkipWhile(x => !x.Equals(name, StringComparison.OrdinalIgnoreCase)).Append(name).ToArray();
                string key = string.Join("->", cycle);
                if (reported.Add(key))
                    diagnostics.Add(new("FLU-LANG-031", $"Module dependency cycle detected: {string.Join(" -> ", cycle)}.", LanguageDiagnosticSeverity.Error));
                return;
            }

            state[name] = 1;
            path.Push(name);
            if (byName.TryGetValue(name, out ILanguageModule? module))
                foreach (string dependency in module.Dependencies)
                    if (byName.ContainsKey(dependency)) Visit(dependency);
            path.Pop();
            state[name] = 2;
        }
    }
}
