using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CandidateSafetyTests
{
    private readonly ITestOutputHelper _output;
    public Web6CandidateSafetyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Manifest_is_create_new_sealed_and_removed_only_by_exact_owner()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        control.Verify();
        Assert.Throws<InvalidOperationException>(() => Write(control));
        control.RemoveAfterVerifiedAbsence();
        Assert.False(Directory.Exists(control.ControlPath));
    }

    [Fact]
    public void Uncertain_scope_disposal_retains_the_original_manifest()
    {
        var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        control.Dispose();
        Assert.True(File.Exists(Path.Combine(control.ControlPath, "allocation.txt")));
        // Original in-memory identity is still held by this pure test; no SQL exists.
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public void Manifest_handle_refuses_replacement_and_data_tamper()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        var file = Path.Combine(control.ControlPath, "allocation.txt");
        Assert.Throws<IOException>(() => File.Move(file, file + ".replacement"));
        Assert.Throws<IOException>(() => File.WriteAllText(file, "tampered"));
        control.Verify();
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public void Manifest_refuses_acl_drift_and_retains_until_original_acl_is_restored()
    {
        using var parent = SqlTestOwnershipManifest.OwnedAclParent.Create(Path.GetTempPath());
        using var control = parent.CreateTarget();
        Write(control);
        // Independent create-new/flush/readback record must exist outside this case first.
        using var authority = control.PreserveBeforeAclMutation(parent, marker => _output.WriteLine(marker));
        var changed = Directory.GetAccessControl(control.ControlPath, SqlTestPrivateRecord.Sections);
        changed.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        authority.RunMutation(() => Directory.SetAccessControl(control.ControlPath, changed),
            () => Assert.Throws<IOException>(() => control.RemoveAfterVerifiedAbsence()));
        control.Verify();
        control.RemoveAfterVerifiedAbsence();
        parent.RemoveAfterVerifiedChildren();
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("traversal")]
    [InlineData("long")]
    public void Manifest_refuses_noncanonical_or_overbudget_parent(string kind)
    {
        var parent = kind == "relative" ? "." : kind == "traversal" ? Path.GetTempPath() + "child\\.." : "C:\\" + new string('a', 230);
        Assert.Throws<IOException>(() => SqlTestOwnershipManifest.Create(parent));
        Assert.Throws<IOException>(() => SqlTestOwnershipManifest.RejectReparse(FileAttributes.ReparsePoint | FileAttributes.Directory));
    }

    [Fact]
    public void Manifest_refuses_unexpected_children_without_removing_any_entry()
    {
        using var control = SqlTestOwnershipManifest.Create(Path.GetTempPath());
        Write(control);
        var extra = Path.Combine(control.ControlPath, "unexpected.txt");
        File.WriteAllText(extra, "test-owned");
        try { Assert.Throws<IOException>(() => control.RemoveAfterVerifiedAbsence()); Assert.True(File.Exists(extra)); }
        finally { File.Delete(extra); }
        control.RemoveAfterVerifiedAbsence();
    }

    [Fact]
    public async Task Exact_net48_http_and_crash_graph_loads_without_SQL()
    {
        var expected = Environment.GetEnvironmentVariable("MYDMSVN_WEB6_EXPECTED_EVIDENCE_TEMP");
        Assert.False(string.IsNullOrWhiteSpace(expected));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Environment.GetEnvironmentVariable("TEMP")!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        RequireMatch(string.Equals(Path.GetFullPath(expected!).TrimEnd('\\'), Environment.GetEnvironmentVariable("TMP")!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        var graph = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "CrashHost"), "*", SearchOption.AllDirectories);
        var maximum = graph.Max(path => path.Length);
        Assert.True(maximum <= 240, "Crash-host dependency path budget exceeded.");
        Assert.True(Path.GetTempPath().TrimEnd('\\').Length + 180 <= 240, "Projected transient path budget exceeded.");
        WebAuthHttpSqlTests.Scenario.LoadWithoutSql();
        var child = await WebOperationCrashProcess.StartAsync(new JObject {
            ["connectionString"]="WEB5-no-SQL-may-open", ["phase"]="none", ["method"]="GET",
            ["path"]="operations/context", ["body"]=JValue.CreateNull(), ["access"]=JValue.CreateNull(), ["headers"]=new JObject()
        });
        await child.CloseAsync();
        Assert.False(WebOperationCrashProcess.IsPoisoned);
        _output.WriteLine("WEB6_PATH_PREFLIGHT sql=0 graphLoaded=1 childrenDrained=1 projectedFullPathMaximum=240 actualGraphMaximum={0}", maximum);
    }

    [Fact]
    public void Candidate_record_is_create_new_and_disposition_cannot_change_the_sealed_bytes()
    {
        using var fixture = new CandidateFixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Verify());
        fixture.Seal();
        var original = File.ReadAllBytes(fixture.ManifestPath);
        Assert.Throws<IOException>(() => Web6CandidateManifest.CreateNew(fixture.ControlPath, fixture.Inputs));
        Assert.Throws<InvalidOperationException>(() => fixture.Seal());
        Assert.Throws<IOException>(() => File.WriteAllText(fixture.ManifestPath, "foreignField=value"));
        fixture.Verify();
        fixture.Manifest.RecordDisposition(Web6CandidateStage.Chromium, Web6CandidateOutcome.Passed);
        Assert.Throws<InvalidOperationException>(() => fixture.Manifest.RecordDisposition(Web6CandidateStage.Chromium, Web6CandidateOutcome.Failed));
        Assert.Equal(original, File.ReadAllBytes(fixture.ManifestPath));
        Assert.Single(Directory.GetFiles(fixture.ControlPath, "disposition-*.record"));
        fixture.Verify();
        fixture.Manifest.Dispose();
        Assert.Throws<ObjectDisposedException>(() => fixture.Manifest.RecordDisposition(Web6CandidateStage.Edge, Web6CandidateOutcome.Passed));
        Assert.True(File.Exists(fixture.ManifestPath));
    }

    [Theory]
    [InlineData("extra-child")]
    [InlineData("root-acl")]
    [InlineData("record-acl")]
    public void Candidate_preflight_refuses_unexpected_private_filesystem_state(string drift)
    {
        using var fixture = new CandidateFixture();
        fixture.Seal();
        fixture.Verify();
        var extra = Path.Combine(fixture.ControlPath, "foreign-field.txt");
        if (drift == "extra-child")
        {
            File.WriteAllText(extra, "test-owned");
            Assert.Throws<IOException>(() => fixture.Verify());
            Assert.True(File.Exists(extra));
            return;
        }

        var isRoot = drift == "root-acl";
        var original = isRoot ? (FileSystemSecurity)Directory.GetAccessControl(fixture.ControlPath, SqlTestPrivateRecord.Sections)
            : File.GetAccessControl(fixture.ManifestPath, SqlTestPrivateRecord.Sections);
        var changed = isRoot ? (FileSystemSecurity)SqlTestPrivateRecord.RestoreDirectory(original.GetSecurityDescriptorBinaryForm())
            : SqlTestPrivateRecord.RestoreFile(original.GetSecurityDescriptorBinaryForm());
        changed.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        try
        {
            if (isRoot) Directory.SetAccessControl(fixture.ControlPath, (DirectorySecurity)changed);
            else File.SetAccessControl(fixture.ManifestPath, (FileSecurity)changed);
            Assert.Throws<IOException>(() => fixture.Verify());
        }
        finally
        {
            if (isRoot) Directory.SetAccessControl(fixture.ControlPath, SqlTestPrivateRecord.RestoreDirectory(original.GetSecurityDescriptorBinaryForm()));
            else File.SetAccessControl(fixture.ManifestPath, SqlTestPrivateRecord.RestoreFile(original.GetSecurityDescriptorBinaryForm()));
        }
        // Restoring a newly created directory may normalize NTFS descriptor flags. Retain
        // the exact descriptor pin and do not reuse a control root after the ACL experiment.
        if (!isRoot) fixture.Verify();
    }

    [Fact]
    public void Candidate_filesystem_gate_rejects_wrong_owner_even_with_the_expected_private_DACL()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        var descriptor = SqlTestPrivateRecord.DirectoryAcl(owner);
        Web6CandidateManifest.RequirePrivateOwner(descriptor, owner);
        descriptor.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        Assert.Throws<IOException>(() => Web6CandidateManifest.RequirePrivateOwner(descriptor, owner));
    }

    [Fact]
    public void Candidate_create_new_rejects_a_reparse_ancestor_before_writing_in_the_target()
    {
        using var fixture = new CandidateFixture();
        var junction = Path.Combine(fixture.WorkPath, "junction");
        var target = Directory.CreateDirectory(Path.Combine(fixture.WorkPath, "junction-target")).FullName;
        var nested = Directory.CreateDirectory(Path.Combine(target, "nested")).FullName;
        using var command = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /c mklink /J \"" + junction + "\" \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true });
        Assert.NotNull(command);
        Assert.True(command!.WaitForExit(5000));
        Assert.Equal(0, command.ExitCode);
        try
        {
            Assert.Throws<IOException>(() => Web6CandidateManifest.CreateNew(Path.Combine(junction, "nested", "refused"), fixture.Inputs));
            Assert.Empty(Directory.GetFileSystemEntries(nested));
        }
        finally { Directory.Delete(junction); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Candidate_create_new_refuses_an_ancestor_with_an_outstanding_delete_handle(bool directParent)
    {
        using var fixture = new CandidateFixture();
        var grandparent = Directory.CreateDirectory(Path.Combine(fixture.WorkPath, "ancestor")).FullName;
        var parent = Directory.CreateDirectory(Path.Combine(grandparent, "parent")).FullName;
        var candidate = Path.Combine(parent, "candidate");
        const uint deleteAndListDirectory = 0x10001, shareAll = 7, openExisting = 3, openDirectory = 0x02000000;
        using (var conflicting = OpenTestDirectory(directParent ? parent : grandparent,
            deleteAndListDirectory, shareAll, IntPtr.Zero, openExisting, openDirectory, IntPtr.Zero))
        {
            Assert.False(conflicting.IsInvalid);
            Assert.Throws<IOException>(() => Web6CandidateManifest.CreateNew(candidate, fixture.Inputs).Dispose());
            Assert.False(Directory.Exists(candidate));
        }

        using var manifest = Web6CandidateManifest.CreateNew(candidate, fixture.Inputs);
        Assert.True(Directory.Exists(candidate));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle OpenTestDirectory(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [Theory]
    [InlineData("backend-head")]
    [InlineData("frontend-tree")]
    [InlineData("backend-dirty")]
    [InlineData("frontend-dirty")]
    [InlineData("schema")]
    [InlineData("certificate")]
    [InlineData("unknown-sql-owner")]
    [InlineData("wrong-sql-receipt")]
    [InlineData("second-worker")]
    [InlineData("wrong-worker")]
    [InlineData("wrong-controller")]
    [InlineData("wrong-queue")]
    [InlineData("second-queue")]
    [InlineData("unknown-audit-launch")]
    [InlineData("wrong-audit-process")]
    [InlineData("wrong-audit-start")]
    [InlineData("wrong-audit-root")]
    [InlineData("wrong-audit-path")]
    [InlineData("wrong-audit-candidate")]
    public void Candidate_preflight_refuses_changed_or_unproven_live_identity(string drift)
    {
        using var fixture = new CandidateFixture();
        fixture.Seal();
        Assert.ThrowsAny<IOException>(() => fixture.Verify(drift));
        fixture.Verify();
    }

    [Theory]
    [InlineData("dll")]
    [InlineData("web-config")]
    [InlineData("iis-config")]
    [InlineData("backend-lock")]
    [InlineData("frontend-lock")]
    [InlineData("contract")]
    [InlineData("dist")]
    [InlineData("published-extra")]
    [InlineData("audit-missing")]
    public void Candidate_preflight_refuses_artifact_drift_or_missing_audit(string drift)
    {
        using var fixture = new CandidateFixture();
        fixture.Seal();
        if (drift == "audit-missing") File.Delete(fixture.Manifest.AuditPath);
        else File.WriteAllText(fixture.ArtifactPaths[drift], "different artifact");
        Assert.ThrowsAny<IOException>(() => fixture.Verify());
    }

    [Fact]
    public void Candidate_seal_refuses_a_published_dist_mismatch_before_writing_the_manifest()
    {
        using var fixture = new CandidateFixture();
        File.WriteAllText(fixture.ArtifactPaths["published-extra"], "extra artifact");
        Assert.ThrowsAny<IOException>(() => fixture.Seal());
        Assert.False(File.Exists(fixture.ManifestPath));
    }

    [Theory]
    [InlineData("audit-deleted")]
    [InlineData("dll-changed")]
    [InlineData("extra-child")]
    public void Candidate_preflight_refuses_filesystem_drift_during_observation(string drift)
    {
        using var fixture = new CandidateFixture();
        fixture.Seal();
        var observed = false;
        Assert.ThrowsAny<IOException>(() => fixture.Verify(duringObservation: () =>
        {
            observed = true;
            if (drift == "audit-deleted") File.Delete(fixture.Manifest.AuditPath);
            else if (drift == "dll-changed") File.WriteAllText(fixture.ArtifactPaths["dll"], "changed during observation");
            else File.WriteAllText(Path.Combine(fixture.ControlPath, "foreign.record"), "created during observation");
        }));
        Assert.True(observed);
    }

    private sealed class CandidateFixture : IDisposable
    {
        private const int ProcessId = 17242;
        private const string QueueName = "owned-queue";
        private static readonly DateTime ProcessStartUtc = new DateTime(2026, 11, 10, 1, 2, 3, DateTimeKind.Utc);
        private static readonly Uri Origin = new Uri("https://localhost:44321/");
        private static readonly string SqlReceipt = new string('8', 64);
        private readonly Web6CandidateFileSet _files;
        internal string WorkPath { get; } = Path.Combine(Path.GetTempPath(), "w6m-" + Guid.NewGuid().ToString("N"));
        internal string ControlPath => Path.Combine(WorkPath, "record");
        internal string ManifestPath => Path.Combine(ControlPath, "candidate.manifest");
        internal Dictionary<string, string> ArtifactPaths { get; }
        internal Web6CandidateInputs Inputs { get; } = new Web6CandidateInputs(
            "web6-test-01", (new string('1', 40), new string('2', 40)), (new string('3', 40), new string('4', 40)),
            new string('5', 64), Origin, new string('6', 64), "Web6PairedCandidate", 600, 60);
        internal Web6CandidateManifest Manifest { get; }

        internal CandidateFixture()
        {
            Directory.CreateDirectory(WorkPath);
            Manifest = Web6CandidateManifest.CreateNew(ControlPath, Inputs);
            using (var identity = WindowsIdentity.GetCurrent())
            using (new FileStream(Manifest.AuditPath, FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None,
                4096, FileOptions.None, SqlTestPrivateRecord.FileAcl(identity.User!))) { }
            ArtifactPaths = new Dictionary<string, string> {
                ["dll"] = WriteFile("bin/backend.dll"), ["web-config"] = WriteFile("Web.config"),
                ["iis-config"] = WriteFile("applicationhost.config"), ["backend-lock"] = WriteFile("backend/a/packages.lock.json"),
                ["frontend-lock"] = WriteFile("frontend/package-lock.json"), ["contract"] = WriteFile("web4/one.json"),
                ["dist"] = WriteFile("dist/assets/main.js"), ["published-extra"] = Path.Combine(WorkPath, "wwwroot", "foreign.js")
            };
            WriteFile("backend/b/packages.lock.json"); WriteFile("web5/two.json"); WriteFile("web6/three.json");
            WriteFile("wwwroot/assets/main.js");
            _files = new Web6CandidateFileSet(ArtifactPaths["dll"], ArtifactPaths["web-config"], ArtifactPaths["iis-config"],
                Path.Combine(WorkPath, "backend"), new[] { "a/packages.lock.json", "b/packages.lock.json" }, ArtifactPaths["frontend-lock"],
                Path.Combine(WorkPath, "web4"), Path.Combine(WorkPath, "web5"), Path.Combine(WorkPath, "web6"),
                Path.Combine(WorkPath, "dist"), Path.Combine(WorkPath, "wwwroot"));
        }

        internal void Seal() => Manifest.SealArtifacts(_files, SqlReceipt);

        internal void Verify(string? drift = null, Action? duringObservation = null) =>
            Manifest.VerifyBeforeAdmission(ProcessId, ProcessStartUtc, QueueName, () =>
        {
            duringObservation?.Invoke();
            var queues = new List<Web5HttpRequestQueue> { Queue(
                drift == "wrong-queue" ? "other-queue" : QueueName,
                drift == "wrong-controller" ? ProcessId + 1 : ProcessId,
                drift == "second-worker" ? new[] { ProcessId, ProcessId + 1 } : new[] { drift == "wrong-worker" ? ProcessId + 1 : ProcessId }) };
            if (drift == "second-queue") queues.Add(Queue("other-queue", ProcessId + 1, new[] { ProcessId + 1 }));
            var auditLaunch = drift == "unknown-audit-launch" ? null : new Web6CandidateAuditLaunch(
                drift == "wrong-audit-process" ? ProcessId + 1 : ProcessId,
                drift == "wrong-audit-start" ? ProcessStartUtc.AddSeconds(1) : ProcessStartUtc,
                drift == "wrong-audit-root" ? WorkPath : ControlPath,
                drift == "wrong-audit-path" ? Path.Combine(ControlPath, "foreign.log") : Manifest.AuditPath,
                drift == "wrong-audit-candidate" ? "other-candidate" : Inputs.CandidateId);
            return new Web6CandidateObservation(
                (drift == "backend-head" ? new string('9', 40) : new string('1', 40), new string('2', 40), drift != "backend-dirty"),
                (new string('3', 40), drift == "frontend-tree" ? new string('9', 40) : new string('4', 40), drift != "frontend-dirty"),
                new string(drift == "schema" ? '9' : '5', 64), new string(drift == "certificate" ? '9' : '6', 64),
                drift == "unknown-sql-owner" ? null : drift == "wrong-sql-receipt" ? new string('9', 64) : SqlReceipt, queues, auditLaunch);
        });

        private static Web5HttpRequestQueue Queue(string name, int controller, int[] workers) => new Web5HttpRequestQueue(name, true,
            workers.Length, controller, workers, new[] { new Web5HttpUrlGroup(true, name, new[] { Origin.AbsoluteUri }) });

        private string WriteFile(string relative)
        {
            var path = Path.Combine(WorkPath, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "test file");
            return path;
        }

        public void Dispose()
        {
            Manifest.Dispose();
            Directory.Delete(WorkPath, recursive: true);
        }
    }

    private static void RequireMatch(bool match) => Assert.True(match, "Evidence environment mismatch.");

    private static void Write(SqlTestOwnershipManifest control) => control.WriteVerifiedRecord(
        "MyDmsVn_Test_11111111111141118111111111111111", "test-server", 17,
        new DateTime(2026, 11, 9, 1, 2, 3), "22222222222242228222222222222222", false, DateTimeOffset.UtcNow);
}
