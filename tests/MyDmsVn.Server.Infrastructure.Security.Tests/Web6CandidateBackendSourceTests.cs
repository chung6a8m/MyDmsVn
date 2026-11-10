using System;
using System.Collections.Generic;
using System.IO;
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

    public void Dispose() => Directory.Delete(_root, true);
}
