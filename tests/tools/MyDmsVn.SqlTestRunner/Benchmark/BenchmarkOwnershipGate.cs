using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Ownership;
using MyDmsVn.SqlTestHarness.Preflight;
using Newtonsoft.Json;

namespace MyDmsVn.SqlTestRunner.Benchmark;

public interface IBenchmarkOwnershipGate
{
    Task<BenchmarkOwnershipResult> ValidateAsync(
        BenchmarkOwnershipEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IBenchmarkLiveOwnershipReader : IDisposable
{
    Task OpenAsync(CancellationToken cancellationToken);

    Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken);

    Task<LiveDatabaseOwnership?> ReadAsync(
        string databaseName,
        CancellationToken cancellationToken);
}

public sealed record BenchmarkOwnershipEvidence(
    string ExecutionId,
    string JournalDirectory,
    string RecoveryArtifactPath,
    string ApprovalFile,
    bool ProcessSucceeded);

public sealed record BenchmarkOwnershipResult(
    bool IsComplete,
    string? RecoveryArtifactPath);

public sealed class BenchmarkOwnershipGate : IBenchmarkOwnershipGate
{
    private readonly Func<IBenchmarkLiveOwnershipReader> _readerFactory;

    public BenchmarkOwnershipGate()
        : this(CreateEnvironmentReader)
    {
    }

    public BenchmarkOwnershipGate(Func<IBenchmarkLiveOwnershipReader> readerFactory) =>
        _readerFactory = readerFactory ?? throw new ArgumentNullException(nameof(readerFactory));

    public async Task<BenchmarkOwnershipResult> ValidateAsync(
        BenchmarkOwnershipEvidence evidence,
        CancellationToken cancellationToken)
    {
        var issues = new List<RecoveryEntry>();
        try
        {
            var approval = SqlInstanceApprovalLoader.Load(evidence.ApprovalFile);
            var journal = new FileDatabaseOwnershipJournal(evidence.JournalDirectory);
            var allRecords = (await journal.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                .ToArray();
            var unexpectedRecords = allRecords
                .Where(record => !string.Equals(
                    record.RunId,
                    evidence.ExecutionId,
                    StringComparison.Ordinal))
                .ToArray();
            foreach (var record in unexpectedRecords)
            {
                issues.Add(RecoveryEntry.UnexpectedRunId(record.OperationId, record.State));
            }

            var records = allRecords
                .Where(record => string.Equals(
                    record.RunId,
                    evidence.ExecutionId,
                    StringComparison.Ordinal))
                .ToArray();
            if (allRecords.Length == 0)
            {
                issues.Add(RecoveryEntry.EmptyJournal());
            }
            else if (records.Length > 0)
            {
                using var reader = _readerFactory();
                await reader.OpenAsync(cancellationToken).ConfigureAwait(false);
                var identity = await reader.ReadIdentityAsync(cancellationToken).ConfigureAwait(false);
                if (identity is null ||
                    !string.Equals(
                        SqlInstanceFingerprint.Create(identity),
                        approval.FingerprintSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The live SQL instance does not match the approval.");
                }

                foreach (var record in records)
                {
                    var live = await reader.ReadAsync(record.DatabaseName, cancellationToken)
                        .ConfigureAwait(false);
                    var approvalMatches = string.Equals(
                                              record.ApprovalId,
                                              approval.ApprovalId,
                                              StringComparison.Ordinal) &&
                                          string.Equals(
                                              record.InstanceFingerprintSha256,
                                              approval.FingerprintSha256,
                                              StringComparison.Ordinal);
                    if (approvalMatches &&
                        record.State == DatabaseOwnershipState.Disposed &&
                        live is null)
                    {
                        continue;
                    }

                    var recovery = await DatabaseOwnershipReconciler.ClassifyAsync(
                            record,
                            live,
                            evidence.ExecutionId)
                        .ConfigureAwait(false);
                    issues.Add(new RecoveryEntry
                    {
                        OperationId = Anonymize(record.OperationId),
                        State = record.State.ToString(),
                        LiveDatabasePresent = live is not null,
                        Classification = approvalMatches
                            ? recovery.Classification.ToString()
                            : "ApprovalMismatch",
                        Reason = approvalMatches
                            ? recovery.Reason
                            : "The durable record does not match the approved instance.",
                    });
                }
            }
        }
        catch
        {
            issues.Clear();
            issues.Add(RecoveryEntry.InspectionFailed());
        }

        if (issues.Count == 0)
        {
            return new BenchmarkOwnershipResult(true, null);
        }

        WriteRecoveryArtifact(evidence, issues);
        return new BenchmarkOwnershipResult(false, evidence.RecoveryArtifactPath);
    }

    private static IBenchmarkLiveOwnershipReader CreateEnvironmentReader()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            SqlTestHarnessEnvironment.ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SQL test connection string is unavailable.");
        }

        return new SqlBenchmarkLiveOwnershipReader(connectionString);
    }

