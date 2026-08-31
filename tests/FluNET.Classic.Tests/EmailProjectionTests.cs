using FluNET.Classic.Email;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class EmailProjectionTests
{
    [Test]
    public async Task Email_address_and_message_projections_return_typed_fields()
    {
        var address = new EmailAddress("ada@example.test");
        var attachments = new[] { new EmailAttachment("note.txt", new byte[] { 1 }) };
        var message = new EmailMessage("Hello", "Body", true, attachments);

        Assert.That(await new GetEmailAddressValue(address).ExecuteAsync(null!), Is.EqualTo("ada@example.test"));
        Assert.That(await new GetEmailSubject(message).ExecuteAsync(null!), Is.EqualTo("Hello"));
        Assert.That(await new GetEmailBody(message).ExecuteAsync(null!), Is.EqualTo("Body"));
        Assert.That(await new GetEmailHtml(message).ExecuteAsync(null!), Is.True);
        Assert.That(await new GetEmailAttachments(message).ExecuteAsync(null!), Is.EqualTo(attachments));
    }
}
