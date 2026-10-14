using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Ownership;
using MyDmsVn.SqlTestHarness.Preflight;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlTestDatabaseMutationTests
{
    private const string MasterConnectionString =
        "Server=sql-alias;Database=master;Integrated Security=true;Encrypt=false";

    [Fact]
    public async Task Direct_create_with_missing_approval_issues_no_destructive_sql()
    {
        await WithJournalAsync(async (directory, journal) =>
        {
            var session = new RecordingSession();
            var missingApproval = Path.Combine(directory, "missing-approval.json");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SqlTestDatabase.CreateAsync(
                    MasterConnectionString,
                    missingApproval,
                    journal,
                    _ => session,
                    CancellationToken.None));

            Assert.Empty(session.Commands);
            Assert.Empty(await journal.ReadAllAsync());
        });
    }

    [Fact]
    public async Task Direct_create_with_expired_approval_issues_no_destructive_sql()
    {
        await WithJournalAsync(async (directory, journal) =>
        {
            var approvalPath = Path.Combine(directory, "approval.json");
            File.WriteAllText(approvalPath, ExpiredApprovalJson());
            var session = new RecordingSession();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SqlTestDatabase.CreateAsync(
                    MasterConnectionString,
                    approvalPath,
                    journal,
                    _ => session,
                    CancellationToken.None));

            Assert.Empty(session.Commands);
            Assert.Empty(await journal.ReadAllAsync());
        });
    }

    [Fact]
    public async Task Direct_create_with_live_identity_mismatch_issues_no_destructive_sql()
    {
        await WithJournalAsync(async (directory, journal) =>
        {
            var approvalPath = Path.Combine(directory, "approval.json");
            File.WriteAllText(approvalPath, ValidApprovalJson());
            var session = new RecordingSession
            {
                Identity = new SqlInstanceIdentity(
                    "production-sql",
                    "PROD",
                    null,
                    "tcp:10.0.0.200,1433"),
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => SqlTestDatabase.CreateAsync(
                    MasterConnectionString,
                    approvalPath,
                    journal,
                    _ => session,
                    CancellationToken.None));

            Assert.DoesNotContain(
                session.Commands,
                command => command.IndexOf("CREATE DATABASE", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Empty(await journal.ReadAllAsync());
        });
    }

    [Fact]
    public async Task Disposal_on_a_changed_server_issues_no_cleanup_ddl()
    {
        await WithJournalAsync(async (directory, journal) =>
        {
            var approvalPath = Path.Combine(directory, "approval.json");
            File.WriteAllText(approvalPath, ValidApprovalJson());
            var session = new RecordingSession();
            var database = await SqlTestDatabase.CreateAsync(
                MasterConnectionString,
                approvalPath,
                journal,
                _ => session,
                CancellationToken.None);
            session.Identity = new SqlInstanceIdentity(
                "production-sql",
                "PROD",
                null,
                "tcp:10.0.0.200,1433");

            await Assert.ThrowsAsync<InvalidOperationException>(() => database.DisposeAsync());

            Assert.DoesNotContain(
                session.Commands,
                command =>
                    command.IndexOf("ALTER DATABASE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    command.IndexOf("DROP DATABASE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    command.IndexOf("KILL", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.Equal(
                DatabaseOwnershipState.Created,
                Assert.Single(await journal.ReadAllAsync()).State);
        });
    }

    [Fact]
    public async Task Database_helper_forwards_the_public_test_identity_to_measurement_context()
    {
        await WithJournalAsync(async (directory, journal) =>
        {
            var approvalPath = Path.Combine(directory, "approval.json");
            File.WriteAllText(approvalPath, ValidApprovalJson());
            var database = await CreateThroughHelperAsync(
                approvalPath,
                journal,
                new RecordingSession());
            try
            {
                Assert.Equal(
                    nameof(Database_helper_forwards_the_public_test_identity_to_measurement_context),
                    database.MeasurementContext.TestCaseId);
            }
            finally
            {
                await database.DisposeAsync();
            }
        });
    }

    private static Task<SqlTestDatabase> CreateThroughHelperAsync(
        string approvalPath,
        IDatabaseOwnershipJournal journal,
        ISqlAdministrativeSession session,
        [CallerMemberName] string testCaseId = "") =>
        SqlTestDatabase.CreateAsync(
            MasterConnectionString,
            approvalPath,
            journal,
            _ => session,
            CancellationToken.None,
            testCaseId);

    private static async Task WithJournalAsync(
        Func<string, FileDatabaseOwnershipJournal, Task> assertion)
    {
        var directory = Path.Combine(Path.GetTempPath(), "MyDmsVn.SqlMutationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await assertion(directory, new FileDatabaseOwnershipJournal(Path.Combine(directory, "journal")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string ExpiredApprovalJson() =>
        ValidApprovalJson().Replace("2099-01-01T00:00:00Z", "2000-01-01T00:00:00Z");

    private static string ValidApprovalJson() =>
        "{" +
        "\"schemaVersion\":1," +
        "\"approvalId\":\"expired\"," +
        "\"nonProductionAttestation\":true," +
        "\"validUntilUtc\":\"2099-01-01T00:00:00Z\"," +
        "\"identity\":{" +
        "\"serverName\":\"vsql01\"," +
        "\"machineName\":\"TESTBOX\"," +
        "\"instanceName\":\"SQLEXPRESS\"," +
        "\"endpoint\":\"tcp:10.0.0.5,1433\"}," +
        "\"fingerprintSha256\":\"3e68cd38f51b859ba18373c96bf81945b44cbc94ebe8e83435dcc1720892f7e7\"}";

    private sealed class RecordingSession : ISqlAdministrativeSession
    {
        public RecordingSession()
        {
            Identity = new SqlInstanceIdentity(
                "vsql01",
                "TESTBOX",
                "SQLEXPRESS",
                "tcp:10.0.0.5,1433");
        }

        public List<string> Commands { get; } = new List<string>();

        public SqlInstanceIdentity Identity { get; set; }

        public DatabaseOwnershipMarker? Marker { get; private set; }

        public Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string?> ReadEffectiveDatabaseAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>("master");

        public Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken) =>
            Task.FromResult<SqlInstanceIdentity?>(Identity);

        public Task<int?> ReadMajorVersionAsync(CancellationToken cancellationToken) =>
            Task.FromResult<int?>(16);

        public Task<bool?> HasServerPermissionAsync(string permissionName, CancellationToken cancellationToken) =>
            Task.FromResult<bool?>(true);

        public Task<LiveDatabaseOwnership?> ReadDatabaseOwnershipAsync(
            string databaseName,
            CancellationToken cancellationToken) =>
            Task.FromResult<LiveDatabaseOwnership?>(
                Marker is null
                    ? null
                    : new LiveDatabaseOwnership(
                        42,
                        new DateTimeOffset(2026, 10, 14, 1, 2, 0, TimeSpan.Zero),
                        Marker));

        public Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, object?> parameters,
            CancellationToken cancellationToken)
        {
            Commands.Add(sql);
            if (parameters.TryGetValue("@markerValue", out var markerValue))
            {
                Marker = DatabaseOwnershipMarker.TryParse(Convert.ToString(markerValue));
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
