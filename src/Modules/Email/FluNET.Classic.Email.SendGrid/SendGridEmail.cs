using FluNET.Classic.Core;
using FluNET.Classic.Email;

namespace FluNET.Classic.Email.SendGrid;

public interface ISendGridClientAdapter { ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default); }
public sealed class SendGridEmailTransport : IEmailTransport
{
    private readonly ISendGridClientAdapter _client; public SendGridEmailTransport(ISendGridClientAdapter client) => _client = client;
    public ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default) => _client.SendAsync(to, message, cancellationToken);
}
public sealed class SendGridEmailModule : LanguageModule { public override string Name => "email.sendgrid"; public override IReadOnlyCollection<string> Dependencies => new[] { "email" }; }
