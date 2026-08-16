using FluNET.Syntax.Core;

namespace FluNET.Language;

public interface IVerbFamily : IVerb, ILanguageElement
{
}

public interface IGet : IVerbFamily;
public interface ISave : IVerbFamily;
public interface ILoad : IVerbFamily;
public interface ISend : IVerbFamily;
public interface IDownload : IVerbFamily;
public interface IDelete : IVerbFamily;
public interface IPost : IVerbFamily;
public interface ITransform : IVerbFamily;
public interface ISay : IVerbFamily;
