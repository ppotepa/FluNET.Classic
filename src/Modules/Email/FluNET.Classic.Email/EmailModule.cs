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
    public override string Name => "email";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:email", "EMAIL", typeof(EmailMessage)),
        new("qualifier:email-address", "ADDRESS", typeof(string)),
        new("qualifier:email-subject", "SUBJECT", typeof(string)),
        new("qualifier:email-body", "BODY", typeof(string)),
        new("qualifier:email-html", "HTML", typeof(bool)),
        new("qualifier:email-attachments", "ATTACHMENTS", typeof(EmailAttachment[]))
    };
}

[Verb("GET")]
[Qualifier("ADDRESS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEmailAddressValue : Get<string, EmailAddress>
{
    public GetEmailAddressValue([From] EmailAddress from) : base(from) { }

    protected override ValueTask<string> ActAsync(EmailAddress from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Value);
}

[Verb("GET")]
[Qualifier("SUBJECT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEmailSubject : Get<string, EmailMessage>
{
    public GetEmailSubject([From] EmailMessage from) : base(from) { }

    protected override ValueTask<string> ActAsync(EmailMessage from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Subject);
}

[Verb("GET")]
[Qualifier("BODY")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEmailBody : Get<string, EmailMessage>
{
    public GetEmailBody([From] EmailMessage from) : base(from) { }

    protected override ValueTask<string> ActAsync(EmailMessage from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Body);
}

[Verb("GET")]
[Qualifier("HTML")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEmailHtml : Get<bool, EmailMessage>
{
    public GetEmailHtml([From] EmailMessage from) : base(from) { }

    protected override ValueTask<bool> ActAsync(EmailMessage from, CancellationToken cancellationToken) => ValueTask.FromResult(from.IsHtml);
}

[Verb("GET")]
[Qualifier("ATTACHMENTS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetEmailAttachments : Get<EmailAttachment[], EmailMessage>
{
    public GetEmailAttachments([From] EmailMessage from) : base(from) { }

    protected override ValueTask<EmailAttachment[]> ActAsync(EmailMessage from, CancellationToken cancellationToken) => ValueTask.FromResult((from.Attachments ?? Array.Empty<EmailAttachment>()).ToArray());
}

[Verb("SEND")]
[Qualifier("EMAIL")]
[RequiresCapability(StandardCapabilities.EmailSend)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
[ExecutionTrait(ExecutionTrait.LongRunning)]
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
