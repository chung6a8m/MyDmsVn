using System;
using System.Net.Http;

namespace MyDmsVn.Server.HttpHost;

// A closed protocol mapping, separate from the unchanged business/Local ErrorOr mapping.
internal static class WebOperationHttpErrors
{
    internal static bool TryMap(string code, string? correlation, out HttpResponseMessage? response)
    {
        var status = code switch
        {
            "Auth.Unauthorized" => 401,
            "Auth.Forbidden" => 403,
            "Operation.NotObserved" => 404,
            "Deployment.EpochMismatch" or "Idempotency.KeyConflict" or "Idempotency.FingerprintMismatch" => 409,
            "Operation.ResultExpired" => 410,
            "Operation.ResultUnavailable" or "Operation.Unavailable" => 503,
            _ => 0
        };
        response = status == 0 ? null : HttpJson.Error(status, code, "The protected operation is unavailable or cannot be disclosed.", correlation ?? Guid.NewGuid().ToString("N"));
        return response != null;
    }
    internal static HttpResponseMessage Map(string code, string? correlation) => TryMap(code, correlation, out var response)
        ? response! : HttpJson.Error(500, "InternalError", "An unexpected error occurred.", correlation ?? Guid.NewGuid().ToString("N"));
}
