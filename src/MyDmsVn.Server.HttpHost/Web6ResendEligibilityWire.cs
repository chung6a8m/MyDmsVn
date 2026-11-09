using System;
using System.Globalization;
using System.Linq;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

internal static class Web6ResendEligibilityWire
{
    internal static JObject Context(WebHostOptions options, Guid epoch, int actorUserId, DateTime serverUtc)
    {
        if (!options.Web6ResendEligibilityEnabled || options.Web6CandidateId == null || epoch == Guid.Empty || actorUserId <= 0)
            throw new WebOperationException("Operation.Unavailable");
        return new JObject
        {
            ["contractVersion"] = Web6ResendReceiptCodec.ContractVersion,
            ["candidateId"] = options.Web6CandidateId,
            ["deploymentEpoch"] = epoch.ToString("D"),
            ["actorUserId"] = actorUserId,
            ["serverUtc"] = Utc(serverUtc),
            ["maximumEligibilitySeconds"] = Web6ResendReceiptCodec.LifetimeSeconds,
            ["eligibilityEnabled"] = true
        };
    }

    internal static JObject Receipt(Web6ResendIdentity identity, IssuedWeb6ResendReceipt receipt)
    {
        return new JObject
        {
            ["contractVersion"] = Web6ResendReceiptCodec.ContractVersion,
            ["candidateId"] = identity.CandidateId,
            ["deploymentEpoch"] = identity.DeploymentEpoch.ToString("D"),
            ["actorUserId"] = identity.ActorUserId,
            ["operationId"] = identity.OperationId.ToString("D"),
            ["operationKind"] = identity.OperationKind,
            ["method"] = identity.Method,
            ["routeScope"] = identity.RouteScope,
            ["requestTargetId"] = identity.RequestTargetId.HasValue
                ? new JValue(identity.RequestTargetId.Value.ToString(CultureInfo.InvariantCulture)) : JValue.CreateNull(),
            ["requestFingerprint"] = identity.FingerprintHex,
            ["issuedAtUtc"] = Utc(receipt.IssuedAtUtc),
            ["eligibleUntilUtc"] = Utc(receipt.EligibleUntilUtc),
            ["receipt"] = receipt.Token
        };
    }

    internal static JObject Eligibility(Web6ResendIdentity identity, ValidatedWeb6ResendReceipt receipt,
        WebOperationResolution? resolution, DateTime serverUtc)
    {
        string decision;
        string reason;
        if (receipt.IsExpired)
        {
            if (resolution != null) throw new WebOperationException("Operation.Unavailable");
            decision = "DENY";
            reason = "RECEIPT_EXPIRED";
        }
        else if (resolution == null)
        {
            decision = "ALLOW";
            reason = "ORIGINAL_NOT_OBSERVED_RECEIPT_VALID";
        }
        else
        {
            RequireIdentity(identity, resolution.Status);
            (decision, reason) = resolution.Status.State switch
            {
                "COMMITTED" when resolution.Status.ResultDocumentId.HasValue && resolution.Status.ResultBytes is { Length: > 0 }
                    && resolution.Terminal == null => ("DENY", "COMMITTED_RESULT_AVAILABLE"),
                "RESULT_UNAVAILABLE" when resolution.Status.ResultBytes == null && resolution.Terminal == null
                    => ("DENY", "COMMITTED_RESULT_UNAVAILABLE"),
                "RESULT_EXPIRED" when resolution.Status.ResultBytes == null && resolution.Terminal == null
                    => ("DENY", "RESULT_EXPIRED"),
                "NOT_EXECUTED" when resolution.Status.ResultBytes == null && !resolution.Status.ResultDocumentId.HasValue
                    && resolution.Terminal != null => ("DENY", "NOT_EXECUTED"),
                "INDETERMINATE" when resolution.Status.ResultBytes == null && !resolution.Status.ResultDocumentId.HasValue
                    && resolution.Terminal == null => ("INDETERMINATE", "TERMINAL_AUDIT_INCOMPLETE"),
                _ => throw new WebOperationException("Operation.Unavailable")
            };
        }
        return new JObject
        {
            ["contractVersion"] = Web6ResendReceiptCodec.ContractVersion,
            ["candidateId"] = identity.CandidateId,
            ["deploymentEpoch"] = identity.DeploymentEpoch.ToString("D"),
            ["actorUserId"] = identity.ActorUserId,
            ["operationId"] = identity.OperationId.ToString("D"),
            ["operationKind"] = identity.OperationKind,
            ["method"] = identity.Method,
            ["routeScope"] = identity.RouteScope,
            ["requestTargetId"] = identity.RequestTargetId.HasValue
                ? new JValue(identity.RequestTargetId.Value.ToString(CultureInfo.InvariantCulture)) : JValue.CreateNull(),
            ["requestFingerprint"] = identity.FingerprintHex,
            ["decision"] = decision,
            ["reason"] = reason,
            ["serverUtc"] = Utc(serverUtc),
            ["eligibleUntilUtc"] = Utc(receipt.EligibleUntilUtc)
        };
    }

    private static void RequireIdentity(Web6ResendIdentity identity, WebOperationStatus status)
    {
        if (status.OperationId != identity.OperationId || status.DeploymentEpoch != identity.DeploymentEpoch ||
            status.Kind != identity.Kind || status.RequestTargetId != identity.RequestTargetId ||
            !status.RequestFingerprint.SequenceEqual(identity.Fingerprint))
            throw new WebOperationException("Operation.Unavailable");
    }

    internal static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
}
