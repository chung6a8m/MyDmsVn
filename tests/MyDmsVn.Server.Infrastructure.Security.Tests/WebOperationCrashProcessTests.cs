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
        // The supervisor may drain by cancellation before the child's exit 2 is
        // observed, or observe that natural failure first. Both retain the exact
        // protocol error; neither permits an unrelated startup failure to pass.
        var protocol = error;
        if (error is AggregateException failures)
        {
            Assert.Equal(2, failures.InnerExceptions.Count);
            protocol = failures.InnerExceptions[0];
            Assert.IsType<InvalidOperationException>(failures.InnerExceptions[1]);
            Assert.Equal("CrashHost failed without exposing request or SQL details.", failures.InnerExceptions[1].Message);
        }
        Assert.IsType<InvalidDataException>(protocol);
        Assert.Contains("Unexpected CrashHost protocol event", protocol!.Message);
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
