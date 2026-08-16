using FluNET.Keywords;
using FluNET.Language;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns;

public interface ITo<out TValue> : INoun, IKeyword, IRole<TValue>
{
    TValue To { get; }
}
