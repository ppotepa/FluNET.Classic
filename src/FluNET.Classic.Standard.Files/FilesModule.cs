using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Files;

public sealed class FilesModule : LanguageModule
{
    public override string Name => "files";
}

[Qualifier("TEXT")]
public sealed class GetText : Get<string[], FileInfo>
{
    public GetText([What] string[] what, [From] FileInfo from) : base(what, from) { }
    protected override async ValueTask<string[]> ActAsync(FileInfo from, CancellationToken cancellationToken) => await File.ReadAllLinesAsync(from.FullName, cancellationToken).ConfigureAwait(false);
}

[Qualifier("TEXT")]
public sealed class GetTextMany : Get<string[], FileInfo[]>
{
    public GetTextMany([What] string[] what, [From] params FileInfo[] from) : base(what, from) { }
    protected override async ValueTask<string[]> ActAsync(FileInfo[] from, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        foreach (FileInfo file in from) lines.AddRange(await File.ReadAllLinesAsync(file.FullName, cancellationToken).ConfigureAwait(false));
        return lines.ToArray();
    }
}

[Qualifier("TEXT")]
public sealed class LoadText : Load<string[], FileInfo>
{
    public LoadText([What] string[] what, [From] FileInfo from) : base(what, from) { }
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

[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class DeleteFile : Delete<FileInfo>
{
    public DeleteFile([From] FileInfo from) : base(from) { }
    protected override ValueTask<bool> DeleteAsync(FileInfo from, CancellationToken cancellationToken)
    {
        bool existed = from.Exists;
        if (existed) from.Delete();
        return ValueTask.FromResult(existed);
    }
}
