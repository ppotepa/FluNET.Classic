using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Files;

public sealed record DirectoryMetadata(
    string Name,
    string FullName,
    DateTimeOffset Created,
    DateTimeOffset Modified,
    int FileCount,
    int DirectoryCount,
    bool Exists) : IExistenceState;

[Verb("CREATE")]
[Qualifier("DIRECTORY")]
[RequiresCapability(StandardCapabilities.FileSystemWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class CreateDirectory : IVerb<DirectoryInfo>, ICreate, IAt<DirectoryInfo>, IPipelineProducer<DirectoryInfo>
{
    private readonly DirectoryInfo _directory;
    public CreateDirectory([At] DirectoryInfo directory) => _directory = directory;
    public ValueTask<DirectoryInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _directory.Create();
        _directory.Refresh();
        return ValueTask.FromResult(_directory);
    }
}

[Verb("GET")]
[Qualifier("METADATA")]
[RequiresCapability(StandardCapabilities.FileSystemRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetDirectoryMetadata : IVerb<DirectoryMetadata>, IGet, IFrom<DirectoryInfo>, IPipelineConsumer<DirectoryInfo>, IPipelineProducer<DirectoryMetadata>
{
    private readonly DirectoryInfo _directory;
    public GetDirectoryMetadata([From] DirectoryInfo directory) => _directory = directory;
    public ValueTask<DirectoryMetadata> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _directory.Refresh();
        bool exists = _directory.Exists;
        return ValueTask.FromResult(new DirectoryMetadata(
            _directory.Name,
            _directory.FullName,
            exists ? new DateTimeOffset(_directory.CreationTimeUtc, TimeSpan.Zero) : default,
            exists ? new DateTimeOffset(_directory.LastWriteTimeUtc, TimeSpan.Zero) : default,
            exists ? _directory.GetFiles().Length : 0,
            exists ? _directory.GetDirectories().Length : 0,
            exists));
    }
}
