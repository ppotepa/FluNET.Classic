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
        new[] { new QualifierDescriptor("qualifier:principal", "PRINCIPAL", typeof(PrincipalInfo)) };
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
