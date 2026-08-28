using FluNET.Classic.Core;

namespace FluNET.Classic.System;

public sealed record SystemMemory(long WorkingSetBytes, long GcMemoryBytes);
public sealed record RuntimeInfo(string Framework, string Architecture, string OSArchitecture, int ProcessorCount);
public sealed class SystemModule : LanguageModule
{
    public override string Name => "system";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[] { new("qualifier:memory", "MEMORY", typeof(SystemMemory)), new("qualifier:runtime", "RUNTIME", typeof(RuntimeInfo)), new("qualifier:hostname", "HOSTNAME", typeof(string)) };
}

[Verb("GET"), Qualifier("MEMORY"), RequiresCapability(StandardCapabilities.SystemRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetSystemMemory : IVerb<SystemMemory>, IGet, IWhat<SystemMemory>, IPipelineProducer<SystemMemory>
{
    public GetSystemMemory([What] SystemMemory what) { }
    public ValueTask<SystemMemory> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new SystemMemory(global::System.Environment.WorkingSet, GC.GetTotalMemory(false)));
}

[Verb("GET"), Qualifier("RUNTIME"), RequiresCapability(StandardCapabilities.SystemRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetRuntimeInfo : IVerb<RuntimeInfo>, IGet, IWhat<RuntimeInfo>, IPipelineProducer<RuntimeInfo>
{
    public GetRuntimeInfo([What] RuntimeInfo what) { }
    public ValueTask<RuntimeInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new RuntimeInfo(global::System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, global::System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), global::System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(), global::System.Environment.ProcessorCount));
}

[Verb("GET"), Qualifier("HOSTNAME"), RequiresCapability(StandardCapabilities.SystemRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetHostName : IVerb<string>, IGet, IWhat<string>, IPipelineProducer<string>
{
    public GetHostName([What] string what) { }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(global::System.Environment.MachineName);
}
