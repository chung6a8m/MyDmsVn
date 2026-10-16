using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests;

public sealed class TestProjectBuildContractTests
{
    [Fact]
    public void Runner_test_project_prepares_both_inventory_tfms_from_a_clean_checkout()
    {
        var projectPath = Path.Combine(
            RepositoryRoot(),
            "tests",
            "tools",
            "MyDmsVn.SqlTestRunner.Tests",
            "MyDmsVn.SqlTestRunner.Tests.csproj");
        var document = XDocument.Load(projectPath);
        var invocations = document.Descendants("MSBuild").ToArray();

        Assert.Contains(document.Descendants("Exec"), element =>
            ((string?)element.Attribute("Command"))?.Contains("dotnet restore", StringComparison.Ordinal) == true &&
            ((string?)element.Attribute("Command"))?.Contains(
                "MyDmsVn.Server.Infrastructure.IntegrationTests.csproj",
                StringComparison.Ordinal) == true);
        Assert.Contains(invocations, element =>
            ((string?)element.Attribute("Targets"))?.Contains("Build", StringComparison.Ordinal) == true &&
            ((string?)element.Attribute("Properties"))?.Contains("TargetFramework=net48", StringComparison.Ordinal) == true);
        Assert.Contains(invocations, element =>
            ((string?)element.Attribute("Targets"))?.Contains("Build", StringComparison.Ordinal) == true &&
            ((string?)element.Attribute("Properties"))?.Contains("TargetFramework=net8.0", StringComparison.Ordinal) == true);
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
