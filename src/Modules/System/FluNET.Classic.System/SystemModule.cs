using FluNET.Classic.Core;

namespace FluNET.Classic.System;

public sealed record SystemMemory(long WorkingSetBytes, long GcMemoryBytes);
public sealed record RuntimeInfo(string Framework, string Architecture, string OSArchitecture, int ProcessorCount);
public sealed class SystemModule : LanguageModule
{
    public override string Name => "system";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[] { new("qualifier:memory", "MEMORY", typeof(SystemMemory)), new("qualifier:runtime", "RUNTIME", typeof(RuntimeInfo)), new("qualifier:hostname", "HOSTNAME", typeof(string)) };
}

[Qualifier("MEMORY")]
[RequiresCapability(StandardCapabilities.SystemRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetSystemMemory : IQuery<SystemMemory>, IPipelineProducer<SystemMemory>
{
    public ValueTask<SystemMemory> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new SystemMemory(global::System.Environment.WorkingSet, GC.GetTotalMemory(false)));
}

[Qualifier("RUNTIME")]
[RequiresCapability(StandardCapabilities.SystemRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetRuntimeInfo : IQuery<RuntimeInfo>, IPipelineProducer<RuntimeInfo>
{
    public ValueTask<RuntimeInfo> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(new RuntimeInfo(global::System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, global::System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), global::System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(), global::System.Environment.ProcessorCount));
}

[Qualifier("HOSTNAME")]
[RequiresCapability(StandardCapabilities.SystemRead)]
[ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class GetHostName : IQuery<string>, IPipelineProducer<string>
{
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(global::System.Environment.MachineName);
}
