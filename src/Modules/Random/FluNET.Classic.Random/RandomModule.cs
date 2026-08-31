using FluNET.Classic.Core;
using System.Security.Cryptography;

namespace FluNET.Classic.Random;

public enum RandomSource { RANDOM, SECURE }
public sealed record NumberRange(int Minimum, int Maximum);
public sealed class RandomModule : LanguageModule
{
    public override string Name => "random";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new QualifierDescriptor[] { new("qualifier:guid", "GUID", typeof(Guid)), new("qualifier:random-bytes", "BYTES", typeof(byte[])) };
}

[Verb("GET"), Qualifier("NUMBER"), ExecutionTrait(ExecutionTrait.NonDeterministic)]
public sealed class GetRandomNumber : IVerb<decimal>, IGet, IFrom<RandomSource>, IWith<NumberRange>, IPipelineProducer<decimal>
{
    private readonly RandomSource _source; private readonly NumberRange? _range;
    public GetRandomNumber([From] RandomSource source, [With] NumberRange? range = null) { _source = source; _range = range; }
    public ValueTask<decimal> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        int min = _range?.Minimum ?? 0, max = _range?.Maximum ?? int.MaxValue; int value = _source == RandomSource.SECURE ? RandomNumberGenerator.GetInt32(min, max) : System.Random.Shared.Next(min, max); return ValueTask.FromResult((decimal)value);
    }
}

[Verb("GET"), Qualifier("GUID"), ExecutionTrait(ExecutionTrait.NonDeterministic)]
public sealed class GetRandomGuid : IVerb<Guid>, IGet, IFrom<RandomSource>, IPipelineProducer<Guid>
{
    private readonly RandomSource _source; public GetRandomGuid([From] RandomSource source) => _source = source;
    public ValueTask<Guid> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(Guid.NewGuid());
}

[Verb("GET"), Qualifier("BYTES"), ExecutionTrait(ExecutionTrait.NonDeterministic)]
public sealed class GetRandomBytes : IVerb<byte[]>, IGet, IFrom<RandomSource>, IWith<int>, IPipelineProducer<byte[]>
{
    private readonly RandomSource _source; private readonly int _length; public GetRandomBytes([From] RandomSource source, [With] int length) { _source = source; _length = length; }
    public ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) { byte[] data = new byte[_length]; if (_source == RandomSource.SECURE) RandomNumberGenerator.Fill(data); else System.Random.Shared.NextBytes(data); return ValueTask.FromResult(data); }
}
