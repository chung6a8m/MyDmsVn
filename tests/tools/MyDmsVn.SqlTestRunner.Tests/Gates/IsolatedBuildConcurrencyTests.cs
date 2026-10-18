using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestRunner.Gates;
using Xunit;

namespace MyDmsVn.SqlTestRunner.Tests.Gates;

public sealed class IsolatedBuildConcurrencyTests
{
    [Fact]
    public async Task Concurrent_inventory_fixture_builds_do_not_touch_normal_project_outputs()
    {
        var repositoryRoot = RepositoryRoot();
        var runnerTestsProject = Path.Combine(
            repositoryRoot,
            "tests",
            "tools",
            "MyDmsVn.SqlTestRunner.Tests",
            "MyDmsVn.SqlTestRunner.Tests.csproj");
        var isolatedProps = Path.Combine(repositoryRoot, "tests", "tools", "TestGate.IsolatedBuild.props");
        var isolatedRoot = Path.Combine(
            repositoryRoot, "artifacts", "concurrent-inventory-" + Guid.NewGuid().ToString("N"));
        var firstRoot = Path.Combine(isolatedRoot, "first");
        var secondRoot = Path.Combine(isolatedRoot, "second");
        var before = NormalProjectOutputSnapshot(repositoryRoot);

        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        try
        {
            var first = BuildInventoryFixturesAsync(firstRoot);
            var second = BuildInventoryFixturesAsync(secondRoot);
            var results = await Task.WhenAll(first, second);

            Assert.All(results, result => Assert.True(result.ExitCode == 0, result.Output));
            Assert.True(File.Exists(Path.Combine(
                firstRoot, "MyDmsVn.Server.Infrastructure.IntegrationTests", "Release", "net48",
                "MyDmsVn.Server.Infrastructure.IntegrationTests.dll")));
            Assert.True(File.Exists(Path.Combine(
                secondRoot, "MyDmsVn.Server.Infrastructure.IntegrationTests", "Release", "net48",
                "MyDmsVn.Server.Infrastructure.IntegrationTests.dll")));
            Assert.True(File.Exists(Path.Combine(
                firstRoot, "obj", "MyDmsVn.Server.Infrastructure.IntegrationTests", "project.assets.json")));
            Assert.True(File.Exists(Path.Combine(
                secondRoot, "obj", "MyDmsVn.Server.Infrastructure.IntegrationTests", "project.assets.json")));
            Assert.Equal(before, NormalProjectOutputSnapshot(repositoryRoot));
        }
        finally
        {
            var artifactsRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "artifacts")) +
                                Path.DirectorySeparatorChar;
            var targetRoot = Path.GetFullPath(isolatedRoot);
            if (!targetRoot.StartsWith(artifactsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to remove inventory outputs outside artifacts.");
            }

            if (Directory.Exists(targetRoot))
            {
                Directory.Delete(targetRoot, recursive: true);
            }

        }

        async Task<(int ExitCode, string Output)> BuildInventoryFixturesAsync(string outputRoot)
        {
            var properties = "Configuration=Release;PreflightOutputRoot=" + outputRoot +
                             ";CustomBeforeDirectoryBuildProps=" + isolatedProps;
            return await RunDotnetAsync(
                repositoryRoot,
                "msbuild",
                "-nodeReuse:false",
                runnerTestsProject,
                "-target:BuildRepositoryInventoryFixtures",
                "-property:" + properties).ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Final_workspace_output_lease_serializes_build_and_test_use()
    {
        var repositoryRoot = Path.Combine(
            Path.GetTempPath(), "testgate-workspace-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repositoryRoot);
        var canonicalRoot = Path.GetFullPath(repositoryRoot).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
        {
            canonicalRoot = canonicalRoot.ToUpperInvariant();
        }

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRoot)));
        var lockPath = Path.Combine(
            Path.GetTempPath(), "MyDmsVn", "TestGate", "workspace-locks", key + ".lock");
        WorkspaceOutputLease? first = null;
        WorkspaceOutputLease? second = null;
        try
        {
            first = await WorkspaceOutputLease.AcquireAsync(
                repositoryRoot, TimeSpan.FromSeconds(10), CancellationToken.None);
            var waiting = WorkspaceOutputLease.AcquireAsync(
                repositoryRoot, TimeSpan.FromSeconds(10), CancellationToken.None);

            await Task.Delay(250);
            Assert.False(waiting.IsCompleted);

            await first.DisposeAsync();
            second = await waiting;
        }
        finally
        {
            if (second is not null)
            {
                await second.DisposeAsync();
            }

            if (first is not null)
            {
                await first.DisposeAsync();
            }

            File.Delete(lockPath);
            Directory.Delete(repositoryRoot);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetAsync(
        string workingDirectory,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw new TimeoutException("Concurrent repository inventory fixture build exceeded three minutes.");
        }

        return (process.ExitCode, (await stdout.ConfigureAwait(false)) + (await stderr.ConfigureAwait(false)));
    }

    private static string[] NormalProjectOutputSnapshot(string repositoryRoot)
    {
        var projectFiles = new[]
            {
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.IntegrationTests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.Catalog.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.Foundation.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.Inventory.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Infrastructure.Migrations.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Desktop.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.SqlTestHarness.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Desktop.Session.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Server.Application.Tests"),
                Path.Combine(repositoryRoot, "tests", "MyDmsVn.Architecture.Tests"),
            }
            .SelectMany(directory => Directory.EnumerateFiles(
                directory, "*.csproj", SearchOption.TopDirectoryOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in projectFiles)
        {
            var projectDirectory = Path.GetDirectoryName(project)!;
            foreach (var outputDirectoryName in new[] { "bin", "obj" })
            {
                var outputDirectory = Path.Combine(projectDirectory, outputDirectoryName);
                if (Directory.Exists(outputDirectory))
                {
            foreach (var path in Directory.EnumerateFiles(
                         outputDirectory, "*", SearchOption.AllDirectories))
                    {
                        files.Add(path);
                    }
                }
            }

            var lockPath = Path.Combine(projectDirectory, "packages.lock.json");
            if (File.Exists(lockPath))
            {
                files.Add(lockPath);
            }
        }

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => path + "|" + new FileInfo(path).Length + "|" +
                            File.GetLastWriteTimeUtc(path).Ticks)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var project in Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly))
            {
                yield return project;
            }

            foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(child);
                if (name is ".git" or "bin" or "obj" or "artifacts" or "packages" or ".vs")
                {
                    continue;
                }

                pending.Push(child);
            }
        }
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
