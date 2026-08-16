using FluNET.Keywords;
using FluNET.Language;
using FluNET.Syntax.Core;

namespace FluNET.Syntax.Nouns;

public interface IUsing<out TValue> : INoun, IKeyword, IRole<TValue>
{
    TValue Using { get; }
}
