using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Files;

public enum TextFileRepresentation
{
    TEXT
}
public enum BinaryFileRepresentation
{
    BINARY
}

public sealed record PathSpec(string Value)
{
    public string FullPath => Path.GetFullPath(Value);
    public override string ToString() => Value;
}

public sealed record FilePattern(string Value)
{
    public string Pattern => string.IsNullOrWhiteSpace(Value) ? "*" : Value;
    public override string ToString() => Pattern;
}

public sealed record FileMetadata(
    string Name,
    string FullName,
    long Length,
    string Extension,
    DateTimeOffset Created,
    DateTimeOffset Modified,
    bool ReadOnly);

public sealed class FilesModule : LanguageModule
{
    public override string Name => "files";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:files", "FILES", typeof(FileInfo[])),
        new("qualifier:directory", "DIRECTORY", typeof(DirectoryInfo)),
        new("qualifier:file-metadata", "METADATA", typeof(FileMetadata))
    };
}

[Qualifier("TEXT")]
public sealed class GetText : Get<string[], FileInfo>, IAs<TextFileRepresentation>
{
    public GetText([From] FileInfo from, [As] TextFileRepresentation @as = TextFileRepresentation.TEXT) : base(from) { }
    protected override async ValueTask<string[]> ActAsync(FileInfo from, CancellationToken cancellationToken) => await File.ReadAllLinesAsync(from.FullName, cancellationToken).ConfigureAwait(false);
}

[Qualifier("TEXT")]
public sealed class GetTextMany : Get<string[], FileInfo[]>
{
    public GetTextMany([From] params FileInfo[] from) : base(from) { }
    protected override async ValueTask<string[]> ActAsync(FileInfo[] from, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (FileInfo file in from)
            lines.AddRange(await File.ReadAllLinesAsync(file.FullName, cancellationToken).ConfigureAwait(false));
        return lines.ToArray();
    }
}

[Qualifier("BINARY")]
public sealed class GetBinary : Get<byte[], FileInfo>, IAs<BinaryFileRepresentation>
{
    public GetBinary([From] FileInfo from, [As] BinaryFileRepresentation @as = BinaryFileRepresentation.BINARY) : base(from) { }
    protected override async ValueTask<byte[]> ActAsync(FileInfo from, CancellationToken cancellationToken) => await File.ReadAllBytesAsync(from.FullName, cancellationToken).ConfigureAwait(false);
}

[Qualifier("METADATA")]
public sealed class GetFileMetadata : Get<FileMetadata, FileInfo>
{
    public GetFileMetadata([From] FileInfo from) : base(from) { }
    protected override ValueTask<FileMetadata> ActAsync(FileInfo from, CancellationToken cancellationToken)
    {
        from.Refresh();
        return ValueTask.FromResult(new FileMetadata(
            from.Name,
            from.FullName,
            from.Exists ? from.Length : 0,
            from.Extension,
            new DateTimeOffset(from.CreationTimeUtc, TimeSpan.Zero),
            new DateTimeOffset(from.LastWriteTimeUtc, TimeSpan.Zero),
            from.IsReadOnly));
    }
}

[Qualifier("TEXT")]
public sealed class LoadText : Load<string[], FileInfo>
{
    public LoadText([From] FileInfo from) : base(from) { }
    protected override async ValueTask<string[]> ActAsync(FileInfo from, CancellationToken cancellationToken) => await File.ReadAllLinesAsync(from.FullName, cancellationToken).ConfigureAwait(false);
}

[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveText : Save<string, FileInfo>
{
    public SaveText([What] string what, [To] FileInfo to) : base(what, to) { }
    protected override ValueTask SaveAsync(string what, FileInfo to, CancellationToken cancellationToken) => new(File.WriteAllTextAsync(to.FullName, what, cancellationToken));
}

[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveLines : Save<string[], FileInfo>
{
    public SaveLines([What] string[] what, [To] FileInfo to) : base(what, to) { }
    protected override ValueTask SaveAsync(string[] what, FileInfo to, CancellationToken cancellationToken) => new(File.WriteAllLinesAsync(to.FullName, what, cancellationToken));
}

[Qualifier("BINARY")]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveBinary : Save<byte[], FileInfo>
{
    public SaveBinary([What] byte[] what, [To] FileInfo to) : base(what, to) { }
    protected override ValueTask SaveAsync(byte[] what, FileInfo to, CancellationToken cancellationToken) => new(File.WriteAllBytesAsync(to.FullName, what, cancellationToken));
}

[Verb("LIST")]
[Qualifier("FILES")]
public sealed class ListFiles : IVerb<FileInfo[]>, IListVerb, IIn<DirectoryInfo>, IWith<FilePattern>, IPipelineProducer<FileInfo[]>
{
    private readonly DirectoryInfo _directory;
    private readonly FilePattern? _pattern;
    public ListFiles([In] DirectoryInfo directory, [With] FilePattern? pattern = null)
    {
        _directory = directory;
        _pattern = pattern;
    }
    public ValueTask<FileInfo[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_directory.Exists ? _directory.GetFiles(_pattern?.Pattern ?? "*") : Array.Empty<FileInfo>());
}

[Verb("CREATE")]
[Qualifier("FILE")]
public sealed class CreateFile : IVerb<FileInfo>, ICreate, IAt<FileInfo>, IPipelineProducer<FileInfo>
{
    private readonly FileInfo _file;
    public CreateFile([At] FileInfo file) => _file = file;
    public ValueTask<FileInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        _file.Directory?.Create();
        if (!_file.Exists)
        {
            using (_file.Create())
            {
            }
            _file.Refresh();
        }
        return ValueTask.FromResult(_file);
    }
}

[Verb("COPY")]
[Qualifier("FILE")]
public sealed class CopyFile : IVerb<FileInfo>, ICopy, IWhat<FileInfo>, ITo<FileInfo>, IPipelineConsumer<FileInfo>, IPipelineProducer<FileInfo>
{
    private readonly FileInfo _source;
    private readonly FileInfo _destination;
    public CopyFile([What] FileInfo source, [To] FileInfo destination)
    {
        _source = source;
        _destination = destination;
    }
    public ValueTask<FileInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        _destination.Directory?.Create();
        _source.CopyTo(_destination.FullName, overwrite: true);
        _destination.Refresh();
        return ValueTask.FromResult(_destination);
    }
}

[Verb("MOVE")]
[Qualifier("FILE")]
public sealed class MoveFile : IVerb<FileInfo>, IMove, IWhat<FileInfo>, ITo<FileInfo>, IPipelineConsumer<FileInfo>, IPipelineProducer<FileInfo>
{
    private readonly FileInfo _source;
    private readonly FileInfo _destination;
    public MoveFile([What] FileInfo source, [To] FileInfo destination)
    {
        _source = source;
        _destination = destination;
    }
    public ValueTask<FileInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        _destination.Directory?.Create();
        _source.MoveTo(_destination.FullName, overwrite: true);
        _destination.Refresh();
        return ValueTask.FromResult(_destination);
    }
}

[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class DeleteFile : Delete<FileInfo>, IAt<FileInfo>
{
    public DeleteFile([At] FileInfo at) : base(at) { }
    protected override ValueTask<bool> DeleteAsync(FileInfo from, CancellationToken cancellationToken)
    {
        bool existed = from.Exists;
        if (existed)
            from.Delete();
        return ValueTask.FromResult(existed);
    }
}
