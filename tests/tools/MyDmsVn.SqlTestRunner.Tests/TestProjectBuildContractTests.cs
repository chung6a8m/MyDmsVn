using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests;

public sealed class TestProjectBuildContractTests
{
    [Fact]
    public void Runner_test_project_prepares_only_the_active_sql_tfm_from_a_clean_checkout()
    {
        var projectPath = Path.Combine(
            RepositoryRoot(),
            "tests",
            "tools",
            "MyDmsVn.SqlTestRunner.Tests",
            "MyDmsVn.SqlTestRunner.Tests.csproj");
        var document = XDocument.Load(projectPath);
        var invocations = document.Descendants("MSBuild").ToArray();

        Assert.DoesNotContain(document.Descendants("Exec"), element =>
            ((string?)element.Attribute("Command"))?.Contains("dotnet restore", StringComparison.Ordinal) == true);
        Assert.Single(invocations);
        Assert.Contains(document.Descendants("Target"), element =>
            (string?)element.Attribute("Name") == "BuildRepositoryInventoryFixtures");
        Assert.Contains(invocations, element =>
            ((string?)element.Attribute("Targets")) == "Restore;Build" &&
            ((string?)element.Attribute("Properties"))?.Contains("PreflightOutputRoot=$(PreflightOutputRoot)", StringComparison.Ordinal) == true &&
            ((string?)element.Attribute("Properties"))?.Contains("CustomBeforeDirectoryBuildProps=$(CustomBeforeDirectoryBuildProps)", StringComparison.Ordinal) == true);
        var isolatedProps = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "tests", "tools", "TestGate.IsolatedBuild.props"));
        Assert.Contains("<BaseOutputPath>$(PreflightOutputRoot)", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("<BaseIntermediateOutputPath>$(PreflightOutputRoot)", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("<MSBuildProjectExtensionsPath>$(PreflightOutputRoot)", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("<NuGetLockFilePath>$(PreflightOutputRoot)", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("<RestoreLockedMode Condition=\"Exists(", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("Name=\"TestGateCopyLockedPackageFile\"", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("SourceFiles=\"$(MSBuildProjectDirectory)\\packages.lock.json\"", isolatedProps, StringComparison.Ordinal);
        Assert.Contains("obj/**;bin/**", isolatedProps, StringComparison.Ordinal);
        Assert.Equal(11, document.Descendants("RepositoryInventoryFixture").Count());
        Assert.DoesNotContain(document.Descendants("Target"), element =>
            ((string?)element.Attribute("BeforeTargets"))?.Contains("Build", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(invocations, element =>
            ((string?)element.Attribute("Properties"))?.Contains("TargetFramework=net8.0", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Candidate_projects_declare_the_reviewed_active_frameworks()
    {
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["src/MyDmsVn.Contracts/MyDmsVn.Contracts.csproj"] = "net48",
            ["src/MyDmsVn.SharedKernel/MyDmsVn.SharedKernel.csproj"] = "net48",
            ["src/MyDmsVn.Server.Domain/MyDmsVn.Server.Domain.csproj"] = "net48",
            ["src/MyDmsVn.Server.Application/MyDmsVn.Server.Application.csproj"] = "net48",
            ["src/MyDmsVn.Server.Infrastructure/MyDmsVn.Server.Infrastructure.csproj"] = "net48",
            ["src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj"] = "net48",
            ["src/MyDmsVn.Desktop.Application/MyDmsVn.Desktop.Application.csproj"] = "net48",
            ["src/MyDmsVn.Desktop.Infrastructure.Local/MyDmsVn.Desktop.Infrastructure.Local.csproj"] = "net48",
            ["src/MyDmsVn.Desktop.WinForms/MyDmsVn.Desktop.WinForms.csproj"] = "net48",
            ["src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.Foundation.Tests/MyDmsVn.Server.Infrastructure.Foundation.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Server.Infrastructure.Migrations.Tests/MyDmsVn.Server.Infrastructure.Migrations.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj"] = "net48",
            ["tests/MyDmsVn.SqlTestHarness/MyDmsVn.SqlTestHarness.csproj"] = "netstandard2.0",
            ["tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj"] = "net8.0",
            ["tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj"] = "net8.0",
        };

        foreach (var item in expected)
        {
            var project = XDocument.Load(Path.Combine(
                RepositoryRoot(),
                item.Key.Replace('/', Path.DirectorySeparatorChar)));
            var declared = project.Descendants()
                .Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .SelectMany(element => element.Value.Split(';'))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            Assert.Equal(new[] { item.Value }, declared);
        }
    }

    [Fact]
    public void Candidate_solution_and_release_matrix_park_the_modern_host()
    {
        var root = RepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "MyDmsVn.sln"));
        var release = File.ReadAllText(Path.Combine(root, "deployment", "release", "Publish-LocalRelease.ps1"));

        Assert.DoesNotContain("MyDmsVn.Desktop.AppCore", solution, StringComparison.Ordinal);
        Assert.DoesNotContain("MyDmsVn.Desktop.AppCore", release, StringComparison.Ordinal);
        Assert.DoesNotContain("net8.0-windows", release, StringComparison.Ordinal);
        Assert.Contains("MyDmsVn.Desktop.App.csproj", release, StringComparison.Ordinal);
        Assert.Contains("MyDmsVn.Server.DbMigrator.csproj", release, StringComparison.Ordinal);
        Assert.True(
            release.Split("'-f', 'net48'", StringSplitOptions.None).Length >= 3,
            "Both the desktop host and admin migrator must publish net48.");
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
