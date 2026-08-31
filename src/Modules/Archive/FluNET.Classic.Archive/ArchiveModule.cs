using FluNET.Classic.Core;
using System.IO.Compression;

namespace FluNET.Classic.Archive;

public enum CompressionFormat
{
    ZIP
}
public sealed record ArchiveDocument(byte[] Data, CompressionFormat Format);
public sealed record ArchiveEntry(string Name, byte[] Data, long Length);

public sealed class ArchiveModule : LanguageModule
{
    public override string Name => "archive";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:archive", "ARCHIVE", typeof(ArchiveDocument)),
        new("qualifier:entries", "ENTRIES", typeof(ArchiveEntry[])),
        new("qualifier:archive-format", "FORMAT", typeof(CompressionFormat)),
        new("qualifier:archive-entry-name", "NAME", typeof(string)),
        new("qualifier:archive-entry-length", "LENGTH", typeof(long))
    };
}

[Verb("GET")]
[Qualifier("FORMAT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetArchiveFormat : Get<CompressionFormat, ArchiveDocument>
{
    public GetArchiveFormat([From] ArchiveDocument from) : base(from) { }

    protected override ValueTask<CompressionFormat> ActAsync(ArchiveDocument from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Format);
}

[Verb("GET")]
[Qualifier("NAME")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetArchiveEntryName : Get<string, ArchiveEntry>
{
    public GetArchiveEntryName([From] ArchiveEntry from) : base(from) { }

    protected override ValueTask<string> ActAsync(ArchiveEntry from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Name);
}

[Verb("GET")]
[Qualifier("LENGTH")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetArchiveEntryLength : Get<long, ArchiveEntry>
{
    public GetArchiveEntryLength([From] ArchiveEntry from) : base(from) { }

    protected override ValueTask<long> ActAsync(ArchiveEntry from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Length);
}

[Verb("CREATE")]
[Qualifier("ARCHIVE")]
[RequiresCapability(StandardCapabilities.FileSystemRead)]
public sealed class CreateArchive : IVerb<ArchiveDocument>, ICreate, IFrom<FileInfo[]>, IUsing<CompressionFormat>, IPipelineProducer<ArchiveDocument>
{
    private readonly FileInfo[] _files; private readonly CompressionFormat _format;
    public CreateArchive([From] FileInfo[] files, [Using] CompressionFormat format)
    {
        _files = files;
        _format = format;
    }
    public async ValueTask<ArchiveDocument> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (_format != CompressionFormat.ZIP)
            throw new NotSupportedException(_format.ToString());
        await using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (FileInfo file in _files)
            {
                ZipArchiveEntry entry = zip.CreateEntry(file.Name, CompressionLevel.Optimal);
                await using Stream target = entry.Open();
                await using FileStream source = file.OpenRead();
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }
        return new(output.ToArray(), _format);
    }
}

[Verb("LIST")]
[Qualifier("ENTRIES")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ListArchiveEntries : IVerb<ArchiveEntry[]>, IListVerb, IFrom<ArchiveDocument>, IPipelineConsumer<ArchiveDocument>, IPipelineProducer<ArchiveEntry[]>
{
    private readonly ArchiveDocument _archive;
    public ListArchiveEntries([From] ArchiveDocument archive) => _archive = archive;
    public async ValueTask<ArchiveEntry[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var input = new MemoryStream(_archive.Data);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        var result = new List<ArchiveEntry>();
        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            await using Stream stream = entry.Open();
            await using var data = new MemoryStream();
            await stream.CopyToAsync(data, cancellationToken).ConfigureAwait(false);
            result.Add(new(entry.FullName, data.ToArray(), entry.Length));
        }
        return result.ToArray();
    }
}

[Verb("GET")]
[Qualifier("BINARY")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetArchiveEntryBinary : IVerb<byte[]>, IGet, IFrom<ArchiveEntry>, IPipelineConsumer<ArchiveEntry>, IPipelineProducer<byte[]>
{
    private readonly ArchiveEntry _entry; public GetArchiveEntryBinary([From] ArchiveEntry entry) => _entry = entry;
    public ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_entry.Data);
}
