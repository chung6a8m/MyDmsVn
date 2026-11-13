using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSelectorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "w6-selector-" + Guid.NewGuid().ToString("N"));
    private const string Tree = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private string FrontendRoot => Path.Combine(_root, "frontend");
    private string CurrentFrontendRoot => Path.Combine(_root, "frontend-current");
    private string BackendRoot => Path.Combine(_root, "backend");
    private string ArchivedDist => Path.Combine(FrontendRoot, "dist");
    private string OldPublish => Path.Combine(_root, "old-publish");
    private string CurrentPublish => Path.Combine(_root, "current-publish");
    private string CandidateDist => Path.Combine(CurrentPublish, "wwwroot");
    private string Asset => Path.Combine(CandidateDist, "index.html");
    private string Evidence => Path.Combine(_root, "evidence");
    private readonly Web6CutoverBuildAuthority _authority;
    private readonly string _publishHash, _pairHash;

    public Web6CutoverSelectorTests()
    {
        Put(ArchivedDist, "index.html", "archived frontend");
        Put(OldPublish, "bin/host.dll", "archived host");
        Put(CurrentPublish, "bin/host.dll", "current host");
        Put(CandidateDist, "index.html", "paired frontend");
        Put(CurrentFrontendRoot, "dist/index.html", "paired frontend");
        Directory.CreateDirectory(BackendRoot);
        Directory.CreateDirectory(Evidence);
        _publishHash = Digest(CurrentPublish);
        _pairHash = Digest(CandidateDist);
        var oldHash = Digest(OldPublish);
        _authority = new Web6CutoverBuildAuthority(Web6CandidateCutoverPlan.FrontendArchiveSha, Tree, Digest(ArchivedDist),
            Web6CandidateCutoverPlan.BackendArchiveSha, Tree, oldHash, _publishHash, oldHash);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Drift_before_each_callback_refuses_and_preserves_every_prior_allocation(int ordinal)
    {
        var calls = new List<Web6CutoverPair>();
        var refusal = (Web6CutoverPair)ordinal;
        var records = new List<(string Path, string Hash)>();
        var selector = Selector();
        if (refusal == Web6CutoverPair.Current) File.WriteAllText(Asset, "pre-lease drift");

        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { }, pair =>
        {
            calls.Add(pair);
            return Task.FromResult("allocation-" + pair);
        }, (pair, identity) =>
        {
            records.Add(Web6BootstrapSelectorEvidence.CutoverOwnership(Evidence, "fixture-run", pair, "database", "confirmed",
                identity, new JObject { ["databaseId"] = 10 + (int)pair }));
            return Task.CompletedTask;
        }, prepare: pair =>
        {
            Assert.Equal((int)pair, records.Count);
            if (pair == refusal && pair != Web6CutoverPair.Current) File.WriteAllText(Asset, "between allocation steps");
        }));

        var expected = new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored }
            .TakeWhile(pair => pair != refusal).ToArray();
        Assert.Equal(expected, calls);
        Assert.Equal(expected.Length, records.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            using var record = ProtectedJsonFile.Open(records[i].Path, records[i].Hash);
            var json = PreparedCandidateReceipt.ReadCanonical(record.Bytes);
            Assert.Equal("allocation-" + expected[i], (string?)json["name"]);
            Assert.Equal(10 + i, (int?)json["identity"]!["databaseId"]);
            Assert.False((bool?)json["deletionAuthority"]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Login_failure_keeps_independent_create_new_identity_for_each_attempt(int failureAt)
    {
        var calls = new List<Web6CutoverPair>();
        var error = new IOException("synthetic login creation failure");
        var task = Web6CutoverSelector.CreateLoginsAsync(pair =>
            Web6BootstrapSelectorEvidence.CreateCutoverLogin(Evidence, "fixture-run", pair, "fixture-" + pair, "fixture-server",
                new string((char)('a' + (int)pair), 32), async () =>
                {
                    calls.Add(pair);
                    Assert.True(File.Exists(Path.Combine(Evidence, $"cutover-{pair.ToString().ToLowerInvariant()}-login-intent.json")));
                    await Task.Yield();
                    if ((int)pair == failureAt) throw error;
                }));
        if (failureAt == 3) await task;
        else Assert.Same(error, await Assert.ThrowsAsync<IOException>(() => task));
        Assert.Equal(Math.Min(failureAt + 1, 3), calls.Count);
        Assert.Equal(Web6CutoverSelector.Pairs.Take(calls.Count), calls);
        foreach (var pair in calls)
        {
            var prefix = Path.Combine(Evidence, $"cutover-{pair.ToString().ToLowerInvariant()}-login-");
            var intentBytes = ProtectedJsonFile.ReadInput(prefix + "intent.json");
            var intent = PreparedCandidateReceipt.ReadCanonical(intentBytes);
            Assert.Equal("fixture-" + pair, (string?)intent["name"]);
            Assert.Equal(new string((char)('a' + (int)pair), 32), (string?)intent["identity"]!["sid"]);
            Assert.False((bool?)intent["deletionAuthority"]);
            if ((int)pair == failureAt)
            {
                Assert.False(File.Exists(prefix + "confirmed.json"));
                continue;
            }
            var confirmationBytes = ProtectedJsonFile.ReadInput(prefix + "confirmed.json");
            using var record = ProtectedJsonFile.Open(prefix + "confirmed.json", ProtectedJsonFile.Digest(confirmationBytes));
            var confirmation = PreparedCandidateReceipt.ReadCanonical(record.Bytes);
            Assert.Equal(ProtectedJsonFile.Digest(intentBytes), (string?)confirmation["identity"]!["intentSha256"]);
            Assert.Equal((string?)intent["identity"]!["sid"], (string?)confirmation["identity"]!["sid"]);
            Assert.False((bool?)confirmation["deletionAuthority"]);
        }
        Assert.Equal(calls.Count + failureAt, Directory.GetFiles(Evidence).Length);
    }

    [Fact]
    public async Task Sealed_files_resist_concurrent_write_and_replacement_through_allocator()
    {
        var writes = new List<Exception?>();
        var calls = new List<Web6CutoverPair>();
        var observations = 0;
        await Selector().AllocateAsync(() =>
        {
            observations++;
            var peer = Task.Run(() =>
            {
                writes.Add(Record.Exception(() => File.WriteAllText(Asset, "changed")));
                writes.Add(Record.Exception(() => File.Move(Asset, Asset + ".moved")));
                writes.Add(Record.Exception(() => Directory.Move(CandidateDist, CandidateDist + ".moved")));
            });
            peer.GetAwaiter().GetResult();
        }, async pair =>
        {
            calls.Add(pair);
            var during = await Task.Run(() => Record.Exception(() => File.WriteAllText(Asset, "changed during allocator")));
            Assert.IsType<IOException>(during);
            Assert.Equal("paired frontend", File.ReadAllText(Asset));
            return pair;
        }, (_, _) => Task.CompletedTask);

        Assert.Equal(3, observations);
        Assert.Equal(new[] { Web6CutoverPair.Current, Web6CutoverPair.Old, Web6CutoverPair.Restored }, calls);
        Assert.Equal(9, writes.Count);
        Assert.All(writes, error => Assert.IsType<IOException>(error));
    }

    [Fact]
    public async Task Archive_wrong_git_root_is_refused_before_allocator()
    {
        var calls = 0;
        var selector = Selector(root => (root == FrontendRoot ? _root : root, Sha(root), Tree));
        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
            pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Attributes_only_junction_drift_during_source_verification_refuses_allocator()
    {
        var empty = Directory.CreateDirectory(Path.Combine(CurrentPublish, "empty")).FullName;
        var target = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        var redirected = Path.Combine(target, "foreign.txt");
        File.WriteAllText(redirected, "foreign");
        var installed = false;
        var calls = 0;
        try
        {
            await Assert.ThrowsAsync<IOException>(() => Selector().AllocateAsync(() =>
            {
                Task.Run(() =>
                {
                    const uint writeAttributes = 0x100, openReparsePoint = 0x00200000, backupSemantics = 0x02000000;
                    using var directory = CreateFile(empty, writeAttributes, 7, IntPtr.Zero, 3,
                        openReparsePoint | backupSemantics, IntPtr.Zero);
                    Assert.False(directory.IsInvalid);
                    var substitute = @"\??\" + target;
                    var paths = Encoding.Unicode.GetBytes(substitute + "\0" + target + "\0");
                    using var buffer = new MemoryStream();
                    using var writer = new BinaryWriter(buffer);
                    writer.Write(0xa0000003u);
                    writer.Write((ushort)(paths.Length + 8));
                    writer.Write((ushort)0);
                    writer.Write((ushort)0);
                    writer.Write((ushort)(substitute.Length * 2));
                    writer.Write((ushort)((substitute.Length + 1) * 2));
                    writer.Write((ushort)(target.Length * 2));
                    writer.Write(paths);
                    var reparse = buffer.ToArray();
                    installed = DeviceIoControl(directory, 0x000900a4, reparse, reparse.Length,
                        IntPtr.Zero, 0, out _, IntPtr.Zero);
                    Assert.True(installed);
                }).GetAwaiter().GetResult();
            }, pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
            Assert.True(installed);
            Assert.Equal(0, calls);
            Assert.Equal("foreign", File.ReadAllText(redirected));
        }
        finally { if (installed) Directory.Delete(empty); }
    }

    [Fact]
    public async Task Archive_junction_is_refused_before_git_and_allocator()
    {
        var original = FrontendRoot + "-original";
        Directory.Move(FrontendRoot, original);
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + FrontendRoot + "\" \"" + original + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(process);
        Assert.True(process!.WaitForExit(5000));
        Assert.Equal(0, process.ExitCode);
        try
        {
            var calls = 0;
            var selector = Selector(_ => throw new InvalidOperationException("Git must not consume a junction."));
            await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
                pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
            Assert.Equal(0, calls);
        }
        finally { Directory.Delete(FrontendRoot); }
    }

    [Fact]
    public async Task Failure_does_not_retry_or_start_a_later_callback()
    {
        var calls = new List<string>();
        var failure = new IOException("synthetic allocator failure");
        var error = await Assert.ThrowsAsync<IOException>(() => Selector().AllocateAsync(() => { }, pair =>
        {
            calls.Add("allocate " + pair);
            return pair == Web6CutoverPair.Old ? Task.FromException<Web6CutoverPair>(failure) : Task.FromResult(pair);
        }, (pair, _) =>
        {
            calls.Add("seal " + pair);
            return Task.CompletedTask;
        }));
        Assert.Same(failure, error);
        Assert.Equal(new[] { "allocate Current", "seal Current", "allocate Old" }, calls);
    }

    [Fact]
    public async Task Current_frontend_must_match_pair_before_allocation_and_composition()
    {
        var selector = Selector(pairHash: Digest(ArchivedDist));
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => selector.AllocateAsync(() => { },
            pair => { calls++; return Task.FromResult(pair); }, (_, _) => Task.CompletedTask));
        Assert.Equal(0, calls);
        Assert.Throws<IOException>(() => selector.Compose(Path.Combine(_root, "deployment"), cached: false, old: false));
        Assert.False(Directory.Exists(Path.Combine(_root, "deployment")));
    }

    [Fact]
    public void Deployment_uses_paired_publish_and_cached_archive_after_worktree_dist_changes()
    {
        Put(CurrentFrontendRoot, "dist/index.html", "late worktree dist mutation");
        var selector = Selector();
        foreach (var (name, cached, old, frontend, host) in new[]
        {
            ("current", false, false, "paired frontend", "current host"),
            ("restored", false, false, "paired frontend", "current host"),
            ("old", false, true, "paired frontend", "archived host"),
            ("cached", true, false, "archived frontend", "current host")
        })
        {
            var destination = Path.Combine(_root, name);
            selector.Compose(destination, cached, old);
            Assert.Equal(frontend, File.ReadAllText(Path.Combine(destination, "wwwroot", "index.html")));
            Assert.Equal(host, File.ReadAllText(Path.Combine(destination, "bin", "host.dll")));
        }
        File.WriteAllText(Asset, "sealed publish drift");
        var refused = Path.Combine(_root, "refused");
        Assert.Throws<IOException>(() => selector.Compose(refused, cached: false, old: false));
        Assert.False(Directory.Exists(refused));
    }

    private Web6CutoverSelector Selector(Func<string, (string Root, string Sha, string Tree)>? git = null, string? pairHash = null) =>
        new Web6CutoverSelector(_authority, FrontendRoot, BackendRoot, ArchivedDist, OldPublish, CurrentPublish,
            _publishHash, pairHash ?? _pairHash, git ?? (root => (root.Replace('\\', '/'), Sha(root), Tree)));
    private string Sha(string root) => root == FrontendRoot ? Web6CandidateCutoverPlan.FrontendArchiveSha : Web6CandidateCutoverPlan.BackendArchiveSha;
    private static string Digest(string root) => new BootstrapTree(root).Digest;
    private static void Put(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint control, byte[] input, int inputSize,
        IntPtr output, int outputSize, out uint returned, IntPtr overlapped);
}
