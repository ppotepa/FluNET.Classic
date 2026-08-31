using FluNET.Classic.Core;

namespace FluNET.Classic.Secrets.Aws;

public interface ISecretsManagerClientAdapter
{
    ValueTask<string?> GetAsync(string name, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string name, string value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
public sealed class SecretsManagerProvider : ISecretProvider
{
    private readonly ISecretsManagerClientAdapter _client; public SecretsManagerProvider(ISecretsManagerClientAdapter client) => _client = client;
    public async ValueTask<SecretValue?> GetAsync(SecretName name, CancellationToken cancellationToken = default) => await _client.GetAsync(name.Value, cancellationToken).ConfigureAwait(false) is { } value ? new SecretValue(value) : null;
    public ValueTask SaveAsync(SecretName name, SecretValue value, CancellationToken cancellationToken = default) => _client.SetAsync(name.Value, value.Reveal().ToString(), cancellationToken);
    public ValueTask<bool> DeleteAsync(SecretName name, CancellationToken cancellationToken = default) => _client.DeleteAsync(name.Value, cancellationToken);
}
public sealed class AwsSecretsModule : LanguageModule
{
    public override string Name => "secrets.aws"; public override IReadOnlyCollection<string> Dependencies => new[] { "secrets" };
}
