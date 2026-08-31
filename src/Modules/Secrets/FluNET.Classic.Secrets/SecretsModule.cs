using FluNET.Classic.Core;

namespace FluNET.Classic.Secrets;

public sealed record SecretName(string Value)
{
    public override string ToString() => Value;
}
public sealed class SecretValue : ISensitiveValue
{
    private readonly string _value; public SecretValue(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));
    public string RedactedText => "***";
    public ReadOnlyMemory<char> Reveal() => _value.AsMemory();
    public override string ToString() => RedactedText;
}

public interface ISecretProvider
{
    ValueTask<SecretValue?> GetAsync(SecretName name, CancellationToken cancellationToken = default);
    ValueTask SaveAsync(SecretName name, SecretValue value, CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(SecretName name, CancellationToken cancellationToken = default);
}

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    public ValueTask<SecretValue?> GetAsync(SecretName name, CancellationToken cancellationToken = default) => ValueTask.FromResult(Environment.GetEnvironmentVariable(name.Value) is { } value ? new SecretValue(value) : null);
    public ValueTask SaveAsync(SecretName name, SecretValue value, CancellationToken cancellationToken = default)
    {
        Environment.SetEnvironmentVariable(name.Value, value.Reveal().ToString());
        return ValueTask.CompletedTask;
    }
    public ValueTask<bool> DeleteAsync(SecretName name, CancellationToken cancellationToken = default)
    {
        bool exists = Environment.GetEnvironmentVariable(name.Value) is not null;
        Environment.SetEnvironmentVariable(name.Value, null);
        return ValueTask.FromResult(exists);
    }
}

public sealed class SecretsModule : LanguageModule
{
    public override string Name => "secrets"; public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:secret", "SECRET", typeof(SecretValue)) };
}

[Verb("GET")]
[Qualifier("SECRET")]
[RequiresCapability(StandardCapabilities.SecretsRead)]
public sealed class GetSecret : IVerb<SecretValue?>, IGet, IFrom<SecretName>, IPipelineProducer<SecretValue?>
{
    private readonly SecretName _name; private readonly ISecretProvider _provider; public GetSecret([From] SecretName name, [FromServices] ISecretProvider provider)
    {
        _name = name;
        _provider = provider;
    }
    public ValueTask<SecretValue?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _provider.GetAsync(_name, cancellationToken);
}

[Verb("SAVE")]
[Qualifier("SECRET")]
[RequiresCapability(StandardCapabilities.SecretsWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class SaveSecret : IVerb<SecretValue>, ISave, IWhat<SecretValue>, ITo<SecretName>, IPipelineConsumer<SecretValue>, IPipelineProducer<SecretValue>
{
    private readonly SecretValue _value; private readonly SecretName _name; private readonly ISecretProvider _provider; public SaveSecret([What] SecretValue value, [To] SecretName name, [FromServices] ISecretProvider provider)
    {
        _value = value;
        _name = name;
        _provider = provider;
    }
    public async ValueTask<SecretValue> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        await _provider.SaveAsync(_name, _value, cancellationToken).ConfigureAwait(false);
        return _value;
    }
}

[Verb("DELETE")]
[Qualifier("SECRET")]
[RequiresCapability(StandardCapabilities.SecretsWrite)]
[ExecutionTrait(ExecutionTrait.SideEffecting)]
public sealed class DeleteSecret : IVerb<bool>, IDelete, IAt<SecretName>, IPipelineProducer<bool>
{
    private readonly SecretName _name; private readonly ISecretProvider _provider; public DeleteSecret([At] SecretName name, [FromServices] ISecretProvider provider)
    {
        _name = name;
        _provider = provider;
    }
    public ValueTask<bool> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => _provider.DeleteAsync(_name, cancellationToken);
}
