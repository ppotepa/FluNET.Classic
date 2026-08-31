using FluNET.Classic.Core;
using System.Runtime.InteropServices;

namespace FluNET.Classic.Standard.OS;

public sealed record OperatingSystemInfo(string Description, Architecture Architecture, string FrameworkDescription);
public sealed record CurrentUserInfo(string Name, string? Domain);
public sealed record WorkingDirectory(string Path);

public sealed class OSModule : LanguageModule
{
    public override string Name => "os";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:env", "ENV", typeof(string)),
        new("qualifier:os", "OS", typeof(OperatingSystemInfo)),
        new("qualifier:user", "USER", typeof(CurrentUserInfo)),
        new("qualifier:cwd", "CWD", typeof(WorkingDirectory), new[] { "WORKDIR" })
    };
}

[Verb("GET")]
[Qualifier("ENV")]
[RequiresCapability(StandardCapabilities.EnvironmentRead)]
public sealed class GetEnvironmentVariable : IVerb<string?>, IGet, IWhat<string?>, IFrom<EnvironmentVariableName>, IPipelineProducer<string?>
{
    private readonly EnvironmentVariableName _name;
    public GetEnvironmentVariable([What] string? what, [From] EnvironmentVariableName name) => _name = name;
    public ValueTask<string?> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(Environment.GetEnvironmentVariable(_name.Value));
}

[Qualifier("ENV")]
[RequiresCapability(StandardCapabilities.EnvironmentWrite)]
public sealed class SaveEnvironmentVariable : Save<string, EnvironmentVariableName>
{
    public SaveEnvironmentVariable([What] string value, [To] EnvironmentVariableName name) : base(value, name) { }
    protected override ValueTask SaveAsync(string what, EnvironmentVariableName to, CancellationToken cancellationToken)
    {
        Environment.SetEnvironmentVariable(to.Value, what);
        return ValueTask.CompletedTask;
    }
}

[Verb("GET")]
[Qualifier("OS")]
[RequiresCapability(StandardCapabilities.SystemRead)]
public sealed class GetOperatingSystem : IVerb<OperatingSystemInfo>, IGet, IWhat<OperatingSystemInfo>, IPipelineProducer<OperatingSystemInfo>
{
    public GetOperatingSystem([What] OperatingSystemInfo what) { }
    public ValueTask<OperatingSystemInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new OperatingSystemInfo(RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture, RuntimeInformation.FrameworkDescription));
}

[Verb("GET")]
[Qualifier("USER")]
[RequiresCapability(StandardCapabilities.SystemRead)]
public sealed class GetCurrentUser : IVerb<CurrentUserInfo>, IGet, IWhat<CurrentUserInfo>, IPipelineProducer<CurrentUserInfo>
{
    public GetCurrentUser([What] CurrentUserInfo what) { }
    public ValueTask<CurrentUserInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new CurrentUserInfo(Environment.UserName, Environment.UserDomainName));
}

[Verb("GET")]
[Qualifier("CWD")]
[RequiresCapability(StandardCapabilities.SystemRead)]
public sealed class GetWorkingDirectory : IVerb<WorkingDirectory>, IGet, IWhat<WorkingDirectory>, IPipelineProducer<WorkingDirectory>
{
    public GetWorkingDirectory([What] WorkingDirectory what) { }
    public ValueTask<WorkingDirectory> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new WorkingDirectory(Environment.CurrentDirectory));
}
