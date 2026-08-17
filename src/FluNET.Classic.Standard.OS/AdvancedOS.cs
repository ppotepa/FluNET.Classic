using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.OS;

public sealed record EnvironmentVariable(string Name, string? Value);

[Verb("LIST"), Qualifier("ENV"), RequiresCapability(StandardCapabilities.EnvironmentRead), ExecutionTrait(ExecutionTrait.Idempotent)]
public sealed class ListEnvironmentVariables : IVerb<EnvironmentVariable[]>, IListVerb, IWhat<EnvironmentVariable[]>, IPipelineProducer<EnvironmentVariable[]>
{
    public ListEnvironmentVariables([What] EnvironmentVariable[] what) { }
    public ValueTask<EnvironmentVariable[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        var result = new List<EnvironmentVariable>(); foreach (System.Collections.DictionaryEntry item in Environment.GetEnvironmentVariables()) result.Add(new(item.Key?.ToString() ?? string.Empty, item.Value?.ToString())); return ValueTask.FromResult(result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
