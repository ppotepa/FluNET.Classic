using FluNET.Classic.Core;
using FluNET.Classic.Standard.Files;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Json;
using FluNET.Classic.Standard.Text;

namespace FluNET.Classic.Standard;

public static class StandardModules
{
    public static IReadOnlyList<ILanguageModule> Create() => new ILanguageModule[] { new FilesModule(), new TextModule(), new JsonModule(), new HttpModule() };
}
