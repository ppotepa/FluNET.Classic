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
    public override string Name => "identity"; public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:principal", "PRINCIPAL", typeof(PrincipalInfo)) };
}

[Verb("GET")]
[Qualifier("PRINCIPAL")]
[RequiresCapability(StandardCapabilities.IdentityRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetPrincipal : IVerb<PrincipalInfo>, IGet, IWhat<PrincipalInfo>, IPipelineProducer<PrincipalInfo>
{
    private readonly IPrincipalProvider _provider; public GetPrincipal([What] PrincipalInfo what, [FromServices] IPrincipalProvider provider) => _provider = provider;
    public ValueTask<PrincipalInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        ClaimsPrincipal p = _provider.Current;
        var claims = p.Claims.GroupBy(x => x.Type).ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToArray());
        return ValueTask.FromResult(new PrincipalInfo(p.Identity?.Name, p.Identity?.IsAuthenticated == true, claims));
    }
}
