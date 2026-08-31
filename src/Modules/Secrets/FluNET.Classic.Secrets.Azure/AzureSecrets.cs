using FluNET.Classic.Core;

namespace FluNET.Classic.Secrets.Azure;

public interface IKeyVaultClientAdapter
{
    ValueTask<string?> GetAsync(string name, CancellationToken cancellationToken = default);
    ValueTask SetAsync(string name, string value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(string name, CancellationToken cancellationToken = default);
}
public sealed class KeyVaultSecretProvider : ISecretProvider
{
    private readonly IKeyVaultClientAdapter _client; public KeyVaultSecretProvider(IKeyVaultClientAdapter client) => _client = client;
    public async ValueTask<SecretValue?> GetAsync(SecretName name, CancellationToken cancellationToken = default) => await _client.GetAsync(name.Value, cancellationToken).ConfigureAwait(false) is { } value ? new SecretValue(value) : null;
    public ValueTask SaveAsync(SecretName name, SecretValue value, CancellationToken cancellationToken = default) => _client.SetAsync(name.Value, value.Reveal().ToString(), cancellationToken);
    public ValueTask<bool> DeleteAsync(SecretName name, CancellationToken cancellationToken = default) => _client.DeleteAsync(name.Value, cancellationToken);
}
public sealed class AzureSecretsModule : LanguageModule { public override string Name => "secrets.azure"; public override IReadOnlyCollection<string> Dependencies => new[] { "secrets" }; }
