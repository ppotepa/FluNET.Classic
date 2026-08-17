namespace FluNET.Language;

public interface ILanguageModule : ILanguageElement
{
    Version Version { get; }

    IReadOnlyCollection<Type> VerbTypes { get; }

    IReadOnlyCollection<QualifierDescriptor> Qualifiers { get; }

    IReadOnlyCollection<Type> ResolverTypes { get; }

    IReadOnlyCollection<Type> ConverterTypes { get; }

    IReadOnlyCollection<string> Dependencies { get; }
}

public sealed record ModuleDescriptor(
    string Name,
    Version Version,
    Type ModuleType,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<Type> VerbTypes,
    IReadOnlyList<QualifierDescriptor> Qualifiers,
    IReadOnlyList<Type> ResolverTypes,
    IReadOnlyList<Type> ConverterTypes);

public enum ExecutionTrait
{
    Pure,
    Idempotent,
    SideEffecting,
    Retryable,
    Transactional,
    LongRunning
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class ExecutionTraitAttribute(ExecutionTrait trait) : Attribute
{
    public ExecutionTrait Trait { get; } = trait;
}

public static class StandardCapabilities
{
    public const string FileSystemRead = "filesystem.read";
    public const string FileSystemWrite = "filesystem.write";
    public const string Network = "network";
    public const string EmailSend = "email.send";
    public const string ProcessExecute = "process.execute";
}
