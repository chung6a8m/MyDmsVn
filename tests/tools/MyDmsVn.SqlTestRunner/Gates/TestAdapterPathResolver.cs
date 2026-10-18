using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MyDmsVn.SqlTestRunner.Gates;

public static class TestAdapterPathResolver
{
    public static string Resolve(string repositoryRoot, string projectRelativePath, string targetFramework)
    {
        var projectDirectory = Path.GetDirectoryName(projectRelativePath) ?? string.Empty;
        var lockPath = Path.Combine(repositoryRoot, projectDirectory, "packages.lock.json");
        using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
        var package = document.RootElement
            .GetProperty("dependencies")
            .EnumerateObject()
            .Select(group => group.Value)
            .FirstOrDefault(group => group.TryGetProperty("xunit.runner.visualstudio", out _))
            .GetProperty("xunit.runner.visualstudio");
        var version = package.GetProperty("resolved").GetString();
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidDataException("The test adapter package has no resolved version.");
        }

        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            packageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        }

        var buildFrameworks = string.Equals(targetFramework, "net48", StringComparison.OrdinalIgnoreCase)
            ? new[] { "net472", "net462" }
            : new[] { targetFramework };
        foreach (var buildFramework in buildFrameworks)
        {
            var adapterPath = Path.Combine(
                packageRoot,
                "xunit.runner.visualstudio",
                version,
                "build",
                buildFramework,
                "xunit.runner.visualstudio.testadapter.dll");
            if (File.Exists(adapterPath))
            {
                return Path.GetDirectoryName(adapterPath)!;
            }
        }

        throw new FileNotFoundException("The reviewed xUnit test adapter was not restored.");
    }
}
