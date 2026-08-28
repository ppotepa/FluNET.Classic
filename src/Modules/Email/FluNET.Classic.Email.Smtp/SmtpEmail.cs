using FluNET.Classic.Core;
using FluNET.Classic.Email;

namespace FluNET.Classic.Email.Smtp;

public sealed record SmtpOptions(string Host, int Port = 587, bool UseTls = true, string? UserName = null, string? CredentialName = null);
public interface ISmtpClientAdapter { ValueTask SendAsync(SmtpOptions options, EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default); }
public sealed class SmtpEmailTransport : IEmailTransport
{
    private readonly ISmtpClientAdapter _client; private readonly SmtpOptions _options; public SmtpEmailTransport(ISmtpClientAdapter client, SmtpOptions options) { _client = client; _options = options; }
    public ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default) => _client.SendAsync(_options, to, message, cancellationToken);
}
public sealed class SmtpEmailModule : LanguageModule { public override string Name => "email.smtp"; public override IReadOnlyCollection<string> Dependencies => new[] { "email" }; }
