using FluNET.Classic.Core;
using System.Security.Claims;

namespace FluNET.Classic.Identity;

public sealed record PrincipalInfo(string? Name, bool IsAuthenticated, IReadOnlyDictionary<string, string[]> Claims);
public interface IPrincipalProvider
{
    ClaimsPrincipal Current
    {
        get;
    }
}
public sealed class IdentityModule : LanguageModule
{
    public override string Name => "identity";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers =>
        new QualifierDescriptor[]
        {
            new("qualifier:principal", "PRINCIPAL", typeof(PrincipalInfo)),
            new("qualifier:principal-name", "NAME", typeof(string)),
            new("qualifier:principal-authenticated", "AUTHENTICATED", typeof(bool)),
            new("qualifier:principal-claims", "CLAIMS", typeof(IReadOnlyDictionary<string, string[]>))
        };
}

[Verb("GET")]
[Qualifier("NAME")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetPrincipalName : Get<string?, PrincipalInfo>
{
    public GetPrincipalName([From] PrincipalInfo from) : base(from) { }

    protected override ValueTask<string?> ActAsync(PrincipalInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Name);
}

[Verb("GET")]
[Qualifier("AUTHENTICATED")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetPrincipalAuthentication : Get<bool, PrincipalInfo>
{
    public GetPrincipalAuthentication([From] PrincipalInfo from) : base(from) { }

    protected override ValueTask<bool> ActAsync(PrincipalInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.IsAuthenticated);
}

[Verb("GET")]
[Qualifier("CLAIMS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetPrincipalClaims : Get<IReadOnlyDictionary<string, string[]>, PrincipalInfo>
{
    public GetPrincipalClaims([From] PrincipalInfo from) : base(from) { }

    protected override ValueTask<IReadOnlyDictionary<string, string[]>> ActAsync(PrincipalInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Claims);
}

[Qualifier("PRINCIPAL")]
[RequiresCapability(StandardCapabilities.IdentityRead)]
public sealed class GetPrincipal : IQuery<PrincipalInfo>, IPipelineProducer<PrincipalInfo>
{
    private readonly IPrincipalProvider _provider;

    public GetPrincipal([FromServices] IPrincipalProvider provider) => _provider = provider;

    public ValueTask<PrincipalInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal principal = _provider.Current;
        var claims = principal.Claims
            .GroupBy(claim => claim.Type)
            .ToDictionary(group => group.Key, group => group.Select(claim => claim.Value).ToArray());

        return ValueTask.FromResult(new PrincipalInfo(
            principal.Identity?.Name,
            principal.Identity?.IsAuthenticated == true,
            claims));
    }
}
