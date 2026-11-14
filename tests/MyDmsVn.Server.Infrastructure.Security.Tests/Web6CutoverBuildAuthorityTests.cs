using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MyDmsVn.Web6.CandidateBootstrap;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverBuildAuthorityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "web6-build-scope-" + Guid.NewGuid().ToString("N"));
    private static readonly string FrontendTree = new string('a', 40), BackendTree = new string('b', 40);
    private string FrontendDist => Path.Combine(_root, "archived-dist");
    private string CurrentPublish => Path.Combine(_root, "current-publish");
    private string BackendArchive => Path.Combine(_root, "archived-publish");
    private string OldPublish => Path.Combine(_root, "old-publish");

    public Web6CutoverBuildAuthorityTests()
    {
        Write(FrontendDist, "index.html", "<script src=assets/app.js></script>");
        Write(FrontendDist, "assets/app.js", "archived frontend");
        Write(CurrentPublish, "wwwroot/index.html", "<script src=assets/app.js></script>");
        Write(CurrentPublish, "wwwroot/assets/app.js", "archived frontend");
        Write(CurrentPublish, "bin/host.dll", "current backend");
        Write(CurrentPublish, "Web.config", "current config");
        foreach (var root in new[] { BackendArchive, OldPublish })
        {
            Write(root, "bin/host.dll", "archived backend");
            Write(root, "Web.config", "archived config");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Separately_sealed_full_publish_superset_is_admitted(bool distinctCurrentFrontend)
    {
        // Even byte-identical frontend files occupy a different normalized scope inside
        // the full publish. A current candidate may also differ from the WEB4 archive.
        if (distinctCurrentFrontend) Write(CurrentPublish, "wwwroot/assets/app.js", "current candidate frontend");
        else Assert.Equal(Measure(FrontendDist), Measure(Path.Combine(CurrentPublish, "wwwroot")));
        Assert.NotEqual(Measure(FrontendDist), Measure(CurrentPublish));
        var authority = Seal();

        var allocations = 0;
        var result = await authority.AllocateAsync(() => Observe(), () => { allocations++; return Task.FromResult(42); });
        Assert.Equal(42, result);
        Assert.Equal(1, allocations);
    }

    [Theory]
    [InlineData("frontend-dist")]
    [InlineData("current-backend")]
    [InlineData("current-config")]
    [InlineData("current-frontend")]
    [InlineData("current-extra-file")]
    [InlineData("backend-archive")]
    [InlineData("old-publish")]
    [InlineData("both-old-trees")]
    [InlineData("frontend-sha")]
    [InlineData("frontend-tree")]
    [InlineData("frontend-dirty")]
    [InlineData("backend-sha")]
    [InlineData("backend-tree")]
    [InlineData("backend-dirty")]
    [InlineData("null-observation")]
    public async Task Independent_drift_is_refused_before_allocator(string drift)
    {
        var authority = Seal();
        switch (drift)
        {
            case "frontend-dist": Write(FrontendDist, "assets/app.js", "changed"); break;
            case "current-backend": Write(CurrentPublish, "bin/host.dll", "changed"); break;
            case "current-config": Write(CurrentPublish, "Web.config", "changed"); break;
            case "current-frontend": Write(CurrentPublish, "wwwroot/assets/app.js", "changed"); break;
            case "current-extra-file": Write(CurrentPublish, "extra.txt", "unsealed"); break;
            case "backend-archive": Write(BackendArchive, "bin/host.dll", "changed"); break;
            case "old-publish": Write(OldPublish, "bin/host.dll", "changed"); break;
            case "both-old-trees":
                Write(BackendArchive, "bin/host.dll", "same changed bytes");
                Write(OldPublish, "bin/host.dll", "same changed bytes");
                break;
        }
        var allocations = 0;
        await Assert.ThrowsAsync<IOException>(() => authority.AllocateAsync(
            () => drift == "null-observation" ? null! : Observe(drift),
            () => { allocations++; return Task.FromResult(42); }));
        Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Drift_after_initial_verification_is_refused_before_the_next_allocator(bool alreadyAllocated)
    {
        var authority = Seal();
        authority.Verify(Observe());
        var allocations = 0;
        Task<int> Allocate()
        {
            allocations++;
            return Task.FromResult(42);
        }

        if (alreadyAllocated) await authority.AllocateAsync(() => Observe(), Allocate);
        Write(CurrentPublish, "bin/host.dll", "changed after initial verification");

        await Assert.ThrowsAsync<IOException>(() => authority.AllocateAsync(() => Observe(), Allocate));
        Assert.Equal(alreadyAllocated ? 1 : 0, allocations);
    }

    [Fact]
    public async Task Identical_scope_old_archive_and_publish_must_agree_even_if_separately_sealed()
    {
        Write(OldPublish, "bin/host.dll", "different but separately sealed");
        var authority = Seal();
        var allocations = 0;
        await Assert.ThrowsAsync<IOException>(() => authority.AllocateAsync(() => Observe(),
            () => { allocations++; return Task.FromResult(42); }));
        Assert.Equal(0, allocations);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unreviewed_archive_source_authority_is_refused_before_allocator(bool frontend)
    {
        var allocations = 0;
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var authority = new Web6CutoverBuildAuthority(
                frontend ? new string('1', 40) : Web6CandidateCutoverPlan.FrontendArchiveSha, FrontendTree, Measure(FrontendDist),
                frontend ? Web6CandidateCutoverPlan.BackendArchiveSha : new string('2', 40), BackendTree, Measure(BackendArchive),
                Measure(CurrentPublish), Measure(OldPublish));
            await authority.AllocateAsync(() => Observe(), () => { allocations++; return Task.FromResult(42); });
        });
        Assert.Equal(0, allocations);
    }

    [Fact]
    public async Task Admitted_allocator_failure_is_not_retried()
    {
        var authority = Seal();
        var allocations = 0;
        var failure = new IOException("Synthetic allocation failure.");
        var observed = await Assert.ThrowsAsync<IOException>(() => authority.AllocateAsync<int>(() => Observe(),
            () => { allocations++; throw failure; }));
        Assert.Same(failure, observed);
        Assert.Equal(1, allocations);
    }

    private Web6CutoverBuildAuthority Seal() => new Web6CutoverBuildAuthority(
        Web6CandidateCutoverPlan.FrontendArchiveSha, FrontendTree, new BootstrapTree(FrontendDist).Digest,
        Web6CandidateCutoverPlan.BackendArchiveSha, BackendTree, new BootstrapTree(BackendArchive).Digest,
        new BootstrapTree(CurrentPublish).Digest, new BootstrapTree(OldPublish).Digest);

    private Web6CutoverBuildObservation Observe(string? drift = null) => new Web6CutoverBuildObservation(
        drift == "frontend-sha" ? new string('1', 40) : Web6CandidateCutoverPlan.FrontendArchiveSha,
        drift == "frontend-tree" ? new string('2', 40) : FrontendTree, drift != "frontend-dirty", Measure(FrontendDist),
        drift == "backend-sha" ? new string('3', 40) : Web6CandidateCutoverPlan.BackendArchiveSha,
        drift == "backend-tree" ? new string('4', 40) : BackendTree, drift != "backend-dirty", Measure(BackendArchive),
        Measure(CurrentPublish), Measure(OldPublish));

    private static string Measure(string root) => Web6CandidateFileSet.MeasureTree(root);
    private static void Write(string root, string path, string contents)
    {
        var fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents, new UTF8Encoding(false));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
