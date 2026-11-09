using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MyDmsVn.WebOperation.Operator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationMaintenanceEvidenceTests : IDisposable
{
    private const string CaseId = "case-exact-01";
    private const string EnvironmentName = "PrivateTestEnvironment";
    private const string Database = "PrivateTestDatabase";
    private const string Login = "PrivateSqlLogin";
    private const string Operation = "80ac4098-f246-4bbd-ae6a-76e086d0865b";
    private const string Correlation = "e9d1e359-b617-47ce-bfa2-430a7a32b1d1";
    private static readonly string Fingerprint = new string('a', 64);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "mydmsvn-b03-evidence-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _utc = new DateTimeOffset(2026, 11, 9, 3, 4, 5, TimeSpan.Zero);

    public WebOperationMaintenanceEvidenceTests()
    {
        CreatePrivateDirectory(_workspace);
        CreatePrivateDirectory(PrimaryRoot);
        CreatePrivateDirectory(BackupRoot);
    }

    private string PrimaryRoot => Path.Combine(_workspace, "primary");
    private string BackupRoot => Path.Combine(_workspace, "backup");
    private string PrimaryCase => Path.Combine(PrimaryRoot, CaseId);
    private string BackupCase => Path.Combine(BackupRoot, CaseId);

    [Fact]
    public void Policy_refuses_relative_same_nested_and_noncanonical_roots_and_wrong_retention()
    {
        var nested = Path.Combine(PrimaryRoot, "nested");
        CreatePrivateDirectory(nested);
        Assert.Throws<ArgumentException>(() => new MaintenanceEvidencePolicy("primary", BackupRoot, Retention));
        Assert.Throws<ArgumentException>(() => new MaintenanceEvidencePolicy(PrimaryRoot, PrimaryRoot, Retention));
        Assert.Throws<ArgumentException>(() => new MaintenanceEvidencePolicy(PrimaryRoot, nested, Retention));
        Assert.Throws<ArgumentException>(() => new MaintenanceEvidencePolicy(PrimaryRoot + @"\..\primary", BackupRoot, Retention));
        Assert.Throws<ArgumentException>(() => new MaintenanceEvidencePolicy(Path.Combine(PrimaryRoot, "NUL.txt"), BackupRoot, Retention));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MaintenanceEvidencePolicy(PrimaryRoot, BackupRoot, Retention.Add(TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public async Task Policy_and_new_case_have_protected_current_user_and_administrators_acl()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var store = Store();
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);

        foreach (var directory in new[] { PrimaryRoot, BackupRoot, PrimaryCase })
        {
            var acl = Directory.GetAccessControl(directory);
            Assert.True(acl.AreAccessRulesProtected);
            Assert.Equal(identity.User, acl.GetOwner(typeof(SecurityIdentifier)));
            var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            Assert.Equal(2, rules.Length);
            Assert.Contains(rules, rule => rule.IdentityReference.Equals(identity.User));
            Assert.Contains(rules, rule => rule.IdentityReference.Equals(admins));
            Assert.All(rules, rule =>
            {
                Assert.False(rule.IsInherited);
                Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
            });
        }

        var aclWithEveryone = Directory.GetAccessControl(BackupRoot);
        aclWithEveryone.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        Directory.SetAccessControl(BackupRoot, aclWithEveryone);
        Assert.Throws<IOException>(() => new MaintenanceEvidencePolicy(PrimaryRoot, BackupRoot, Retention));
    }

    [Fact]
    public void Policy_refuses_reparse_ancestry_even_when_the_target_has_a_private_acl()
    {
        var alias = Path.Combine(_workspace, "alias");
        CreateJunction(alias, PrimaryRoot);
        try
        {
            var inside = Path.Combine(PrimaryRoot, "inside");
            CreatePrivateDirectory(inside);
            Assert.Throws<IOException>(() => new MaintenanceEvidencePolicy(Path.Combine(alias, "inside"), BackupRoot, Retention));
        }
        finally
        {
            Directory.Delete(alias, false);
        }
    }

    [Fact]
    public async Task Attempt_requires_create_new_case_even_if_an_existing_case_is_private()
    {
        var store = Store();
        CreatePrivateDirectory(PrimaryCase);
        await Assert.ThrowsAsync<IOException>(() => store.WriteAttemptAsync(Attempt(), CancellationToken.None));
        Assert.Empty(Directory.GetFileSystemEntries(PrimaryCase));
    }

    [Fact]
    public async Task Attempt_outcomes_and_both_manifests_bind_canonical_sanitized_bytes_and_sequence()
    {
        var store = Store();
        var attempt = await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => store.WriteOutcomeAsync(Readback(), CancellationToken.None));
        _utc = _utc.AddSeconds(1);
        var commit = await store.WriteOutcomeAsync(Transaction(), CancellationToken.None);
        _utc = _utc.AddSeconds(1);
        var readback = await store.WriteOutcomeAsync(Readback(), CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => store.WriteOutcomeAsync(Readback(), CancellationToken.None));
        var receipt = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);

        Assert.Equal(CaseId, receipt.CaseId);
        Assert.Equal(MaintenanceEvidenceDelivery.Delivered, receipt.Delivery);
        Assert.Equal(new[] { attempt.EventHash!, commit.EventHash!, readback.EventHash! }, receipt.EventHashes);
        var primaryBytes = File.ReadAllBytes(Path.Combine(PrimaryCase, "manifest.json"));
        var backupBytes = File.ReadAllBytes(Path.Combine(BackupCase, "manifest.json"));
        Assert.Equal(Sha(primaryBytes), receipt.PrimaryManifestHash);
        Assert.Equal(Sha(backupBytes), receipt.BackupManifestHash);
        Assert.NotEqual(receipt.PrimaryManifestHash, receipt.BackupManifestHash);
        AssertSanitized(primaryBytes);
        AssertSanitized(backupBytes);

        using var primary = JsonDocument.Parse(primaryBytes);
        using var backup = JsonDocument.Parse(backupBytes);
        var primaryManifest = primary.RootElement;
        var backupManifest = backup.RootElement;
        Assert.Equal(CaseId, primaryManifest.GetProperty("caseId").GetString());
        Assert.Equal(primaryManifest.GetProperty("copyId").GetString(), backupManifest.GetProperty("peerCopyId").GetString());
        Assert.Equal(backupManifest.GetProperty("copyId").GetString(), primaryManifest.GetProperty("peerCopyId").GetString());
        Assert.NotEqual(primaryManifest.GetProperty("copyId").GetString(), backupManifest.GetProperty("copyId").GetString());
        Assert.Equal(primaryManifest.GetProperty("events").GetRawText(), backupManifest.GetProperty("events").GetRawText());
        Assert.Equal(_utc, primaryManifest.GetProperty("utc").GetDateTimeOffset());

        var events = primaryManifest.GetProperty("events").EnumerateArray().ToArray();
        var phases = new[] { "Attempt", "Transaction", "Readback" };
        Assert.Equal(phases.Length, events.Length);
        for (var index = 0; index < events.Length; index++)
        {
            var name = events[index].GetProperty("name").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(PrimaryCase, name));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(BackupCase, name)));
            Assert.Equal(Sha(bytes), events[index].GetProperty("sha256").GetString());
            Assert.Equal(Sha(bytes), receipt.EventHashes[index]);
            AssertSanitized(bytes);
            using var evidence = JsonDocument.Parse(bytes);
            Assert.Equal(phases[index], evidence.RootElement.GetProperty("phase").GetString());
            Assert.Equal("80ac4098", evidence.RootElement.GetProperty("operation").GetString());
            Assert.Equal("e9d1e359", evidence.RootElement.GetProperty("correlation").GetString());
            Assert.Equal(Sha(Encoding.UTF8.GetBytes(Login)), evidence.RootElement.GetProperty("loginHash").GetString());
            Assert.Equal(Sha(Encoding.UTF8.GetBytes(EnvironmentName)), evidence.RootElement.GetProperty("environmentHash").GetString());
            Assert.Equal(Sha(Encoding.UTF8.GetBytes(Database)), evidence.RootElement.GetProperty("databaseHash").GetString());
            Assert.Equal(Sha(Enumerable.Repeat((byte)0xaa, 32).ToArray()), evidence.RootElement.GetProperty("fingerprintHash").GetString());
        }
    }

    [Fact]
    public async Task Reconciliation_can_append_readback_and_rejects_a_different_correlation()
    {
        var store = Store();
        await store.WriteAttemptAsync(Reconcile(MaintenanceEvidencePhase.Attempt, MaintenanceReadback.NotRun, Correlation), CancellationToken.None);
        await Assert.ThrowsAsync<IOException>(() => store.WriteOutcomeAsync(Reconcile(MaintenanceEvidencePhase.Readback,
            MaintenanceReadback.BusinessCommittedResultUnavailable, "e9d1e359-b617-47ce-bfa2-430a7a32b1d2"), CancellationToken.None));

        await store.WriteOutcomeAsync(Reconcile(MaintenanceEvidencePhase.Readback,
            MaintenanceReadback.BusinessCommittedResultUnavailableSuperseded, Correlation), CancellationToken.None);
        var receipt = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);
        Assert.Equal(MaintenanceEvidenceDelivery.Delivered, receipt.Delivery);
        Assert.Equal(2, receipt.EventHashes.Count);
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(PrimaryCase, "03-readback.json")));
        Assert.Equal("reconcile-committed", json.RootElement.GetProperty("command").GetString());
        Assert.Equal("NotStarted", json.RootElement.GetProperty("transactionCertainty").GetString());
        Assert.Equal("BusinessCommittedResultUnavailableSuperseded", json.RootElement.GetProperty("readback").GetString());
    }

    [Theory]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("Lpt9")]
    public void Evidence_case_id_refuses_windows_device_names(string caseId) =>
        Assert.Throws<ArgumentException>(() => Reconcile(MaintenanceEvidencePhase.Attempt, MaintenanceReadback.NotRun, Correlation, caseId));

    [Fact]
    public void Compaction_preview_cannot_record_a_committed_transaction() =>
        Assert.Throws<ArgumentException>(() => Record(MaintenanceEvidencePhase.Transaction, MaintenanceTransactionCertainty.Committed,
            MaintenanceReadback.NotRun, execute: false));

    [Fact]
    public async Task Backup_root_identity_is_revalidated_before_another_primary_event()
    {
        var store = Store();
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        Directory.Move(BackupRoot, Path.Combine(_workspace, "previous-backup"));
        CreatePrivateDirectory(BackupRoot);

        await Assert.ThrowsAsync<IOException>(() => store.WriteOutcomeAsync(Transaction(), CancellationToken.None));
        Assert.Single(Directory.GetFileSystemEntries(PrimaryCase));
        Assert.Empty(Directory.GetFileSystemEntries(BackupRoot));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaintenanceEvidenceStore_revalidates_identity_before_every_write_and_copy(bool reparse)
    {
        var store = Store();
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        var retained = Path.Combine(PrimaryRoot, "retained");
        Directory.Move(PrimaryCase, retained);
        if (reparse)
            CreateJunction(PrimaryCase, retained);
        else
            CreatePrivateDirectory(PrimaryCase);

        try
        {
            await Assert.ThrowsAsync<IOException>(() => store.WriteOutcomeAsync(Transaction(), CancellationToken.None));
            var failed = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);
            Assert.Equal(MaintenanceEvidenceDelivery.Escalate, failed.Delivery);
            Assert.False(Directory.Exists(BackupCase));
            Assert.Single(Directory.GetFileSystemEntries(retained));
            if (!reparse)
                Assert.Empty(Directory.GetFileSystemEntries(PrimaryCase));
        }
        finally
        {
            if (reparse)
                Directory.Delete(PrimaryCase, false);
        }
    }

    [Fact]
    public async Task Purge_requires_expired_primary_and_backup_manifests_with_exact_hashes()
    {
        var receipt = await Deliver();
        _utc = _utc.AddDays(90);
        var sibling = Path.Combine(PrimaryRoot, CaseId + "-keep");
        CreatePrivateDirectory(sibling);

        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, primaryHash: new string('0', 64)));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, backupHash: new string('0', 64)));
        AssertCopiesIntact(receipt);
        Assert.Equal(MaintenanceEvidencePurge.Purged, await Purge(receipt));
        Assert.False(Directory.Exists(PrimaryCase));
        Assert.False(Directory.Exists(BackupCase));
        Assert.True(Directory.Exists(sibling));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(89)]
    public async Task Purge_denies_nonexpired_and_future_final_manifest_utc(int days)
    {
        var receipt = await Deliver();
        _utc = _utc.AddDays(days);
        foreach (var directory in new[] { PrimaryCase, BackupCase })
            File.SetLastWriteTimeUtc(Path.Combine(directory, "manifest.json"), DateTime.UtcNow.AddYears(-5));

        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt));
        AssertCopiesIntact(receipt);
    }

    [Theory]
    [InlineData("manifest-missing")]
    [InlineData("manifest-changed")]
    [InlineData("manifest-replaced")]
    [InlineData("event-changed")]
    [InlineData("case-changed")]
    [InlineData("unexpected-entry")]
    [InlineData("unexpected-file-stream")]
    [InlineData("unexpected-directory-stream")]
    public async Task Purge_retains_primary_when_backup_no_longer_matches(string change)
    {
        var receipt = await Deliver();
        _utc = _utc.AddDays(100);
        var backupManifest = Path.Combine(BackupCase, "manifest.json");
        switch (change)
        {
            case "manifest-missing":
                File.Delete(backupManifest);
                break;
            case "manifest-changed":
                File.AppendAllText(backupManifest, " ");
                break;
            case "manifest-replaced":
                var originalManifest = Path.Combine(BackupRoot, "retained-manifest.json");
                var acl = File.GetAccessControl(backupManifest);
                File.Move(backupManifest, originalManifest);
                File.Copy(originalManifest, backupManifest);
                File.SetAccessControl(backupManifest, acl);
                Assert.Equal(receipt.BackupManifestHash, Sha(File.ReadAllBytes(backupManifest)));
                break;
            case "event-changed":
                File.AppendAllText(Path.Combine(BackupCase, "01-attempt.json"), " ");
                break;
            case "case-changed":
                var moved = Path.Combine(BackupRoot, "retained");
                Directory.Move(BackupCase, moved);
                CreatePrivateDirectory(BackupCase);
                foreach (var file in Directory.GetFiles(moved))
                    File.Move(file, Path.Combine(BackupCase, Path.GetFileName(file)));
                break;
            case "unexpected-entry":
                File.WriteAllText(Path.Combine(BackupCase, "foreign.txt"), "retain");
                break;
            case "unexpected-file-stream":
                WriteNamedStream(backupManifest);
                break;
            case "unexpected-directory-stream":
                WriteNamedStream(BackupCase);
                break;
        }
        var filesBefore = Directory.GetFiles(BackupCase).ToDictionary(file => file, file => File.ReadAllBytes(file));

        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt));
        Assert.Equal(receipt.PrimaryManifestHash, Sha(File.ReadAllBytes(Path.Combine(PrimaryCase, "manifest.json"))));
        Assert.Equal(filesBefore.Count, Directory.GetFiles(BackupCase).Length);
        foreach (var item in filesBefore)
            Assert.Equal(item.Value, File.ReadAllBytes(item.Key));
    }

    [Fact]
    public async Task Purge_denies_prefix_traversal_wrong_case_and_reciprocal_hash_order()
    {
        var receipt = await Deliver();
        _utc = _utc.AddDays(100);
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, primary: PrimaryRoot));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, primary: PrimaryCase + "-suffix"));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, primary: Path.Combine(PrimaryRoot, "dummy", "..", CaseId)));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, backup: Path.Combine(BackupCase, "..", CaseId)));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, caseId: "../" + CaseId));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt, caseId: "case-exact"));
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt,
            primaryHash: receipt.BackupManifestHash, backupHash: receipt.PrimaryManifestHash));
        AssertCopiesIntact(receipt);
    }

    [Fact]
    public async Task Partial_copy_remains_retained_without_a_delivered_pair()
    {
        var store = Store();
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        _utc = _utc.AddSeconds(-1);
        var receipt = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);

        Assert.Equal(MaintenanceEvidenceDelivery.Escalate, receipt.Delivery);
        Assert.Null(receipt.PrimaryManifestHash);
        Assert.Null(receipt.BackupManifestHash);
        await Assert.ThrowsAsync<IOException>(() => store.FinalizeAndBackupAsync(CaseId, CancellationToken.None));
        _utc = _utc.AddDays(100);
        Assert.Equal(MaintenanceEvidencePurge.Denied, await Purge(receipt,
            primaryHash: new string('0', 64), backupHash: new string('0', 64)));
        var primary = Assert.Single(Directory.GetFiles(PrimaryCase));
        var backup = Assert.Single(Directory.GetFiles(BackupCase));
        Assert.Equal(File.ReadAllBytes(primary), File.ReadAllBytes(backup));
    }

    [Fact]
    public async Task Finalize_holds_verified_source_bytes_until_both_manifests_are_durable()
    {
        var attemptPath = Path.Combine(PrimaryCase, "01-attempt.json");
        var shouldContend = false;
        var store = new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(PrimaryRoot, BackupRoot, Retention), () =>
        {
            if (shouldContend)
                Assert.Throws<IOException>(() => File.AppendAllText(attemptPath, "late change"));
            return _utc;
        });
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        shouldContend = true;

        var receipt = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);

        Assert.Equal(MaintenanceEvidenceDelivery.Delivered, receipt.Delivery);
        Assert.Equal(receipt.EventHash, Sha(File.ReadAllBytes(attemptPath)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Purge_escalates_failed_deletion_and_does_not_continue_to_the_next_copy(bool failBackup)
    {
        var receipt = await Deliver();
        _utc = _utc.AddDays(100);
        var protectedFile = Path.Combine(failBackup ? BackupCase : PrimaryCase, "manifest.json");
        File.SetAttributes(protectedFile, File.GetAttributes(protectedFile) | FileAttributes.ReadOnly);
        try
        {
            var expected = failBackup ? MaintenanceEvidencePurge.BackupUncertain : MaintenanceEvidencePurge.PrimaryUncertain;
            Assert.Equal(expected, await Purge(receipt));
            Assert.True(File.Exists(Path.Combine(PrimaryCase, "manifest.json")));
            Assert.Equal(failBackup, Directory.Exists(BackupCase));
            if (failBackup)
                Assert.True(File.Exists(Path.Combine(PrimaryCase, "01-attempt.json")));
        }
        finally
        {
            if (File.Exists(protectedFile))
                File.SetAttributes(protectedFile, File.GetAttributes(protectedFile) & ~FileAttributes.ReadOnly);
        }
    }

    private async Task<MaintenanceEvidenceReceipt> Deliver()
    {
        var store = Store();
        await store.WriteAttemptAsync(Attempt(), CancellationToken.None);
        _utc = _utc.AddDays(1);
        var receipt = await store.FinalizeAndBackupAsync(CaseId, CancellationToken.None);
        Assert.Equal(MaintenanceEvidenceDelivery.Delivered, receipt.Delivery);
        return receipt;
    }

    private Task<MaintenanceEvidencePurge> Purge(MaintenanceEvidenceReceipt receipt, string? primary = null,
        string? backup = null, string? caseId = null, string? primaryHash = null, string? backupHash = null) =>
        Store().PurgeExpiredAsync(primary ?? PrimaryCase, backup ?? BackupCase, caseId ?? CaseId,
            primaryHash ?? receipt.PrimaryManifestHash!, backupHash ?? receipt.BackupManifestHash!, CancellationToken.None);

    private void AssertCopiesIntact(MaintenanceEvidenceReceipt receipt)
    {
        Assert.Equal(receipt.PrimaryManifestHash, Sha(File.ReadAllBytes(Path.Combine(PrimaryCase, "manifest.json"))));
        Assert.Equal(receipt.BackupManifestHash, Sha(File.ReadAllBytes(Path.Combine(BackupCase, "manifest.json"))));
        Assert.True(File.Exists(Path.Combine(PrimaryCase, "01-attempt.json")));
        Assert.True(File.Exists(Path.Combine(BackupCase, "01-attempt.json")));
    }

    private MaintenanceEvidenceStore Store() =>
        new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(PrimaryRoot, BackupRoot, Retention), () => _utc);

    private static MaintenanceEvidenceRecord Attempt() => Record(MaintenanceEvidencePhase.Attempt,
        MaintenanceTransactionCertainty.NotStarted, MaintenanceReadback.NotRun);

    private static MaintenanceEvidenceRecord Transaction() => Record(MaintenanceEvidencePhase.Transaction,
        MaintenanceTransactionCertainty.Committed, MaintenanceReadback.NotRun);

    private static MaintenanceEvidenceRecord Readback() => Record(MaintenanceEvidencePhase.Readback,
        MaintenanceTransactionCertainty.Committed, MaintenanceReadback.Verified);

    private static MaintenanceEvidenceRecord Record(MaintenanceEvidencePhase phase, MaintenanceTransactionCertainty certainty,
        MaintenanceReadback readback, bool execute = true)
    {
        var args = new[]
        {
            execute ? CompactOptions.ExecuteCommand : CompactOptions.PreviewCommand,
            "--environment", EnvironmentName, "--database", Database,
            "--current-epoch", "11111111-1111-1111-1111-111111111111",
            "--stored-epoch", "22222222-2222-2222-2222-222222222222", "--actor", "7",
            "--operation", Operation, "--fingerprint", Fingerprint, "--correlation", Correlation, "--case", CaseId
        };
        if (execute)
        {
            var confirmation = $"compact-execute|environment={EnvironmentName}|database={Database}" +
                "|current-epoch=11111111-1111-1111-1111-111111111111|stored-epoch=22222222-2222-2222-2222-222222222222|actor=7" +
                $"|operation={Operation}|fingerprint={Fingerprint}|correlation={Correlation}|case={CaseId}|timeout-seconds=15";
            args = args.Concat(new[] { "--confirm", confirmation }).ToArray();
        }
        Assert.True(CompactOptions.TryParse(args, out var options, out _));
        return MaintenanceEvidenceRecord.FromCompaction(options!, Login, phase, certainty, readback);
    }

    private static MaintenanceEvidenceRecord Reconcile(MaintenanceEvidencePhase phase, MaintenanceReadback readback, string correlation,
        string caseId = CaseId) =>
        MaintenanceEvidenceRecord.FromReconciliation(EnvironmentName, Database, Login, 7, Guid.Parse(Operation), Guid.Parse(correlation),
            Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Enumerable.Repeat((byte)0xaa, 32).ToArray(), caseId, phase, readback);

    private static void AssertSanitized(byte[] bytes)
    {
        var json = new UTF8Encoding(false, true).GetString(bytes);
        Assert.StartsWith("{\"version\":1,", json);
        foreach (var raw in new[] { EnvironmentName, Database, Login, Operation, Correlation, Fingerprint })
            Assert.DoesNotContain(raw, json);
    }

    private static string Sha(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static void CreatePrivateDirectory(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var acl = new DirectorySecurity();
        acl.SetOwner(identity.User!);
        acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl, AccessControlType.Allow));
        Directory.CreateDirectory(path, acl);
    }

    private static void CreateJunction(string link, string destination)
    {
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{destination}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        if (!process.WaitForExit(5000))
        {
            process.Kill();
            Assert.True(process.WaitForExit(5000));
            throw new TimeoutException("Junction fixture timed out.");
        }
        Assert.Equal(0, process.ExitCode);
        Assert.True((File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0);
    }

    private static void WriteNamedStream(string entry)
    {
        const uint genericWrite = 0x40000000;
        const uint createNew = 1;
        const uint normalAttributes = 0x80;
        using var handle = CreateFile(entry + ":outside-manifest:$DATA", genericWrite, 0, IntPtr.Zero, createNew,
            normalAttributes, IntPtr.Zero);
        Assert.False(handle.IsInvalid);
        using var stream = new FileStream(handle, FileAccess.Write);
        stream.WriteByte(1);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr securityAttributes,
        uint disposition, uint attributes, IntPtr template);

    public void Dispose()
    {
        // Only this fixture's freshly generated directory is owned, and junctions are removed in each test.
        var expected = Path.Combine(Path.GetFullPath(Path.GetTempPath()), Path.GetFileName(_workspace));
        Assert.Equal(expected, Path.GetFullPath(_workspace));
        Assert.Equal((FileAttributes)0, File.GetAttributes(_workspace) & FileAttributes.ReparsePoint);
        Directory.Delete(_workspace, true);
        Assert.False(Directory.Exists(_workspace));
    }
}
