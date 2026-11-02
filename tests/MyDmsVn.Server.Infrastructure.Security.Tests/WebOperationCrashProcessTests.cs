using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationCrashProcessTests
{
    [Fact]
    public async Task Ready_child_is_killed_externally_and_drained_before_any_SQL_dispatch()
    {
        var child = await WebOperationCrashProcess.StartAsync(Configuration());
        try
        {
            Assert.True(child.ProcessId > 0);
            await child.KillAsync();
            Assert.False(WebOperationCrashProcess.IsPoisoned);
        }
        finally { await child.CloseAsync(); }
    }

    [Fact]
    public async Task Malformed_control_is_rejected_without_disclosing_credentials_or_poisoning_cleanup()
    {
        var configuration = Configuration();
        configuration["phase"] = "invalid-test-phase";
        configuration["access"] = "secret-test-marker-never-log";
        var error = await Record.ExceptionAsync(() => WebOperationCrashProcess.StartAsync(configuration));
        var failures = Assert.IsType<AggregateException>(error);
        Assert.IsType<InvalidDataException>(failures.InnerExceptions[0]);
        Assert.Contains("Unexpected CrashHost protocol event", failures.InnerExceptions[0].Message);
        Assert.DoesNotContain("secret-test-marker-never-log", error.ToString());
        Assert.DoesNotContain("WEB5-no-SQL-may-open", error.ToString());
        Assert.False(WebOperationCrashProcess.IsPoisoned);
    }

    private static JObject Configuration() => new JObject
    {
        // Deliberately unparsable: any accidental SQL open fails. Ready precedes dispatch.
        ["connectionString"] = "WEB5-no-SQL-may-open", ["phase"] = "none",
        ["method"] = "GET", ["path"] = "operations/context", ["body"] = JValue.CreateNull(),
        ["access"] = JValue.CreateNull(), ["headers"] = new JObject()
    };
}
