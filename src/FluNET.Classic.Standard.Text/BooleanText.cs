using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Text;

[Qualifier("BOOLEAN")]
public sealed class SayBoolean : Say<bool>
{
    private readonly IOutputWriter _writer;
    public SayBoolean([What] bool what, [FromServices] IOutputWriter writer) : base(what) => _writer = writer;
    protected override async ValueTask SayAsync(bool what, CancellationToken cancellationToken) =>
        await _writer.WriteLineAsync(what ? "true" : "false", cancellationToken).ConfigureAwait(false);
}
