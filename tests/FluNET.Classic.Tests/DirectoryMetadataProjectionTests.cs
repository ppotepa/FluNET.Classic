using FluNET.Classic.Core;
using FluNET.Classic.Standard.Files;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class DirectoryMetadataProjectionTests
{
    [Test]
    public async Task Directory_metadata_projections_expose_counts_and_existence()
    {
        var metadata = new DirectoryMetadata(
            "docs",
            @"C:\docs",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            4,
            2,
            true);
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetDirectoryFileCount(metadata).ExecuteAsync(context), Is.EqualTo(4));
        Assert.That(await new GetDirectoryCount(metadata).ExecuteAsync(context), Is.EqualTo(2));
        Assert.That(await new GetDirectoryExists(metadata).ExecuteAsync(context), Is.True);
    }
}
