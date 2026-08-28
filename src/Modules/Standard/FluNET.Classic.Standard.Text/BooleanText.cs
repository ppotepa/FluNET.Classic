using FluNET.Classic.Core;

namespace FluNET.Classic.Standard.Text;

[StableId("verb:say:boolean")]
[Qualifier("BOOLEAN")]
public sealed class SayBoolean : Say<bool>
{
    private readonly IOutputWriter _writer;

    [StableId("ctor:say:boolean")]
    public SayBoolean(
        [What, StableId("role:say:boolean:what")] bool what,
        [FromServices] IOutputWriter writer) : base(what) => _writer = writer;

    protected override async ValueTask SayAsync(bool what, CancellationToken cancellationToken) =>
        await _writer.WriteLineAsync(what ? "true" : "false", cancellationToken).ConfigureAwait(false);
}
