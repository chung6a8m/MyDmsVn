using System;

namespace MyDmsVn.SqlTestHarness.Approval;

public sealed class SqlInstanceApproval
{
    public SqlInstanceApproval(
        string approvalId,
        SqlInstanceIdentity identity,
        string fingerprintSha256,
        DateTimeOffset? validUntilUtc,
        string? configurationSha256 = null)
    {
        ApprovalId = approvalId ?? throw new ArgumentNullException(nameof(approvalId));
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        FingerprintSha256 = fingerprintSha256 ?? throw new ArgumentNullException(nameof(fingerprintSha256));
        ValidUntilUtc = validUntilUtc;
        ConfigurationSha256 = configurationSha256 ?? fingerprintSha256;
    }

    public string ApprovalId { get; }

    public SqlInstanceIdentity Identity { get; }

    public string FingerprintSha256 { get; }

    public DateTimeOffset? ValidUntilUtc { get; }

    public string ConfigurationSha256 { get; }
}
