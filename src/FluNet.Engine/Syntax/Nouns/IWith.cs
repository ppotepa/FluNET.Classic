using FluNET.Keywords;
using FluNET.Language;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns;

public interface IWith<out TValue> : INoun, IKeyword, IRole<TValue>
{
    TValue With { get; }
}
