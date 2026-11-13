using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateBackendSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6-source-" + Guid.NewGuid().ToString("N"));
    private static readonly string Sha = new string('a', 40), Tree = new string('b', 40);

    public Web6CandidateBackendSourceTests() => Directory.CreateDirectory(Path.Combine(_root, ".git"));

    [Theory]
    [InlineData(null)]
    [InlineData("CleanArchitecture.sln")]
    [InlineData("MyDmsVn.UiLab.sln")]
    public async Task Missing_or_wrong_solution_marker_prevents_identity_read_and_allocator(string? marker)
    {
        if (marker != null) File.WriteAllText(Path.Combine(_root, marker), "wrong solution marker");
        var reads = 0; var allocations = 0;
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            var source = Web6CandidateBackendSource.Admit(_root, root => { reads++; return (root, Sha, Tree); });
            await source.AllocateAsync(() => { allocations++; return Task.FromResult(1); });
        });
        Assert.Equal(0, reads); Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData("root")]
    [InlineData("sha")]
    [InlineData("tree")]
    public async Task Prepared_source_mismatch_prevents_allocator(string mismatch)
    {
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "solution");
        var allocations = 0;
        await Assert.ThrowsAnyAsync<IOException>(async () =>
        {
            var source = Web6CandidateBackendSource.Admit(_root, root => (root, Sha, Tree),
                mismatch == "root" ? Path.Combine(_root, "other") : _root,
                mismatch == "sha" ? new string('c', 40) : Sha, mismatch == "tree" ? new string('d', 40) : Tree);
            await source.AllocateAsync(() => { allocations++; return Task.FromResult(1); });
        });
        Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("git-root")]
    [InlineData("sha")]
    [InlineData("tree")]
    [InlineData("dirty")]
    public async Task Source_change_after_admission_stops_allocator(string change)
    {
        var marker = Path.Combine(_root, "MyDmsVn.sln"); File.WriteAllText(marker, "solution");
        var changed = false; var allocations = 0;
        var source = Web6CandidateBackendSource.Admit(_root, root =>
        {
            if (changed && change == "dirty") throw new IOException("Source dirty.");
            return (changed && change == "git-root" ? Path.GetDirectoryName(root)! : root,
                changed && change == "sha" ? new string('c', 40) : Sha,
                changed && change == "tree" ? new string('d', 40) : Tree);
        });
        changed = true;
        if (change == "marker") File.Move(marker, Path.Combine(_root, "CleanArchitecture.sln"));
        await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(() => { allocations++; return Task.FromResult(1); }));
        Assert.Equal(0, allocations);
    }

    [Fact]
    public async Task Verified_source_is_reused_and_checked_before_allocator()
    {
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "solution");
        var calls = new List<string>();
        var source = Web6CandidateBackendSource.Admit(_root, root => { calls.Add("source"); return (root, Sha, Tree); });
        var result = await source.AllocateAsync(() => { calls.Add("allocate"); return Task.FromResult(42); });
        Assert.Equal(42, result); Assert.Equal(_root, source.Root);
        Assert.Equal(new[] { "source", "source", "allocate" }, calls);
    }

    [Fact]
    public void Actual_checkout_resolves_MyDmsVn_solution_marker()
    {
        var root = Web6CandidateBackendSource.ResolveRoot(AppContext.BaseDirectory);
        Assert.True(File.Exists(Path.Combine(root, "MyDmsVn.sln")));
        Assert.True(File.Exists(Path.Combine(root, "src", "MyDmsVn.Server.HttpHost", "MyDmsVn.Server.HttpHost.csproj")));
        Assert.True(File.Exists(Path.Combine(root, "tests", "MyDmsVn.Server.Infrastructure.Security.Tests", "MyDmsVn.Server.Infrastructure.Security.Tests.csproj")));
    }

    [Theory]
    [InlineData("dirty-submodule")]
    [InlineData("untracked")]
    [InlineData("external-config")]
    [InlineData("wrong-git-root")]
    [InlineData("junction-root")]
    public async Task Live_backend_reader_refuses_hidden_or_redirected_source_before_allocator(string change)
    {
        Git(_root, "init --quiet");
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "synthetic solution");
        var submodule = Directory.CreateDirectory(Path.Combine(_root, "vendor", "sample")).FullName;
        Git(submodule, "init --quiet");
        File.WriteAllText(Path.Combine(submodule, "tracked.txt"), "tracked");
        Git(submodule, "add tracked.txt");
        Git(submodule, "commit --quiet -m synthetic-submodule");
        Git(_root, "config diff.ignoreSubmodules all");
        Git(_root, "config status.showUntrackedFiles no");
        Git(_root, "update-index --add --cacheinfo 160000," + Git(submodule, "rev-parse HEAD") + ",vendor/sample");
        Git(_root, "add MyDmsVn.sln");
        Git(_root, "commit --quiet -m synthetic-root");
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var calls = 0;
        Func<Task<int>> allocate = () => { calls++; return Task.FromResult(1); };
        switch (change)
        {
            case "dirty-submodule":
                File.WriteAllText(Path.Combine(submodule, "tracked.txt"), "uncommitted change");
                Assert.Equal("", Git(_root, "status --porcelain"));
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "untracked":
                File.WriteAllText(Path.Combine(_root, "new.txt"), "untracked change");
                Assert.Equal("", Git(_root, "status --porcelain"));
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "external-config":
                Git(_root, "config include.path missing-fixture-config");
                await Assert.ThrowsAnyAsync<IOException>(() => source.AllocateAsync(allocate));
                break;
            case "wrong-git-root":
                var nested = Directory.CreateDirectory(Path.Combine(_root, "wrong")).FullName;
                File.WriteAllText(Path.Combine(nested, "MyDmsVn.sln"), "fake solution marker");
                await Assert.ThrowsAnyAsync<IOException>(() => WebHostIisExpressTests.AdmitCurrentBackend(nested).AllocateAsync(allocate));
                break;
            case "junction-root":
                var alias = _root + "-alias";
                using (var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    "/d /c mklink /J \"" + alias + "\" \"" + _root + "\"") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    Assert.NotNull(command);
                    Assert.True(command!.WaitForExit(5000));
                    Assert.Equal(0, command.ExitCode);
                }
                try { await Assert.ThrowsAnyAsync<IOException>(() => WebHostIisExpressTests.AdmitCurrentBackend(alias).AllocateAsync(allocate)); }
                finally { Directory.Delete(alias); }
                break;
        }
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Live_backend_reader_ignores_ambient_git_directory()
    {
        Git(_root, "init --quiet");
        File.WriteAllText(Path.Combine(_root, "MyDmsVn.sln"), "synthetic solution");
        Git(_root, "add MyDmsVn.sln");
        Git(_root, "commit --quiet -m synthetic-root");
        var source = WebHostIisExpressTests.AdmitCurrentBackend(_root);
        var prior = Environment.GetEnvironmentVariable("GIT_DIR");
        try
        {
            Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(_root, "absent-git-directory"));
            Assert.Equal(1, await source.AllocateAsync(() => Task.FromResult(1)));
        }
        finally { Environment.SetEnvironmentVariable("GIT_DIR", prior); }
    }

    private static string Git(string root, string arguments)
    {
        var start = new ProcessStartInfo("git",
            "-c user.name=Fixture -c user.email=fixture@example.invalid -c core.hooksPath=NUL " + arguments)
        {
            WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true,
        };
        foreach (var key in new List<string>(start.EnvironmentVariables.Keys.Cast<string>()))
            if (key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)) start.EnvironmentVariables.Remove(key);
        start.EnvironmentVariables["GIT_CONFIG_NOSYSTEM"] = "1";
        start.EnvironmentVariables["GIT_CONFIG_GLOBAL"] = "NUL";
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(5000));
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        return output.Trim();
    }

    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        Directory.Delete(_root, true);
    }
}
