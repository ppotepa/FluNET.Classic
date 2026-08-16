using FluNET.Keywords;
using FluNET.Language;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns;

public interface IFrom<out TValue> : INoun, IKeyword, IRole<TValue>
{
    TValue From { get; }
}
