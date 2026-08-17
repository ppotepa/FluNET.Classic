using System.Reflection;
using FluNET.Classic.Core;

namespace FluNET.Classic.OutputProjectionFixture;

public sealed record PairResult(string First, int Second);

public sealed class ProjectionFixtureModule : LanguageModule
{
    public override string Name => "output-projection-fixture";
    public override IReadOnlyCollection<Assembly> Assemblies => new[] { typeof(ProjectionFixtureModule).Assembly };
    public override IReadOnlyCollection<IntrinsicDescriptor> Intrinsics => new[]
    {
        new IntrinsicDescriptor(
            "intrinsic:test:top",
            "TOP",
            IntrinsicSyntaxKind.CollectionAmountFrom,
            Execution: IntrinsicExecutionKind.Streaming)
    };
}

[Verb("PROJECTPAIR")]
public sealed class ProjectPair : IVerb<PairResult>
{
    public ProjectPair(
        [What, RoleDirection(RoleDirection.Output), OutputMember("First")] string first,
        [With, RoleDirection(RoleDirection.Output), OutputMember("Second")] int second)
    {
    }

    public ValueTask<PairResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new PairResult("member-value", 42));
}

[Verb("PROJECTTUPLE")]
public sealed class ProjectTuple : IVerb<(string Text, int Number)>
{
    public ProjectTuple(
        [What, RoleDirection(RoleDirection.Output), OutputIndex(0)] string text,
        [With, RoleDirection(RoleDirection.Output), OutputIndex(1)] int number)
    {
    }

    public ValueTask<(string Text, int Number)> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(("tuple-value", 7));
}

[Verb("ACCEPTNULL")]
public sealed class AcceptNull : IVerb<string>
{
    private readonly string? _value;
    public AcceptNull([What] string? value) => _value = value;
    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_value ?? "<null>");
}

[Verb("INTERPRET")]
public sealed class InterpretAs : IVerb<string>
{
    private readonly string _value;
    private readonly string _representation;
    public InterpretAs([What] string value, [As] string representation)
    {
        _value = value;
        _representation = representation;
    }

    public ValueTask<string> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"{_representation}:{_value}");
}
