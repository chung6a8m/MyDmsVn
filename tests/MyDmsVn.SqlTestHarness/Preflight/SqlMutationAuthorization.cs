using System;
using MyDmsVn.SqlTestHarness.Approval;

namespace MyDmsVn.SqlTestHarness.Preflight;

public sealed class SqlMutationAuthorization
{
    public SqlMutationAuthorization(
        string approvalId,
        string instanceFingerprintSha256,
        string approvalConfigurationSha256,
        SqlInstanceIdentity identity,
        DateTimeOffset validatedAtUtc)
    {
        ApprovalId = approvalId;
        InstanceFingerprintSha256 = instanceFingerprintSha256;
        ApprovalConfigurationSha256 = approvalConfigurationSha256;
        Identity = identity;
        ValidatedAtUtc = validatedAtUtc;
    }

    public string ApprovalId { get; }

    public string InstanceFingerprintSha256 { get; }

    public string ApprovalConfigurationSha256 { get; }

    public SqlInstanceIdentity Identity { get; }

    public DateTimeOffset ValidatedAtUtc { get; }
}
