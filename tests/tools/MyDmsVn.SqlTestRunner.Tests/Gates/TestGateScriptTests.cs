using System;
using System.IO;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class TestGateScriptTests
{
    [Fact]
    public void Wrapper_exposes_only_manifest_identity_and_private_inputs()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "tools", "Invoke-TestGate.ps1"));

        Assert.Contains("test-gate", script, StringComparison.Ordinal);
        Assert.Contains("git -C $RepositoryRoot status --porcelain", script, StringComparison.Ordinal);
        Assert.Contains("git -C $RepositoryRoot rev-parse HEAD", script, StringComparison.Ordinal);
        Assert.Contains("$Revision", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Project", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Framework", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Filter", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Category", script, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MyDmsVn.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
