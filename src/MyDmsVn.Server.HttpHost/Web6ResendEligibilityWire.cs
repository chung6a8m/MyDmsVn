using System;
using System.Globalization;
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

    internal static string Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
}
