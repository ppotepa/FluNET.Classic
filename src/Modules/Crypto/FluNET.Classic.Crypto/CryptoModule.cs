using FluNET.Classic.Core;
using System.Security.Cryptography;

namespace FluNET.Classic.Crypto;

public enum HashAlgorithmKind { SHA256, SHA384, SHA512 }
public enum BinaryEncoding { BASE64, HEX }
public sealed record HashValue(byte[] Bytes, HashAlgorithmKind Algorithm) : IValidState { public bool IsValid => Bytes.Length > 0; public override string ToString() => Convert.ToHexString(Bytes); }

public sealed class CryptoModule : LanguageModule
{
    public override string Name => "crypto";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:hash", "HASH", typeof(HashValue)) };
}

[Verb("TRANSFORM"), Qualifier("HASH"), ExecutionTrait(ExecutionTrait.Pure), RequiresCapability(StandardCapabilities.Crypto)]
public sealed class HashBinary : IVerb<HashValue>, ITransform, IWhat<byte[]>, IUsing<HashAlgorithmKind>, IPipelineConsumer<byte[]>, IPipelineProducer<HashValue>
{
    private readonly byte[] _data; private readonly HashAlgorithmKind _algorithm;
    public HashBinary([What] byte[] data, [Using] HashAlgorithmKind algorithm) { _data = data; _algorithm = algorithm; }
    public ValueTask<HashValue> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default)
    {
        byte[] hash = _algorithm switch { HashAlgorithmKind.SHA256 => SHA256.HashData(_data), HashAlgorithmKind.SHA384 => SHA384.HashData(_data), HashAlgorithmKind.SHA512 => SHA512.HashData(_data), _ => throw new NotSupportedException() };
        return ValueTask.FromResult(new HashValue(hash, _algorithm));
    }
}

[Verb("FORMAT"), Qualifier("TEXT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class FormatHash : IVerb<string>, IFormat, IWhat<HashValue>, IUsing<BinaryEncoding>, IPipelineConsumer<HashValue>, IPipelineProducer<string>
{
    private readonly HashValue _hash; private readonly BinaryEncoding _encoding;
    public FormatHash([What] HashValue hash, [Using] BinaryEncoding encoding) { _hash = hash; _encoding = encoding; }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_encoding == BinaryEncoding.BASE64 ? Convert.ToBase64String(_hash.Bytes) : Convert.ToHexString(_hash.Bytes));
}

[Verb("TRANSFORM"), Qualifier("BINARY"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class DecodeText : IVerb<byte[]>, ITransform, IWhat<string>, IUsing<BinaryEncoding>, IPipelineConsumer<string>, IPipelineProducer<byte[]>
{
    private readonly string _text; private readonly BinaryEncoding _encoding;
    public DecodeText([What] string text, [Using] BinaryEncoding encoding) { _text = text; _encoding = encoding; }
    public ValueTask<byte[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_encoding == BinaryEncoding.BASE64 ? Convert.FromBase64String(_text) : Convert.FromHexString(_text));
}
