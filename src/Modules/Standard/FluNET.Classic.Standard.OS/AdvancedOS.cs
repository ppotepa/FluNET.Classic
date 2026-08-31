using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.OS;

public sealed record EnvironmentVariableName(string Value)
{
    public static bool TryParse(string value, out EnvironmentVariableName? result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = null;
            return false;
        }
        result = new(value.Trim());
        return true;
    }

    public override string ToString() => Value;
}
public sealed record EnvironmentVariable(string Name, string? Value);
public enum WorkingDirectoryTarget
{
    CWD
}

[Verb("LIST")]
[Qualifier("ENV")]
[RequiresCapability(StandardCapabilities.EnvironmentRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class ListEnvironmentVariables : IContextQuery<EnvironmentVariable[]>, IListVerb, IPipelineProducer<EnvironmentVariable[]>
{
    public ValueTask<EnvironmentVariable[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = new List<EnvironmentVariable>();
        foreach (System.Collections.DictionaryEntry item in Environment.GetEnvironmentVariables())
            result.Add(new(item.Key?.ToString() ?? string.Empty, item.Value?.ToString()));
        return ValueTask.FromResult(result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}

[Verb("SAVE")]
[Qualifier("CWD")]
[RequiresCapability(StandardCapabilities.SystemWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveWorkingDirectory : IVerb<WorkingDirectory>, ISave, IWhat<DirectoryInfo>, ITo<WorkingDirectoryTarget>, IPipelineConsumer<DirectoryInfo>, IPipelineProducer<WorkingDirectory>
{
    private readonly DirectoryInfo _directory; private readonly WorkingDirectoryTarget _target;
    public SaveWorkingDirectory([What] DirectoryInfo directory, [To] WorkingDirectoryTarget target)
    {
        _directory = directory;
        _target = target;
    }
    public ValueTask<WorkingDirectory> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_target != WorkingDirectoryTarget.CWD)
            throw new NotSupportedException(_target.ToString());
        if (!_directory.Exists)
            throw new DirectoryNotFoundException(_directory.FullName);
        Environment.CurrentDirectory = _directory.FullName;
        return ValueTask.FromResult(new WorkingDirectory(Environment.CurrentDirectory));
    }
}
