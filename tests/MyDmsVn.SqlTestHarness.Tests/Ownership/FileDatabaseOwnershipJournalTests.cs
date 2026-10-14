using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Ownership;
using Xunit;

namespace MyDmsVn.SqlTestHarness.Tests.Ownership;

public sealed class FileDatabaseOwnershipJournalTests
{
    [Fact]
    public async Task Create_intent_is_durable_and_readable_after_reopening()
    {
        await WithDirectoryAsync(async directory =>
        {
            var record = CreateIntent();
            await new FileDatabaseOwnershipJournal(directory).WriteCreateIntentAsync(record);

            var actual = Assert.Single(
                await new FileDatabaseOwnershipJournal(directory).ReadAllAsync());

            AssertEquivalent(record, actual);
        });
    }

    [Fact]
    public async Task Orphan_temporary_write_is_detected_instead_of_accepted()
    {
        await WithDirectoryAsync(async directory =>
        {
            File.WriteAllText(Path.Combine(directory, "interrupted.tmp"), "partial");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => new FileDatabaseOwnershipJournal(directory).ReadAllAsync());

            Assert.Contains("incomplete", exception.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Replaying_the_same_create_intent_is_idempotent()
    {
        await WithDirectoryAsync(async directory =>
        {
            var journal = new FileDatabaseOwnershipJournal(directory);
            var record = CreateIntent();

            await journal.WriteCreateIntentAsync(record);
            await journal.WriteCreateIntentAsync(record);

            Assert.Single(await journal.ReadAllAsync());
        });
    }

    [Fact]
    public async Task Invalid_transition_is_rejected_without_changing_the_record()
    {
        await WithDirectoryAsync(async directory =>
        {
            var journal = new FileDatabaseOwnershipJournal(directory);
            var record = CreateIntent();
            await journal.WriteCreateIntentAsync(record);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => journal.TransitionAsync(record, DatabaseOwnershipState.Disposed));

            Assert.Equal(
                DatabaseOwnershipState.CreateIntent,
                Assert.Single(await journal.ReadAllAsync()).State);
        });
    }

    [Fact]
    public async Task Concurrent_writers_produce_complete_independent_records()
    {
        await WithDirectoryAsync(async directory =>
        {
            var journal = new FileDatabaseOwnershipJournal(directory);
            var writes = Enumerable.Range(0, 8)
                .Select(index => Task.Run(() => journal.WriteCreateIntentAsync(CreateIntent(index))))
                .ToArray();

            await Task.WhenAll(writes);

            var records = await journal.ReadAllAsync();
            Assert.Equal(8, records.Count);
            Assert.Equal(8, records.Select(record => record.DatabaseName).Distinct().Count());
        });
    }

    [Fact]
    public async Task Durable_record_contains_no_connection_string_raw_host_or_credential()
    {
        await WithDirectoryAsync(async directory =>
        {
            await new FileDatabaseOwnershipJournal(directory).WriteCreateIntentAsync(CreateIntent());

            var json = File.ReadAllText(Assert.Single(Directory.GetFiles(directory, "*.json")));

            Assert.DoesNotContain("Server=", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vsql01", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TopSecret", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("instance-fingerprint", json, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Failed_durable_flush_reports_failure_and_leaves_no_authorizing_record()
    {
        await WithDirectoryAsync(async directory =>
        {
            var journal = new FileDatabaseOwnershipJournal(
                directory,
                _ => throw new IOException("expected flush failure"));

            await Assert.ThrowsAsync<IOException>(
                () => journal.WriteCreateIntentAsync(CreateIntent()));

            Assert.Empty(Directory.GetFiles(directory, "*.json"));
        });
    }

    [Fact]
    public async Task Transient_replace_failure_is_retried_without_losing_the_record()
    {
        await WithDirectoryAsync(async directory =>
        {
            var replaceAttempts = 0;
            var journal = new FileDatabaseOwnershipJournal(
                directory,
                stream => stream.Flush(flushToDisk: true),
                (source, destination, backup) =>
                {
                    replaceAttempts++;
                    if (replaceAttempts < 6)
                    {
                        throw new ReplaceFileIOException();
                    }

                    File.Replace(source, destination, backup);
                },
                _ => { });
            var record = CreateIntent();
            await journal.WriteCreateIntentAsync(record);

            var updated = await journal.TransitionAsync(
                record,
                DatabaseOwnershipState.Created,
                databaseId: 7,
                databaseCreateDateUtc: DateTimeOffset.UtcNow);

            Assert.Equal(6, replaceAttempts);
            Assert.Equal(DatabaseOwnershipState.Created, updated.State);
            Assert.Equal(
                DatabaseOwnershipState.Created,
                Assert.Single(await journal.ReadAllAsync()).State);
        });
    }

    private static DatabaseOwnershipRecord CreateIntent(int index = 0) =>
        new DatabaseOwnershipRecord(
            runId: "run-001",
            operationId: "operation-" + index,
            databaseName: "MyDmsVn_Test_" + index.ToString("x32"),
            ownershipToken: "ownership-" + index,
            instanceFingerprintSha256: "instance-fingerprint",
            approvalId: "approval-001",
            state: DatabaseOwnershipState.CreateIntent,
            updatedUtc: new DateTimeOffset(2026, 10, 14, 1, 2, 3, TimeSpan.Zero),
            databaseId: null,
            databaseCreateDateUtc: null);

    private static void AssertEquivalent(DatabaseOwnershipRecord expected, DatabaseOwnershipRecord actual)
    {
        Assert.Equal(expected.RunId, actual.RunId);
        Assert.Equal(expected.OperationId, actual.OperationId);
        Assert.Equal(expected.DatabaseName, actual.DatabaseName);
        Assert.Equal(expected.OwnershipToken, actual.OwnershipToken);
        Assert.Equal(expected.InstanceFingerprintSha256, actual.InstanceFingerprintSha256);
        Assert.Equal(expected.ApprovalId, actual.ApprovalId);
        Assert.Equal(expected.State, actual.State);
        Assert.Equal(expected.UpdatedUtc, actual.UpdatedUtc);
    }

    private static async Task WithDirectoryAsync(Func<string, Task> assertion)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "MyDmsVn.SqlTestHarness.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await assertion(directory);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class ReplaceFileIOException : IOException
    {
        public ReplaceFileIOException()
            : base("Unable to remove the file to be replaced.")
        {
            HResult = unchecked((int)0x80070497);
        }
    }
}
