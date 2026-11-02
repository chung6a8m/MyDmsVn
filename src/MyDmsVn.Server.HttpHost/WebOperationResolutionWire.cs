using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using MyDmsVn.Server.Application.Operations;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.HttpHost;

public static class WebOperationResolutionWire
{
    public const string ContractVersion = "web5-resolution-v1";
    public static void Path(HttpRequestMessage request, string expected)
    {
        var uri = request.RequestUri;
        if (request.Method != HttpMethod.Get || uri == null || !uri.IsAbsoluteUri) throw new WireInputException();
        var original = uri.OriginalString;
        var index = original.IndexOf('/', original.IndexOf("://", StringComparison.Ordinal) + 3);
        if (index < 0 || original.Substring(index) != expected) throw new WireInputException();
    }

    // Serialization is not authorization. Call only with a currently authorized repository
    // result while its transaction still owns the epoch/actor/key and permission locks.
    public static JObject Envelope(WebOperationResolution resolution, int actorUserId, Guid epoch, Guid key, byte[] fingerprint)
    {
        if (actorUserId <= 0 || resolution == null) throw new WebOperationException("Operation.Unavailable");
        var status = resolution.Status;
        var negative = status.State == "NOT_EXECUTED";
        var indeterminate = status.State == "INDETERMINATE";
        if ((negative || indeterminate) && (status.ResultBytes != null || status.ResultDocumentId != null))
            throw new WebOperationException("Operation.Unavailable");
        if (negative != (resolution.Terminal != null)) throw new WebOperationException("Operation.Unavailable");
        var original = negative || indeterminate ? new WebOperationStatus(status.OperationId, status.DeploymentEpoch, status.Kind,
            status.RequestTargetId, status.RequestFingerprint, null, status.CompletedAtUtc, "RESULT_UNAVAILABLE", null) : status;
        var result = OperationsController.Envelope(original, epoch, key, fingerprint);
        result.AddFirst(new JProperty("actorUserId", actorUserId));
        result.AddFirst(new JProperty("contractVersion", ContractVersion));
        if (negative || indeterminate) result["state"] = status.State;
        JToken terminal = JValue.CreateNull();
        if (resolution.Terminal is WebOperationTerminalDecision decision)
        {
            if (decision.DecidedAtUtc.Kind != DateTimeKind.Utc || decision.DecidedAtUtc != status.CompletedAtUtc ||
                decision.CaseId == null || !Regex.IsMatch(decision.CaseId, "\\A[A-Za-z0-9_-]{1,128}\\z", RegexOptions.CultureInvariant))
                throw new WebOperationException("Operation.Unavailable");
            if (!Regex.IsMatch(decision.DecisionId.ToString("D"), "\\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\z"))
                throw new WebOperationException("Operation.Unavailable");
            terminal = new JObject { ["decisionId"] = decision.DecisionId.ToString("D"), ["caseId"] = decision.CaseId,
                ["decidedAtUtc"] = decision.DecidedAtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture) };
        }
        result["terminal"] = terminal;
        return result;
    }
}
