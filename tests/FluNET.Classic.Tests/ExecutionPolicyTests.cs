using FluNET.Classic.Hosting;
using FluNET.Classic.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace FluNET.Classic.Tests;

public sealed class ExecutionPolicyTests
{
    [TestCase(nameof(ExecutionPolicy.RetryAttempts), -1)]
    [TestCase(nameof(ExecutionPolicy.MaxParallelism), 0)]
    public void Host_rejects_invalid_integer_execution_policy(string property, int value)
    {
        using ServiceProvider host = FluNetHost.Create(new FluNetOptions
        {
            ConfigureExecution = policy =>
            {
                if (property == nameof(ExecutionPolicy.RetryAttempts))
                    policy.RetryAttempts = value;
                else
                    policy.MaxParallelism = value;
            }
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => host.GetRequiredService<ExecutionPolicy>());
    }

    [Test]
    public void Host_rejects_non_positive_timeout()
    {
        using ServiceProvider host = FluNetHost.Create(new FluNetOptions
        {
            ConfigureExecution = policy => policy.DefaultTimeout = TimeSpan.Zero
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => host.GetRequiredService<ExecutionPolicy>());
    }
}
