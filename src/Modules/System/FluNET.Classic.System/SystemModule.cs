using FluNET.Classic.Core;

namespace FluNET.Classic.System;

public sealed record SystemMemory(long WorkingSetBytes, long GcMemoryBytes);
public sealed record RuntimeInfo(string Framework, string Architecture, string OSArchitecture, int ProcessorCount);
public sealed class SystemModule : LanguageModule
{
    public override string Name => "system";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[]
    {
        new("qualifier:memory", "MEMORY", typeof(SystemMemory)),
        new("qualifier:runtime", "RUNTIME", typeof(RuntimeInfo)),
        new("qualifier:hostname", "HOSTNAME", typeof(string)),
        new("qualifier:working-set", "WORKINGSET", typeof(long)),
        new("qualifier:gc-memory", "GCMEMORY", typeof(long)),
        new("qualifier:runtime-framework", "FRAMEWORK", typeof(string)),
        new("qualifier:runtime-architecture", "ARCHITECTURE", typeof(string)),
        new("qualifier:runtime-os-architecture", "OSARCHITECTURE", typeof(string)),
        new("qualifier:processor-count", "PROCESSORS", typeof(int))
    };
}

[Verb("GET")]
[Qualifier("WORKINGSET")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetWorkingSetMemory : Get<long, SystemMemory>
{
    public GetWorkingSetMemory([From] SystemMemory from) : base(from) { }

    protected override ValueTask<long> ActAsync(SystemMemory from, CancellationToken cancellationToken) => ValueTask.FromResult(from.WorkingSetBytes);
}

[Verb("GET")]
[Qualifier("GCMEMORY")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetGcMemory : Get<long, SystemMemory>
{
    public GetGcMemory([From] SystemMemory from) : base(from) { }

    protected override ValueTask<long> ActAsync(SystemMemory from, CancellationToken cancellationToken) => ValueTask.FromResult(from.GcMemoryBytes);
}

[Verb("GET")]
[Qualifier("FRAMEWORK")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRuntimeFramework : Get<string, RuntimeInfo>
{
    public GetRuntimeFramework([From] RuntimeInfo from) : base(from) { }

    protected override ValueTask<string> ActAsync(RuntimeInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Framework);
}

[Verb("GET")]
[Qualifier("ARCHITECTURE")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRuntimeArchitecture : Get<string, RuntimeInfo>
{
    public GetRuntimeArchitecture([From] RuntimeInfo from) : base(from) { }

    protected override ValueTask<string> ActAsync(RuntimeInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.Architecture);
}

[Verb("GET")]
[Qualifier("OSARCHITECTURE")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetRuntimeOsArchitecture : Get<string, RuntimeInfo>
{
    public GetRuntimeOsArchitecture([From] RuntimeInfo from) : base(from) { }

    protected override ValueTask<string> ActAsync(RuntimeInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.OSArchitecture);
}

[Verb("GET")]
[Qualifier("PROCESSORS")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class GetProcessorCount : Get<int, RuntimeInfo>
{
    public GetProcessorCount([From] RuntimeInfo from) : base(from) { }

    protected override ValueTask<int> ActAsync(RuntimeInfo from, CancellationToken cancellationToken) => ValueTask.FromResult(from.ProcessorCount);
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
