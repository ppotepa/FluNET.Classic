using FluNET.Classic.Archive;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ArchiveProjectionTests
{
    [Test]
    public async Task Archive_document_and_entry_projections_return_typed_fields()
    {
        var document = new ArchiveDocument(new byte[] { 1, 2 }, CompressionFormat.ZIP);
        var entry = new ArchiveEntry("readme.txt", new byte[] { 1, 2, 3 }, 3);

        Assert.That(await new GetArchiveFormat(document).ExecuteAsync(null!), Is.EqualTo(CompressionFormat.ZIP));
        Assert.That(await new GetArchiveEntryName(entry).ExecuteAsync(null!), Is.EqualTo("readme.txt"));
        Assert.That(await new GetArchiveEntryLength(entry).ExecuteAsync(null!), Is.EqualTo(3));
    }
}
