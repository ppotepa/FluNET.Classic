namespace FluNET.Classic.Core;

public interface ILanguageElement { string Name { get; } }
public interface IRole { }
public interface IRole<out TValue> : IRole { }
public interface IWhat<out TValue> : IRole<TValue> { }
public interface IFrom<out TValue> : IRole<TValue> { }
public interface ITo<out TValue> : IRole<TValue> { }
public interface IUsing<out TValue> : IRole<TValue> { }
public interface IWith<out TValue> : IRole<TValue> { }
public interface IAs<out TValue> : IRole<TValue> { }
public interface IIn<out TValue> : IRole<TValue> { }
public interface IAt<out TValue> : IRole<TValue> { }
public interface IFor<out TValue> : IRole<TValue> { }
public interface IUntil<out TValue> : IRole<TValue> { }
public interface IThen<out TValue> : IRole<TValue> { }

public enum RoleDirection { Input, Output, InputOutput }
public enum RoleCardinality { One, ZeroOrOne, OneOrMore, ZeroOrMore }
public enum ExecutionTrait { Pure, Idempotent, Retryable, Transactional, LongRunning, SideEffecting, NonDeterministic }

public interface IVerb { }
public interface IVerb<TResult> : IVerb { ValueTask<TResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default); }
public interface IVerbFamily : IVerb { }
public interface IGet : IVerbFamily { }
public interface ISave : IVerbFamily { }
public interface ILoad : IVerbFamily { }
public interface ICreate : IVerbFamily { }
public interface IDelete : IVerbFamily { }
public interface IListVerb : IVerbFamily { }
public interface ICopy : IVerbFamily { }
public interface IMove : IVerbFamily { }
public interface IRun : IVerbFamily { }
public interface IStop : IVerbFamily { }
public interface ISend : IVerbFamily { }
public interface IDownload : IVerbFamily { }
public interface IPost : IVerbFamily { }
public interface ICheck : IVerbFamily { }
public interface IParse : IVerbFamily { }
public interface IFormat : IVerbFamily { }
public interface ITransform : IVerbFamily { }
public interface IWait : IVerbFamily { }
public interface IFilter : IVerbFamily { }
public interface ISay : IVerbFamily { }

public interface IPipelineProducer<out TValue> { }
public interface IPipelineConsumer<in TValue> { }

public interface IExistenceState { bool Exists { get; } }
public interface IOkState { bool IsOk { get; } }
public interface IValidState { bool IsValid { get; } }

public sealed record VerbExecutionContext(IServiceProvider? Services, IReadOnlyDictionary<string, object?> Variables, object? PipelineValue);

public static class StandardCapabilities
{
    public const string FileSystemRead = "filesystem.read";
    public const string FileSystemWrite = "filesystem.write";
    public const string Network = "network";
    public const string EmailSend = "email.send";
    public const string ProcessExecute = "process.execute";
    public const string ProcessInspect = "process.inspect";
    public const string ProcessTerminate = "process.terminate";
    public const string EnvironmentRead = "os.environment.read";
    public const string EnvironmentWrite = "os.environment.write";
    public const string SystemRead = "os.system.read";
}

public interface ICapabilityPolicy { bool IsAllowed(string capability); }
public sealed class AllowAllCapabilityPolicy : ICapabilityPolicy { public bool IsAllowed(string capability) => true; }
public sealed class CapabilitySetPolicy : ICapabilityPolicy
{
    private readonly HashSet<string> _allowed;
    public CapabilitySetPolicy(IEnumerable<string> allowed) => _allowed = new(allowed ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    public bool IsAllowed(string capability) => _allowed.Contains(capability);
}
