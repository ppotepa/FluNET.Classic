using FluNET.Classic.Core;
using FluNET.Classic.Standard.Files;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class FileMetadataProjectionTests
{
    [Test]
    public async Task Metadata_projections_expose_typed_file_properties()
    {
        var metadata = new FileMetadata(
            "readme.md",
            @"C:\docs\readme.md",
            42,
            ".md",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            true);
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetFileLength(metadata).ExecuteAsync(context), Is.EqualTo(42));
        Assert.That(await new GetFileExtension(metadata).ExecuteAsync(context), Is.EqualTo(".md"));
        Assert.That(await new GetFileReadOnly(metadata).ExecuteAsync(context), Is.True);
    }
}
