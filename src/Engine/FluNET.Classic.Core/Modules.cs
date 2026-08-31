using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace FluNET.Classic.Core;

public interface ILanguageModule
{
    string Name
    {
        get;
    }
    Version Version
    {
        get;
    }
    IReadOnlyCollection<string> Dependencies
    {
        get;
    }
    IReadOnlyCollection<Assembly> Assemblies
    {
        get;
    }
    IReadOnlyCollection<QualifierDescriptor> Qualifiers
    {
        get;
    }
    IReadOnlyCollection<PredicateDescriptor> Predicates
    {
        get;
    }
    IReadOnlyCollection<OperatorDescriptor> Operators
    {
        get;
    }
    IReadOnlyCollection<IntrinsicDescriptor> Intrinsics
    {
        get;
    }
}

public abstract class LanguageModule : ILanguageModule
{
    public abstract string Name
    {
        get;
    }
    public virtual Version Version => new(1, 0, 0);
    public virtual IReadOnlyCollection<string> Dependencies => Array.Empty<string>();
    public virtual IReadOnlyCollection<Assembly> Assemblies => new[] { GetType().Assembly };
    public virtual IReadOnlyCollection<QualifierDescriptor> Qualifiers => Array.Empty<QualifierDescriptor>();
    public virtual IReadOnlyCollection<PredicateDescriptor> Predicates => Array.Empty<PredicateDescriptor>();
    public virtual IReadOnlyCollection<OperatorDescriptor> Operators => Array.Empty<OperatorDescriptor>();
    public virtual IReadOnlyCollection<IntrinsicDescriptor> Intrinsics => Array.Empty<IntrinsicDescriptor>();
}

public static class StandardQualifiers
{
    public static IReadOnlyList<QualifierDescriptor> All
    {
        get;
    } = new QualifierDescriptor[]
    {
        new("qualifier:text", "TEXT", typeof(string)), new("qualifier:json", "JSON", typeof(JsonNode)), new("qualifier:xml", "XML", typeof(XDocument)),
        new("qualifier:binary", "BINARY", typeof(byte[])), new("qualifier:csv", "CSV", typeof(string)), new("qualifier:html", "HTML", typeof(string)),
        new("qualifier:yaml", "YAML", typeof(string)), new("qualifier:image", "IMAGE", typeof(byte[])), new("qualifier:video", "VIDEO", typeof(byte[])),
        new("qualifier:audio", "AUDIO", typeof(byte[])), new("qualifier:file", "FILE", typeof(FileInfo)), new("qualifier:uri", "URI", typeof(Uri)),
        new("qualifier:date", "DATE", typeof(DateTime)), new("qualifier:boolean", "BOOLEAN", typeof(bool)), new("qualifier:number", "NUMBER", typeof(decimal))
    };
}
