using FluNET.Classic.Core;

namespace FluNET.Classic.Email;

public sealed record EmailAddress
{
    public EmailAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            throw new FormatException($"Invalid email address '{value}'.");
        Value = value;
    }
    public string Value
    {
        get;
    }
    public override string ToString() => Value;
}
public sealed record EmailAttachment(string Name, byte[] Data, string? ContentType = null);
public sealed record EmailMessage(string Subject, string Body, bool IsHtml = false, IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailTransport
{
    ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default);
}
public sealed class EmailModule : LanguageModule
{
    public override string Name => "email"; public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:email", "EMAIL", typeof(EmailMessage)) };
}

[Verb("SEND")]
[Qualifier("EMAIL")]
[RequiresCapability(StandardCapabilities.EmailSend)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SendEmailMessage : IVerb<EmailMessage>, ISend, IWhat<EmailMessage>, ITo<EmailAddress>, IPipelineConsumer<EmailMessage>, IPipelineProducer<EmailMessage>
{
    private readonly EmailMessage _message; private readonly EmailAddress _to; private readonly IEmailTransport _transport;
    public SendEmailMessage([What] EmailMessage message, [To] EmailAddress to, [FromServices] IEmailTransport transport)
    {
        _message = message;
        _to = to;
        _transport = transport;
    }
    public async ValueTask<EmailMessage> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _transport.SendAsync(_to, _message, cancellationToken).ConfigureAwait(false);
        return _message;
    }
}
