using System.Reflection;
using FluNET.Classic.Core;

namespace FluNET.Classic.OutputProjectionFixture;

public sealed record PairResult(string First, int Second);

public sealed class ProjectionFixtureModule : LanguageModule
{
    public override string Name => "output-projection-fixture";
    public override IReadOnlyCollection<Assembly> Assemblies => new[] { typeof(ProjectionFixtureModule).Assembly };
}

[Verb("PROJECTPAIR")]
public sealed class ProjectPair : IVerb<PairResult>
{
    public ProjectPair(
        [What(Direction = RoleDirection.Output), OutputMember("First")] string first,
        [With(Direction = RoleDirection.Output), OutputMember("Second")] int second)
    {
    }

    public ValueTask<PairResult> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new PairResult("member-value", 42));
}

[Verb("PROJECTTUPLE")]
public sealed class ProjectTuple : IVerb<(string Text, int Number)>
{
    public ProjectTuple(
        [What(Direction = RoleDirection.Output), OutputIndex(0)] string text,
        [With(Direction = RoleDirection.Output), OutputIndex(1)] int number)
    {
    }

    public ValueTask<(string Text, int Number)> ExecuteAsync(VerbExecutionContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(("tuple-value", 7));
}