    private static string Anonymize(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteRecoveryArtifact(
        BenchmarkOwnershipEvidence evidence,
        IReadOnlyCollection<RecoveryEntry> issues)
    {
        var directory = Path.GetDirectoryName(evidence.RecoveryArtifactPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("A recovery artifact directory is required.");
        }

        Directory.CreateDirectory(directory);
        var document = new RecoveryDocument
        {
            RunId = evidence.ExecutionId,
            ProcessSucceeded = evidence.ProcessSucceeded,
            IsComplete = false,
            Entries = issues.ToArray(),
        };
        var temporaryPath = evidence.RecoveryArtifactPath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(
            temporaryPath,
            JsonConvert.SerializeObject(document, Formatting.Indented));
        File.Move(temporaryPath, evidence.RecoveryArtifactPath, true);
    }

    private sealed class RecoveryDocument
    {
        [JsonProperty("runId")]
        public string RunId { get; set; } = string.Empty;

        [JsonProperty("processSucceeded")]
        public bool ProcessSucceeded { get; set; }

        [JsonProperty("isComplete")]
        public bool IsComplete { get; set; }

        [JsonProperty("entries")]
        public RecoveryEntry[] Entries { get; set; } = Array.Empty<RecoveryEntry>();
    }

    private sealed class RecoveryEntry
    {
        [JsonProperty("operationId")]
        public string OperationId { get; set; } = string.Empty;

        [JsonProperty("state")]
        public string State { get; set; } = string.Empty;

        [JsonProperty("liveDatabasePresent")]
        public bool LiveDatabasePresent { get; set; }

        [JsonProperty("classification")]
        public string Classification { get; set; } = string.Empty;

        [JsonProperty("reason")]
        public string Reason { get; set; } = string.Empty;

        public static RecoveryEntry EmptyJournal() => new RecoveryEntry
        {
            State = "Missing",
            Classification = "JournalEmpty",
            Reason = "No ownership records were found for the execution.",
        };

        public static RecoveryEntry InspectionFailed() => new RecoveryEntry
        {
            State = "Unknown",
            Classification = "InspectionFailed",
            Reason = "Ownership inspection could not be completed.",
        };

        public static RecoveryEntry UnexpectedRunId(
            string operationId,
            DatabaseOwnershipState state) => new RecoveryEntry
        {
            OperationId = Anonymize(operationId),
            State = state.ToString(),
            Classification = "UnexpectedRunId",
            Reason = "The isolated ownership journal contains a record for another execution.",
        };
    }
}

internal sealed class SqlBenchmarkLiveOwnershipReader : IBenchmarkLiveOwnershipReader
{
    private readonly SqlClientAdministrativeSession _session;

    public SqlBenchmarkLiveOwnershipReader(string connectionString) =>
        _session = new SqlClientAdministrativeSession(connectionString);

    public Task OpenAsync(CancellationToken cancellationToken) =>
        _session.OpenAsync(cancellationToken);

    public Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken) =>
        _session.ReadIdentityAsync(cancellationToken);

    public Task<LiveDatabaseOwnership?> ReadAsync(
        string databaseName,
        CancellationToken cancellationToken) =>
        _session.ReadDatabaseOwnershipAsync(databaseName, cancellationToken);

    public void Dispose() => _session.Dispose();
}
