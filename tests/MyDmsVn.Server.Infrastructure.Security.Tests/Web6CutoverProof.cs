using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Only sanitized identity crosses IPC; canonical result/body remain inside the SQL owner.
internal sealed class Web6CommittedSetup
{
    private Web6CommittedSetup(Guid operation, string fingerprint) { Operation = operation; Fingerprint = fingerprint; }
    internal Guid Operation { get; }
    internal string Fingerprint { get; }
    internal static Web6CommittedSetup Parse(JObject? proof, Guid operation)
    {
        var names = new[] { "operationId", "operationKind", "requestFingerprint", "authorizedBusinessEffects", "checkId" };
        if (proof == null || !proof.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)
            .SequenceEqual(names.OrderBy(n => n, StringComparer.Ordinal)) ||
            names.Where(n => n != "authorizedBusinessEffects").Any(n => proof[n]?.Type != JTokenType.String) ||
            !Regex.IsMatch((string?)proof["operationId"] ?? "", @"\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\z") ||
            (string?)proof["operationId"] != operation.ToString("D") || (string?)proof["operationKind"] != "Sales.Create" ||
            !Regex.IsMatch((string?)proof["requestFingerprint"] ?? "", @"\A[0-9a-f]{64}\z") ||
            proof["authorizedBusinessEffects"]?.Type != JTokenType.Integer || (int)proof["authorizedBusinessEffects"]! != 1 ||
            (string?)proof["checkId"] != "web6-cutover-authentic-committed-setup") throw Invalid();
        return new Web6CommittedSetup(operation, (string)proof["requestFingerprint"]!);
    }

    internal void VerifyJournal(Guid operation, int kind, string fingerprint, Guid epoch, Guid expectedEpoch,
        string disposition, string actor, long? target, long document)
    {
        if (operation != Operation || kind != 1 || fingerprint != Fingerprint || epoch != expectedEpoch ||
            expectedEpoch == Guid.Empty || disposition != "COMMITTED" || actor != "integratedEditor" || target.HasValue || document <= 0) throw Invalid();
    }
    private static InvalidDataException Invalid() => new InvalidDataException("Invalid authentic committed cutover proof.");
}

internal static class Web6CutoverDelta
{
    internal static void VerifyZero(JObject before, JObject after)
    { if (!JToken.DeepEquals(before, after)) throw Invalid(); }

    internal static void VerifySetup(JObject before, JObject after, Guid operation, long document)
    {
        var normalized = (JObject)after.DeepClone();
        JArray Rows(string key) => normalized[key] as JArray ?? throw Invalid();
        var sales = Rows("sales").Where(r => (long?)r["SaleId"] == document).ToArray();
        var lines = Rows("saleLines").Where(r => (long?)r["SaleId"] == document).ToArray();
        var journal = Rows("journal").Where(r => string.Equals((string?)r["OperationId"], operation.ToString("D"), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sales.Length != 1 || lines.Length == 0 || journal.Length != 1 || (long?)journal[0]["ResultDocumentId"] != document ||
            ((JArray)before["sales"]!).Any(r => (long?)r["SaleId"] == document) ||
            ((JArray)before["journal"]!).Any(r => string.Equals((string?)r["OperationId"], operation.ToString("D"), StringComparison.OrdinalIgnoreCase))) throw Invalid();
        foreach (var row in sales.Concat(lines).Concat(journal)) row.Remove();
        var sequence = Rows("sequences").SingleOrDefault(r => (string?)r["name"] == "SalesReceiptNumberSequence") ?? throw Invalid();
        var prior = ((JArray)before["sequences"]!).SingleOrDefault(r => (string?)r["name"] == "SalesReceiptNumberSequence") ?? throw Invalid();
        // current_value equals START WITH even before first use; last_used_value distinguishes that case.
        if (prior["lastUsed"] == null || sequence["lastUsed"] == null) throw Invalid();
        var expected = prior["lastUsed"]!.Type == JTokenType.Null ? (decimal?)prior["start"] : (decimal?)prior["value"] + 1;
        if (expected == null || (decimal?)sequence["value"] != expected || (decimal?)sequence["lastUsed"] != expected) throw Invalid();
        sequence["value"] = prior["value"]!.DeepClone();
        sequence["lastUsed"] = prior["lastUsed"]!.DeepClone();
        VerifyZero(before, normalized);
    }

    internal static void VerifyUnavailable(JObject before, JObject after, Guid operation)
    {
        var expected = (JObject)before.DeepClone();
        var row = ((JArray)expected["journal"]!).Single(r => string.Equals((string?)r["OperationId"], operation.ToString("D"), StringComparison.OrdinalIgnoreCase));
        row["ResultBytes"] = "e30="; // UTF-8 {}, deliberately noncanonical but within the nonempty result CHECK.
        VerifyZero(expected, after);
    }
    private static InvalidDataException Invalid() => new InvalidDataException("Cutover authorized setup or zero lifecycle delta failed.");
}

// Fixture-only corruption: always attempt restoration and observation, preserving every failure.
internal static class Web6CutoverCorruption
{
    internal static async Task Run(Func<Task> disable, Func<Task> mutate, Func<Task> enable, Func<Task> verify)
    {
        var failures = new List<Exception>();
        try { await disable(); await mutate(); }
        catch (Exception failure) { failures.Add(failure); }
        finally
        {
            try { await enable(); } catch (Exception failure) { failures.Add(failure); }
            try { await verify(); } catch (Exception failure) { failures.Add(failure); }
        }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new AggregateException("Cutover result corruption or trigger restoration failed.", failures);
    }
}

internal static class Web6CutoverSessionCopy
{
    internal static void Verify(JObject source, JObject? existing, int targetActor, DateTime now)
    {
        var fields = new[] { "SessionId", "UserId", "CreatedAtUtc", "AbsoluteExpiresAtUtc", "IdleExpiresAtUtc", "RevokedAtUtc" };
        if (!source.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).SequenceEqual(fields.OrderBy(n => n, StringComparer.Ordinal)) ||
            !Guid.TryParse((string?)source["SessionId"], out var session) || session == Guid.Empty ||
            source["UserId"]?.Type != JTokenType.Integer || (int)source["UserId"]! != targetActor || targetActor <= 0 ||
            source["RevokedAtUtc"]?.Type != JTokenType.Null || (DateTime?)source["CreatedAtUtc"] >= now ||
            (DateTime?)source["IdleExpiresAtUtc"] <= now || (DateTime?)source["AbsoluteExpiresAtUtc"] <= now ||
            (existing != null && !JToken.DeepEquals(source, existing)))
            throw new InvalidDataException("Restored fixture session identity is not exact and active.");
    }
}
