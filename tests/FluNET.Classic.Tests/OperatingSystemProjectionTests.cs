using FluNET.Classic.Core;
using FluNET.Classic.Standard.OS;
using NUnit.Framework;
using System.Runtime.InteropServices;

namespace FluNET.Classic.Tests;

public sealed class OperatingSystemProjectionTests
{
    [Test]
    public async Task Operating_system_value_projections_expose_typed_fields()
    {
        var os = new OperatingSystemInfo("test OS", Architecture.X64, ".NET test");
        var user = new CurrentUserInfo("alice", "EXAMPLE");
        var cwd = new WorkingDirectory(@"C:\work");
        var context = new VerbExecutionContext(null, new Dictionary<string, object?>(), null);

        Assert.That(await new GetOperatingSystemDescription(os).ExecuteAsync(context), Is.EqualTo("test OS"));
        Assert.That(await new GetOperatingSystemArchitecture(os).ExecuteAsync(context), Is.EqualTo(Architecture.X64));
        Assert.That(await new GetOperatingSystemFramework(os).ExecuteAsync(context), Is.EqualTo(".NET test"));
        Assert.That(await new GetCurrentUserName(user).ExecuteAsync(context), Is.EqualTo("alice"));
        Assert.That(await new GetCurrentUserDomain(user).ExecuteAsync(context), Is.EqualTo("EXAMPLE"));
        Assert.That(await new GetWorkingDirectoryPath(cwd).ExecuteAsync(context), Is.EqualTo(@"C:\work"));
    }
}
