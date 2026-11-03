using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web5OriginalRaceForensicsTests
{
    [HistoricalForensicsFact, Trait("Category", "Web5HistoricalForensics")]
    public async Task Historical_private_manifests_are_observed_read_only_and_remain_blocked_without_authority()
    {
        var parent = Environment.GetEnvironmentVariable("MYDMSVN_WEB5_RACE_PARENT_DIRECTORY")!;
        using var user = WindowsIdentity.GetCurrent();
        Web5AttendedLease.VerifyPrivateDirectory(parent, user.User!);
        var entries = Directory.GetFileSystemEntries(parent);
        Assert.NotEmpty(entries);
        Assert.All(entries, entry =>
        {
            Assert.True(Directory.Exists(entry));
            Assert.Matches(new Regex("\\Arace-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant), Path.GetFileName(entry));
        });
        foreach (var entry in entries)
        {
            var manifest = Web5OriginalRaceForensics.ReadManifest(entry);
            var observation = await Web5OriginalRaceForensics.ObserveAsync(
                Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!,
                manifest, TimeSpan.FromMilliseconds(500), CancellationToken.None);
            var review = Web5OriginalRaceForensics.Review(manifest, observation);
            Assert.False(review.CleanupAuthorityProven);
            Assert.NotEmpty(review.Blockers);
        }
        Console.WriteLine("WEB5_HISTORICAL_FORENSICS_READ_ONLY count=" + entries.Length);
    }

    [Fact]
    public void Exact_private_manifest_is_parsed_but_never_grants_cleanup_authority()
    {
        using var fixture = new ManifestFixture();
        var first = fixture.Add();
        var second = fixture.Add();

        var manifest = Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath);

        Assert.Equal(fixture.Database, manifest.DatabaseName);
        Assert.Equal(new[] { first, second }.OrderBy(value => value), manifest.Records.Select(record => record.OperationId).OrderBy(value => value));
        Assert.False(manifest.ContainsOwnershipToken);
        Assert.False(manifest.ContainsDatabaseIdentity);
        Assert.False(manifest.CleanupAuthorityProven);
    }

    [Fact]
    public void Filename_operation_mismatch_is_rejected()
    {
        using var fixture = new ManifestFixture();
        var operation = fixture.Add();
        File.Move(Path.Combine(fixture.DirectoryPath, operation.ToString("N") + ".txt"),
            Path.Combine(fixture.DirectoryPath, Guid.NewGuid().ToString("N") + ".txt"));
        Assert.Throws<InvalidDataException>(() => Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath));
    }

    [Fact]
    public void Mixed_database_is_rejected()
    {
        using var fixture = new ManifestFixture();
        fixture.Add();
        fixture.Add(database: SqlTestDatabaseName.Create());
        Assert.Throws<InvalidDataException>(() => Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath));
    }

    [Fact]
    public void Duplicate_trigger_is_rejected()
    {
        using var fixture = new ManifestFixture();
        var trigger = "Web5Race_" + new string('a', 32);
        fixture.Add(trigger: trigger);
        fixture.Add(trigger: trigger);
        Assert.Throws<InvalidDataException>(() => Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath));
    }

    [Fact]
    public void Unexpected_directory_or_extra_manifest_field_is_rejected()
    {
        using (var fixture = new ManifestFixture())
        {
            fixture.Add();
            Directory.CreateDirectory(Path.Combine(fixture.DirectoryPath, "unexpected"));
            Assert.Throws<InvalidDataException>(() => Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath));
        }
        using (var fixture = new ManifestFixture())
        {
            var operation = fixture.Add();
            File.AppendAllText(Path.Combine(fixture.DirectoryPath, operation.ToString("N") + ".txt"), "token=not-authority\n");
            Assert.Throws<InvalidDataException>(() => Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath));
        }
    }

    [Fact]
    public void Stable_zero_snapshots_and_marker_presence_still_block_without_original_token_and_creation_tuple()
    {
        using var fixture = new ManifestFixture();
        fixture.Add();
        var manifest = Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath);
        var observation = new Web5OriginalRaceSqlObservation(
            fixture.Database, "nonproduction", 17, new DateTime(2026, 11, 3, 1, 2, 3, DateTimeKind.Utc),
            ownershipMarkerPresent: true,
            new Web5OriginalRaceActivitySnapshot(0, 0), new Web5OriginalRaceActivitySnapshot(0, 0));

        var review = Web5OriginalRaceForensics.Review(manifest, observation);

        Assert.True(review.StableQuiescenceObserved);
        Assert.False(review.CleanupAuthorityProven);
        Assert.Contains(review.Blockers, blocker => blocker.IndexOf("token", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.Contains(review.Blockers, blocker => blocker.IndexOf("creation", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Active_or_changing_session_lock_snapshots_are_not_stable_quiescence()
    {
        using var fixture = new ManifestFixture();
        fixture.Add();
        var manifest = Web5OriginalRaceForensics.ReadManifest(fixture.DirectoryPath);
        var observation = new Web5OriginalRaceSqlObservation(
            fixture.Database, "nonproduction", 17, DateTime.UtcNow, true,
            new Web5OriginalRaceActivitySnapshot(1, 0), new Web5OriginalRaceActivitySnapshot(0, 0));

        var review = Web5OriginalRaceForensics.Review(manifest, observation);

        Assert.False(review.StableQuiescenceObserved);
        Assert.False(review.CleanupAuthorityProven);
    }

    private sealed class ManifestFixture : IDisposable
    {
        private readonly string _parent;
        internal ManifestFixture()
        {
            using var user = WindowsIdentity.GetCurrent();
            _parent = Path.Combine(Path.GetTempPath(), "web5-forensic-parent-" + Guid.NewGuid().ToString("N"));
            var acl = new DirectorySecurity();
            acl.SetAccessRuleProtection(true, false);
            acl.SetOwner(user.User!);
            acl.AddAccessRule(new FileSystemAccessRule(user.User!, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(_parent, acl);
            DirectoryPath = Path.Combine(_parent, "race-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath, acl);
        }
        internal string DirectoryPath { get; }
        internal string Database { get; } = SqlTestDatabaseName.Create();
        internal Guid Add(string? database = null, string? trigger = null)
        {
            var operation = Guid.NewGuid();
            var value = "database=" + (database ?? Database) + "\noperation=" + operation.ToString("D") +
                "\ntrigger=" + (trigger ?? "Web5Race_" + Guid.NewGuid().ToString("N")) + "\n";
            File.WriteAllText(Path.Combine(DirectoryPath, operation.ToString("N") + ".txt"), value);
            return operation;
        }
        public void Dispose() => Directory.Delete(_parent, true);
    }

    private sealed class HistoricalForensicsFactAttribute : FactAttribute
    {
        public HistoricalForensicsFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("MYDMSVN_WEB5_HISTORICAL_FORENSICS_ENABLE") != "1" ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MYDMSVN_WEB5_RACE_PARENT_DIRECTORY")) ||
                string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)))
                Skip = "Historical forensics requires explicit read-only opt-in, exact private parent and approved nonproduction master.";
        }
    }
}
