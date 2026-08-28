using FluNET.Classic.Core;
using FluNET.Classic.Standard.Collections;
using FluNET.Classic.Standard.DateTime;
using FluNET.Classic.Standard.Files;
using FluNET.Classic.Standard.Http;
using FluNET.Classic.Standard.Json;
using FluNET.Classic.Standard.OS;
using FluNET.Classic.Standard.Process;
using FluNET.Classic.Standard.Text;

namespace FluNET.Classic.Standard;

public static class StandardModules
{
    public static IReadOnlyList<ILanguageModule> Create() => new ILanguageModule[]
    {
        new TextModule(),
        new FilesModule(),
        new DateTimeModule(),
        new OSModule(),
        new ProcessModule(),
        new JsonModule(),
        new HttpModule(),
        new CollectionsModule()
    };
}
