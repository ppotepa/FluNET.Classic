using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Files;

public enum SearchScope { TOP, RECURSIVE }
public enum FileWriteMode { OVERWRITE, APPEND, ATOMIC }

[Verb("LIST"), Qualifier("FILES"), RequiresCapability(StandardCapabilities.FileSystemRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class ListFilesScoped : IVerb<FileInfo[]>, IListVerb, IIn<DirectoryInfo>, IUsing<SearchScope>, IWith<FilePattern>, IPipelineProducer<FileInfo[]>
{
    private readonly DirectoryInfo _directory; private readonly SearchScope _scope; private readonly FilePattern? _pattern;
    public ListFilesScoped([In] DirectoryInfo directory, [Using] SearchScope scope, [With] FilePattern? pattern = null) { _directory = directory; _scope = scope; _pattern = pattern; }
    public ValueTask<FileInfo[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_directory.Exists ? _directory.GetFiles(_pattern?.Pattern ?? "*", _scope == SearchScope.RECURSIVE ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<FileInfo>());
}

[Verb("LIST"), Qualifier("DIRECTORY"), RequiresCapability(StandardCapabilities.FileSystemRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class ListDirectories : IVerb<DirectoryInfo[]>, IListVerb, IIn<DirectoryInfo>, IUsing<SearchScope>, IPipelineProducer<DirectoryInfo[]>
{
    private readonly DirectoryInfo _directory; private readonly SearchScope _scope;
    public ListDirectories([In] DirectoryInfo directory, [Using] SearchScope scope) { _directory = directory; _scope = scope; }
    public ValueTask<DirectoryInfo[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_directory.Exists ? _directory.GetDirectories("*", _scope == SearchScope.RECURSIVE ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly) : Array.Empty<DirectoryInfo>());
}

[Verb("SAVE"), Qualifier("TEXT"), RequiresCapability(StandardCapabilities.FileSystemWrite), ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveTextWithMode : IVerb<string>, ISave, IWhat<string>, ITo<FileInfo>, IUsing<FileWriteMode>, IPipelineConsumer<string>, IPipelineProducer<string>
{
    private readonly string _text; private readonly FileInfo _file; private readonly FileWriteMode _mode;
    public SaveTextWithMode([What] string text, [To] FileInfo file, [Using] FileWriteMode mode) { _text = text; _file = file; _mode = mode; }
    public async ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        _file.Directory?.Create();
        if (_mode == FileWriteMode.APPEND) await File.AppendAllTextAsync(_file.FullName, _text, cancellationToken).ConfigureAwait(false);
        else if (_mode == FileWriteMode.ATOMIC)
        {
            string temp = _file.FullName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temp, _text, cancellationToken).ConfigureAwait(false); File.Move(temp, _file.FullName, true); } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        else await File.WriteAllTextAsync(_file.FullName, _text, cancellationToken).ConfigureAwait(false);
        return _text;
    }
}

[Verb("COPY"), Qualifier("DIRECTORY"), RequiresCapability(StandardCapabilities.FileSystemRead), RequiresCapability(StandardCapabilities.FileSystemWrite), ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class CopyDirectory : IVerb<DirectoryInfo>, ICopy, IWhat<DirectoryInfo>, ITo<DirectoryInfo>, IPipelineConsumer<DirectoryInfo>, IPipelineProducer<DirectoryInfo>
{
    private readonly DirectoryInfo _source; private readonly DirectoryInfo _destination;
    public CopyDirectory([What] DirectoryInfo source, [To] DirectoryInfo destination) { _source = source; _destination = destination; }
    public ValueTask<DirectoryInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!_source.Exists) throw new DirectoryNotFoundException(_source.FullName); _destination.Create();
        foreach (DirectoryInfo directory in _source.GetDirectories("*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(_destination.FullName, Path.GetRelativePath(_source.FullName, directory.FullName)));
        foreach (FileInfo file in _source.GetFiles("*", SearchOption.AllDirectories)) { string target = Path.Combine(_destination.FullName, Path.GetRelativePath(_source.FullName, file.FullName)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); file.CopyTo(target, true); }
        _destination.Refresh(); return ValueTask.FromResult(_destination);
    }
}

[Verb("MOVE"), Qualifier("DIRECTORY"), RequiresCapability(StandardCapabilities.FileSystemRead), RequiresCapability(StandardCapabilities.FileSystemWrite), ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class MoveDirectory : IVerb<DirectoryInfo>, IMove, IWhat<DirectoryInfo>, ITo<DirectoryInfo>, IPipelineConsumer<DirectoryInfo>, IPipelineProducer<DirectoryInfo>
{
    private readonly DirectoryInfo _source; private readonly DirectoryInfo _destination;
    public MoveDirectory([What] DirectoryInfo source, [To] DirectoryInfo destination) { _source = source; _destination = destination; }
    public ValueTask<DirectoryInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) { _destination.Parent?.Create(); _source.MoveTo(_destination.FullName); _destination.Refresh(); return ValueTask.FromResult(_destination); }
}

[Verb("DELETE"), Qualifier("DIRECTORY"), RequiresCapability(StandardCapabilities.FileSystemWrite), ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class DeleteDirectory : IVerb<bool>, IDelete, IAt<DirectoryInfo>, IPipelineProducer<bool>
{
    private readonly DirectoryInfo _directory; public DeleteDirectory([At] DirectoryInfo directory) => _directory = directory;
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) { bool existed = _directory.Exists; if (existed) _directory.Delete(true); return ValueTask.FromResult(existed); }
}
