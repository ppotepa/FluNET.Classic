using FluNET.Classic.Core;
using System.Text;

namespace FluNET.Classic.Standard.Text;

public enum TextTarget
{
    TEXT
}
public enum BinaryTarget
{
    BINARY
}
public enum TextEncodingKind
{
    UTF8, ASCII
}
public enum TextOperation
{
    UPPER, LOWER, TRIM, BASE64, FROMBASE64
}
public enum SplitStrategy
{
    SPLIT
}
public enum JoinStrategy
{
    JOIN
}
public enum ReplaceStrategy
{
    REPLACE
}

public sealed record TextReplacement(string OldValue, string NewValue)
{
    public static bool TryParse(string value, out TextReplacement? result)
    {
        int separator = value.IndexOf("=>", StringComparison.Ordinal);
        if (separator < 0)
        {
            result = null;
            return false;
        }
        result = new(value[..separator].Trim(), value[(separator + 2)..].Trim());
        return true;
    }
}

public interface IOutputWriter
{
    ValueTask WriteLineAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class TextModule : LanguageModule
{
    public override string Name => "text";
    public override IReadOnlyCollection<QualifierDescriptor> Qualifiers => new[] { new QualifierDescriptor("qualifier:lines", "LINES", typeof(string[])) };
}

[Qualifier("TEXT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformText : Transform<string, string, TextOperation>
{
    public TransformText([What] string what, [Using] TextOperation @using) : base(what, @using) { }
    protected override ValueTask<string> TransformAsync(string what, TextOperation @using, CancellationToken cancellationToken) => ValueTask.FromResult(Apply(what, @using));
    internal static string Apply(string value, TextOperation operation) => operation switch
    {
        TextOperation.UPPER => value.ToUpperInvariant(),
        TextOperation.LOWER => value.ToLowerInvariant(),
        TextOperation.TRIM => value.Trim(),
        TextOperation.BASE64 => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)),
        TextOperation.FROMBASE64 => Encoding.UTF8.GetString(Convert.FromBase64String(value)),
        _ => throw new InvalidOperationException($"Unknown text transform '{operation}'.")
    };
}

[Qualifier("TEXT"), ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformLines : Transform<string[], string[], TextOperation>
{
    public TransformLines([What] string[] what, [Using] TextOperation @using) : base(what, @using) { }
    protected override ValueTask<string[]> TransformAsync(string[] what, TextOperation @using, CancellationToken cancellationToken) => ValueTask.FromResult(what.Select(x => TransformText.Apply(x, @using)).ToArray());
}

[Verb("TRANSFORM")]
[Qualifier("LINES")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class SplitText : IVerb<string[]>, ITransform, IWhat<string>, IUsing<SplitStrategy>, IWith<string>, IPipelineConsumer<string>, IPipelineProducer<string[]>
{
    private readonly string _text; private readonly string _separator;
    public SplitText([What] string text, [Using] SplitStrategy operation, [With] string separator)
    {
        _text = text;
        _separator = separator;
    }
    public ValueTask<string[]> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_text.Split(_separator, StringSplitOptions.None));
}

[Verb("TRANSFORM")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class JoinLines : IVerb<string>, ITransform, IWhat<string[]>, IUsing<JoinStrategy>, IWith<string>, IPipelineConsumer<string[]>, IPipelineProducer<string>
{
    private readonly string[] _lines; private readonly string _separator;
    public JoinLines([What] string[] lines, [Using] JoinStrategy operation, [With] string separator)
    {
        _lines = lines;
        _separator = separator;
    }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(string.Join(_separator, _lines));
}

[Verb("TRANSFORM")]
[Qualifier("TEXT")]
[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class ReplaceText : IVerb<string>, ITransform, IWhat<string>, IUsing<ReplaceStrategy>, IWith<TextReplacement>, IPipelineConsumer<string>, IPipelineProducer<string>
{
    private readonly string _text; private readonly TextReplacement _replacement;
    public ReplaceText([What] string text, [Using] ReplaceStrategy operation, [With] TextReplacement replacement)
    {
        _text = text;
        _replacement = replacement;
    }
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) => ValueTask.FromResult(_text.Replace(_replacement.OldValue, _replacement.NewValue, StringComparison.Ordinal));
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformTextToBinary : TransformToUsing<byte[], string, BinaryTarget, TextEncodingKind>
{
    public TransformTextToBinary([What] string what, [To] BinaryTarget to, [Using] TextEncodingKind @using) : base(what, to, @using) { }
    protected override ValueTask<byte[]> TransformAsync(string what, BinaryTarget to, TextEncodingKind @using, CancellationToken cancellationToken) => ValueTask.FromResult(GetEncoding(@using).GetBytes(what));
    private static Encoding GetEncoding(TextEncodingKind encoding) => encoding switch { TextEncodingKind.UTF8 => Encoding.UTF8, TextEncodingKind.ASCII => Encoding.ASCII, _ => throw new InvalidOperationException($"Unknown text encoding '{encoding}'.") };
}

[ExecutionTrait(ExecutionTrait.Pure)]
public sealed class TransformBinaryToText : TransformToUsing<string, byte[], TextTarget, TextEncodingKind>
{
    public TransformBinaryToText([What] byte[] what, [To] TextTarget to, [Using] TextEncodingKind @using) : base(what, to, @using) { }
    protected override ValueTask<string> TransformAsync(byte[] what, TextTarget to, TextEncodingKind @using, CancellationToken cancellationToken) => ValueTask.FromResult(GetEncoding(@using).GetString(what));
    private static Encoding GetEncoding(TextEncodingKind encoding) => encoding switch { TextEncodingKind.UTF8 => Encoding.UTF8, TextEncodingKind.ASCII => Encoding.ASCII, _ => throw new InvalidOperationException($"Unknown text encoding '{encoding}'.") };
}

[Qualifier("TEXT")]
public sealed class SayText : Say<string>
{
    private readonly IOutputWriter _writer; public SayText([What] string what, [FromServices] IOutputWriter writer) : base(what) => _writer = writer;
    protected override async ValueTask SayAsync(string what, CancellationToken cancellationToken) => await _writer.WriteLineAsync(what, cancellationToken).ConfigureAwait(false);
}
