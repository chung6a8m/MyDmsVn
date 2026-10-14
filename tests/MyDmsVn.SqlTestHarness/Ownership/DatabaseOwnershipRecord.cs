using System;

namespace MyDmsVn.SqlTestHarness.Ownership;

public sealed class DatabaseOwnershipRecord
{
    public DatabaseOwnershipRecord(
        string runId,
        string operationId,
        string databaseName,
        string ownershipToken,
        string instanceFingerprintSha256,
        string approvalId,
        DatabaseOwnershipState state,
        DateTimeOffset updatedUtc,
        int? databaseId,
        DateTimeOffset? databaseCreateDateUtc)
    {
        RunId = Require(runId, nameof(runId));
        OperationId = Require(operationId, nameof(operationId));
        DatabaseName = Require(databaseName, nameof(databaseName));
        OwnershipToken = Require(ownershipToken, nameof(ownershipToken));
        InstanceFingerprintSha256 = Require(instanceFingerprintSha256, nameof(instanceFingerprintSha256));
        ApprovalId = Require(approvalId, nameof(approvalId));
        State = state;
        UpdatedUtc = updatedUtc;
        DatabaseId = databaseId;
        DatabaseCreateDateUtc = databaseCreateDateUtc;
    }

    public string RunId { get; }

    public string OperationId { get; }

    public string DatabaseName { get; }

    public string OwnershipToken { get; }

    public string InstanceFingerprintSha256 { get; }

    public string ApprovalId { get; }

    public DatabaseOwnershipState State { get; }

    public DateTimeOffset UpdatedUtc { get; }

    public int? DatabaseId { get; }

    public DateTimeOffset? DatabaseCreateDateUtc { get; }

    public DatabaseOwnershipRecord WithState(
        DatabaseOwnershipState state,
        DateTimeOffset updatedUtc,
        int? databaseId = null,
        DateTimeOffset? databaseCreateDateUtc = null) =>
        new DatabaseOwnershipRecord(
            RunId,
            OperationId,
            DatabaseName,
            OwnershipToken,
            InstanceFingerprintSha256,
            ApprovalId,
            state,
            updatedUtc,
            databaseId ?? DatabaseId,
            databaseCreateDateUtc ?? DatabaseCreateDateUtc);

    public bool HasSameIdentity(DatabaseOwnershipRecord other) =>
        other is not null &&
        string.Equals(RunId, other.RunId, StringComparison.Ordinal) &&
        string.Equals(OperationId, other.OperationId, StringComparison.Ordinal) &&
        string.Equals(DatabaseName, other.DatabaseName, StringComparison.Ordinal) &&
        string.Equals(OwnershipToken, other.OwnershipToken, StringComparison.Ordinal) &&
        string.Equals(InstanceFingerprintSha256, other.InstanceFingerprintSha256, StringComparison.Ordinal) &&
        string.Equals(ApprovalId, other.ApprovalId, StringComparison.Ordinal);

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value;
    }
}
