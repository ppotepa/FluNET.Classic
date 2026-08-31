using FluNET.Classic.Core;

namespace FluNET.Classic.Email.Graph;

public interface IGraphMailClientAdapter { ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default); }
public sealed class GraphEmailTransport : IEmailTransport
{
    private readonly IGraphMailClientAdapter _client; public GraphEmailTransport(IGraphMailClientAdapter client) => _client = client;
    public ValueTask SendAsync(EmailAddress to, EmailMessage message, CancellationToken cancellationToken = default) => _client.SendAsync(to, message, cancellationToken);
}
public sealed class GraphEmailModule : LanguageModule { public override string Name => "email.graph"; public override IReadOnlyCollection<string> Dependencies => new[] { "email" }; }
