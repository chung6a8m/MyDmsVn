using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6MaintenanceRecoveryTests
{
    [Theory]
    [InlineData("master")]
    [InlineData("tempdb")]
    [InlineData("MyDmsVn_Test_not-owned")]
    public void Recovery_refuses_system_or_unowned_names(string name) =>
        Assert.Throws<InvalidOperationException>(() => Web6MaintenanceRecoveryFixture.EnsureDistinctNames(SqlTestDatabaseName.Create(), name));

    [Fact]
    public void Recovery_refuses_source_equal_clone()
    {
        var name = SqlTestDatabaseName.Create();
        Assert.Throws<InvalidOperationException>(() => Web6MaintenanceRecoveryFixture.EnsureDistinctNames(name, name));
    }

    [Fact]
    public void Recovery_refuses_existing_backup_data_and_log_paths()
    {
        var path = Path.GetTempFileName();
        try
        {
            foreach (var role in new[] { "backup", "data", "log" })
                Assert.Throws<InvalidOperationException>(() => Web6MaintenanceRecoveryFixture.EnsureNewPath(role, path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Admission_requires_oracle_single_rotation_and_all_principals()
    {
        var gate = new Web6MaintenanceRecoveryFixture.Admission("exact-owned-manifest");
        Assert.Throws<InvalidOperationException>(gate.Admit);
        Assert.Throws<InvalidOperationException>(() => gate.Rotated(1));
        gate.OracleVerified();
        Assert.Throws<InvalidOperationException>(gate.Admit);
        gate.Rotated(1);
        Assert.Throws<InvalidOperationException>(() => gate.Rotated(1));
        Assert.Throws<InvalidOperationException>(gate.Admit);
        gate.PrincipalsVerified();
        gate.Admit();
        Assert.True(gate.WriterAdmission);
    }

    [Theory]
    [InlineData("creation-identity")]
    [InlineData("reparse-backup")]
    [InlineData("reparse-data")]
    [InlineData("reparse-log")]
    [InlineData("process-drain-timeout")]
    [InlineData("session-drain-timeout")]
    [InlineData("stale-writer")]
    [InlineData("cleanup-identity")]
    public void Uncertain_control_state_quarantines_exact_manifest_without_cleanup(string reason)
    {
        var gate = new Web6MaintenanceRecoveryFixture.Admission("source+clone+backup+data+log+creation-token");
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (reason == "creation-identity") gate.CheckOwnership(false, FileAttributes.Normal);
            else if (reason.StartsWith("reparse-", StringComparison.Ordinal))
            {
                var paths = new[] { FileAttributes.Normal, FileAttributes.Normal, FileAttributes.Normal };
                paths[reason == "reparse-backup" ? 0 : reason == "reparse-data" ? 1 : 2] = FileAttributes.ReparsePoint;
                gate.CheckOwnership(true, paths);
            }
            else if (reason == "process-drain-timeout") gate.CheckDrain(false, 0);
            else if (reason == "session-drain-timeout") gate.CheckDrain(true, 1);
            else if (reason == "stale-writer") gate.RequireWriter();
            else { gate.Drained(); gate.Cleanup("sibling-or-historical-manifest", () => throw new Xunit.Sdk.XunitException("Cleanup reached.")); }
        });
        Assert.False(gate.WriterAdmission);
        Assert.Equal("source+clone+backup+data+log+creation-token", gate.QuarantineManifest);
        var cleanupCommands = 0;
        Assert.Throws<InvalidOperationException>(() => gate.Cleanup("source+clone+backup+data+log+creation-token", () => cleanupCommands++));
        Assert.Equal(0, cleanupCommands);
        Assert.Throws<InvalidOperationException>(gate.Admit);
    }

    [Fact]
    public void Cleanup_requires_exact_manifest_and_successful_drain()
    {
        var gate = new Web6MaintenanceRecoveryFixture.Admission("original");
        var commands = 0;
        Assert.Throws<InvalidOperationException>(() => gate.Cleanup("original", () => commands++));
        gate.Drained();
        Assert.Throws<InvalidOperationException>(() => gate.Cleanup("sibling", () => commands++));
        Assert.Equal(0, commands);
        Assert.Equal("original", gate.QuarantineManifest);
        Assert.Throws<InvalidOperationException>(() => gate.Cleanup("original", () => commands++));
        Assert.Equal(0, commands);
    }

    [Fact]
    public void Epoch_CAS_failure_keeps_admission_closed()
    {
        var gate = new Web6MaintenanceRecoveryFixture.Admission("original");
        gate.OracleVerified();
        Assert.Throws<InvalidOperationException>(() => gate.Rotated(0));
        Assert.False(gate.WriterAdmission);
    }

    [Theory]
    [InlineData("restore")]
    [InlineData("dropDatabase")]
    [InlineData("executeSql")]
    public void Recovery_control_never_accepts_arbitrary_SQL_or_database_actions(string action) =>
        Assert.Throws<InvalidDataException>(() => WebRecoveryPilotControl.Validate(new JObject
        { ["id"] = 1, ["token"] = "token", ["action"] = action, ["database"] = "master", ["sql"] = "DROP DATABASE master" }, "token"));

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Copy_only_backup_restores_B03_source_to_distinct_owned_clone_and_rotates_epoch_before_admission()
    {
        var fixture = await Web6MaintenanceRecoveryFixture.CreateAsync();
        try
        {
            var result = await fixture.RunAsync(Web6MaintenanceRecoveryFixture.Failure.None);
            Assert.True(result.Succeeded, result.Phase);
            Assert.False(result.Quarantined);
            Assert.True(result.PhysicalOracleMatched);
            Assert.True(result.CopyOnlyChecksummed);
            Assert.Equal(1, result.EpochRotations);
            Assert.Equal(3, result.StaleDenials);
            Assert.Equal(1, result.CurrentOperations);
            Assert.True(result.ResourcesAbsent);
        }
        finally { await fixture.TestCleanupOnlyAsync(); }
    }

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Restore_failure_keeps_admission_closed_and_retains_exact_owned_quarantine()
    {
        foreach (var failure in new[] { Web6MaintenanceRecoveryFixture.Failure.Checksum, Web6MaintenanceRecoveryFixture.Failure.Restore,
            Web6MaintenanceRecoveryFixture.Failure.PrincipalProvisioning, Web6MaintenanceRecoveryFixture.Failure.EpochCas })
        {
            var fixture = await Web6MaintenanceRecoveryFixture.CreateAsync();
            try
            {
                var result = await fixture.RunAsync(failure);
                Assert.False(result.Succeeded);
                Assert.True(result.Quarantined);
                Assert.False(result.WriterAdmission);
                Assert.False(result.ResourcesAbsent);
                Assert.Equal(fixture.ManifestPath, result.QuarantineManifest);
                Assert.True(File.Exists(result.QuarantineManifest));
                Assert.Equal(0, result.NormalCleanupCommands);
                Assert.Equal(failure, result.ObservedFailure);
                if (failure == Web6MaintenanceRecoveryFixture.Failure.Checksum) Assert.True(result.ChecksumRejected);
            }
            finally
            {
                await fixture.TestCleanupOnlyAsync();
                Assert.True(fixture.TestCleanupResourcesAbsent);
            }
        }
    }
}
