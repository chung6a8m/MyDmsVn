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
        Assert.Contains("$RepositoryRoot = (Get-Location).Path", script, StringComparison.Ordinal);
        Assert.Contains("$ManifestPath = 'tests/test-gates.v1.json'", script, StringComparison.Ordinal);
        Assert.Contains("dotnet build $runnerProject -c Release -f net8.0 --no-restore", script, StringComparison.Ordinal);
        Assert.Contains("MyDmsVn\\TestGate", script, StringComparison.Ordinal);
        Assert.Contains("$runnerDll.Length -gt 240", script, StringComparison.Ordinal);
        Assert.Contains("BuildRepositoryInventoryFixtures", script, StringComparison.Ordinal);
        Assert.Contains("testgate-inventory-", script, StringComparison.Ordinal);
        Assert.Contains("TestGate.IsolatedBuild.props", script, StringComparison.Ordinal);
        Assert.Contains("PreflightOutputRoot=$inventoryOutputRoot", script, StringComparison.Ordinal);
        Assert.Contains("MYDMSVN_TESTGATE_INVENTORY_ROOT", script, StringComparison.Ordinal);
        Assert.Contains("MYDMSVN_TESTGATE_ISOLATED_BUILD_PROPS", script, StringComparison.Ordinal);
        Assert.Contains("finally {", script, StringComparison.Ordinal);
        Assert.Contains("$env:MYDMSVN_TESTGATE_INVENTORY_ROOT = $previousInventoryOutputRoot", script, StringComparison.Ordinal);
        Assert.Contains("$env:MYDMSVN_TESTGATE_ISOLATED_BUILD_PROPS = $previousIsolatedBuildProps", script, StringComparison.Ordinal);
        Assert.Contains("Remove-IsolatedInventoryOutput $inventoryOutputRoot", script, StringComparison.Ordinal);
        Assert.Contains("Refusing to remove isolated inventory output outside the repository.", script, StringComparison.Ordinal);
        Assert.Contains("MyDmsVn.SqlTestRunner.dll", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'run', '--project'", script, StringComparison.Ordinal);
        Assert.Contains("git -C $RepositoryRoot status --porcelain", script, StringComparison.Ordinal);
        Assert.Contains("git -C $RepositoryRoot rev-parse HEAD", script, StringComparison.Ordinal);
        Assert.Contains("$Revision", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Project", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Framework", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Filter", script, StringComparison.Ordinal);
        Assert.DoesNotContain("$Category", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Repository_inventory_fixture_build_is_opt_in_and_not_attached_to_regular_builds()
    {
        var project = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tests", "tools", "MyDmsVn.SqlTestRunner.Tests",
            "MyDmsVn.SqlTestRunner.Tests.csproj"));

        Assert.Contains("<Target Name=\"BuildRepositoryInventoryFixtures\">", project, StringComparison.Ordinal);
        Assert.DoesNotContain("BeforeTargets=\"Build\"", project, StringComparison.Ordinal);
        Assert.Contains("@(RepositoryInventoryFixture)", project, StringComparison.Ordinal);
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
