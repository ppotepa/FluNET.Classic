using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public class FlowScopeTests
{
    [Test]
    public void Variable_defined_with_same_type_in_both_if_branches_is_available_after_if()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        const string source = """
            IF [flag] IS true THEN {
                CHECK IF true INTO [shared].
            } ELSE {
                CHECK IF false INTO [shared].
            }
            CHECK IF [shared] IS true INTO [after].
            """;

        CheckResult result = engine.Check(source, new Dictionary<string, Type> { ["flag"] = typeof(bool) });

        Assert.That(result.Success, Is.True, string.Join("; ", result.Bound?.Diagnostics.Select(x => x.Message) ?? Array.Empty<string>()));
    }

    [Test]
    public void Variable_defined_in_only_one_if_branch_does_not_escape_the_branch()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        const string source = """
            IF [flag] IS true THEN {
                CHECK IF true INTO [onlyThen].
            } ELSE {
                CHECK IF false.
            }
            CHECK IF [onlyThen] IS true INTO [after].
            """;

        CheckResult result = engine.Check(source, new Dictionary<string, Type> { ["flag"] = typeof(bool) });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Bound?.Diagnostics.Any(x => x.Code == "FLU-BIND-151"), Is.True);
    }

    [Test]
    public void Incompatible_types_from_if_branches_report_a_flow_diagnostic()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        const string source = """
            IF [flag] IS true THEN {
                CHECK IF true INTO [value].
            } ELSE {
                PARSE DATE FROM "2026-08-17" INTO [value].
            }
            """;

        CheckResult result = engine.Check(source, new Dictionary<string, Type> { ["flag"] = typeof(bool) });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Bound?.Diagnostics.Any(x => x.Code == "FLU-BIND-132"), Is.True);
    }

    [Test]
    public void For_each_iterator_does_not_escape_its_body()
    {
        using ServiceProvider host = FluNetHost.Create();
        ClassicEngine engine = host.GetRequiredService<ClassicEngine>();

        const string source = """
            FOR EACH [item] IN [items] THEN {
                SAY [item].
            }
            SAY [item].
            """;

        CheckResult result = engine.Check(source, new Dictionary<string, Type> { ["items"] = typeof(string[]) });

        Assert.That(result.Success, Is.False);
        Assert.That(result.Bound?.Diagnostics.Any(), Is.True);
    }
}
