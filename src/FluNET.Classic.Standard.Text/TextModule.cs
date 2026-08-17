using System.Text;
using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Text;

public enum TextTarget { TEXT }
public enum BinaryTarget { BINARY }
public enum TextEncodingKind { UTF8, ASCII }

public interface IOutputWriter
{
    ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class TextModule : LanguageModule
{
    public override string Name => "text";
}

[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformText : Transform<string, string, string>
{
    public TransformText([What] string what, [Using] string @using) : base(what, @using) { }
    protected override ValueTask<string> TransformAsync(string what, string @using, CancellationToken cancellationToken) => ValueTask.FromResult(Apply(what, @using));
    internal static string Apply(string value, string operation) => operation.ToUpperInvariant() switch
    {
        "UPPER" => value.ToUpperInvariant(),
        "LOWER" => value.ToLowerInvariant(),
        "TRIM" => value.Trim(),
        "BASE64" => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
        "FROMBASE64" => Encoding.UTF8.GetString(Convert.FromBase64String(value)),
        _ => throw new InvalidOperationException($"Unknown text transform '{operation}'.")
    };
}

[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformLines : Transform<string[], string[], string>
{
    public TransformLines([What] string[] what, [Using] string @using) : base(what, @using) { }
    protected override ValueTask<string[]> TransformAsync(string[] what, string @using, CancellationToken cancellationToken) => ValueTask.FromResult(what.Select(x => TransformText.Apply(x, @using)).ToArray());
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformTextToBinary : TransformToUsing<byte[], string, BinaryTarget, TextEncodingKind>
{
    public TransformTextToBinary([What] string what, [To] BinaryTarget to, [Using] TextEncodingKind @using) : base(what, to, @using) { }
    protected override ValueTask<byte[]> TransformAsync(string what, BinaryTarget to, TextEncodingKind @using, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetEncoding(@using).GetBytes(what));

    private static Encoding GetEncoding(TextEncodingKind encoding) => encoding switch
    {
        TextEncodingKind.UTF8 => Encoding.UTF8,
        TextEncodingKind.ASCII => Encoding.ASCII,
        _ => throw new InvalidOperationException($"Unknown text encoding '{encoding}'.")
    };
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformBinaryToText : TransformToUsing<string, byte[], TextTarget, TextEncodingKind>
{
    public TransformBinaryToText([What] byte[] what, [To] TextTarget to, [Using] TextEncodingKind @using) : base(what, to, @using) { }
    protected override ValueTask<string> TransformAsync(byte[] what, TextTarget to, TextEncodingKind @using, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetEncoding(@using).GetString(what));

    private static Encoding GetEncoding(TextEncodingKind encoding) => encoding switch
    {
        TextEncodingKind.UTF8 => Encoding.UTF8,
        TextEncodingKind.ASCII => Encoding.ASCII,
        _ => throw new InvalidOperationException($"Unknown text encoding '{encoding}'.")
    };
}

[Qualifier("TEXT")]
public sealed class SayText : Say<string>
{
    private readonly IOutputWriter _writer;
    public SayText([What] string what, [FromServices] IOutputWriter writer) : base(what) => _writer = writer;
    protected override async ValueTask SayAsync(string what, CancellationToken cancellationToken) => await _writer.WriteLineAsync(what, cancellationToken).ConfigureAwait(false);
}
