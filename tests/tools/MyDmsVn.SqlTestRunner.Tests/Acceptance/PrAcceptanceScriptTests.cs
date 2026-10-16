using System;
using System.IO;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Acceptance;

public sealed class PrAcceptanceScriptTests
{
    [Fact]
    public void Script_requires_a_clean_commit_and_invokes_the_distinct_mode()
    {
        var root = RepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tests", "tools", "Invoke-SqlPrAcceptance.ps1"));

        Assert.Contains("git -C $RepositoryRoot status --porcelain", script, StringComparison.Ordinal);
        Assert.Contains("git -C $RepositoryRoot rev-parse HEAD", script, StringComparison.Ordinal);
        Assert.Contains("pr-acceptance", script, StringComparison.Ordinal);
        Assert.Contains("--revision $revision", script, StringComparison.Ordinal);
        Assert.Contains("[string[]] $Frameworks", script, StringComparison.Ordinal);
        Assert.Contains("--frameworks ($Frameworks -join ',')", script, StringComparison.Ordinal);
        Assert.DoesNotContain("baseline", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Strict_wrappers_require_and_forward_reviewed_framework_arrays()
    {
        var root = RepositoryRoot();
        foreach (var name in new[] { "Invoke-SqlPrAcceptance.ps1", "Invoke-SqlIntegrationBenchmark.ps1" })
        {
            var script = File.ReadAllText(Path.Combine(root, "tests", "tools", name));
            Assert.Contains("[string[]] $Frameworks", script, StringComparison.Ordinal);
            Assert.Contains("--frameworks ($Frameworks -join ',')", script, StringComparison.Ordinal);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
