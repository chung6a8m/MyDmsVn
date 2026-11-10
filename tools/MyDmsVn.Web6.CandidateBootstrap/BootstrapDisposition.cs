using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Web6.CandidateBootstrap;

internal static class BootstrapDisposition
{
    // Historical records are exclusions, never a source of cleanup authority.
    private static readonly string[] Excluded = { "quarantine", "web5-historical", "20261109-b03", "20261103-web5-bounded-closeout" };

    internal static void RequireAllowedResource(string path, string root)
    {
        path = BootstrapJson.AbsolutePath(path);
        if (!BootstrapJson.IsChild(path, root) || Excluded.Any(s => path.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
            throw new IOException("Bootstrap resource is outside this attempt or belongs to excluded historical evidence.");
    }

    internal static async Task<ProtectedDispositionReceipt> ObserveAsync(Web6SelectorPlan plan, BootstrapRunState state,
        RunTransitionReceipt recovery, IReadOnlyList<AcceptedSelectorReceipt> receipts, string connectionString, CancellationToken token)
    {
        if (recovery.Stage != BootstrapRunStage.RecoveryRecorded || receipts.Count != 5)
            throw new IOException("Bootstrap disposition must follow all five gate receipts.");
        var native = receipts.Select(r => r.Read(plan)).ToArray();
        foreach (var receipt in native) RequireOwnershipEvidence(plan, state.RunId, receipt);
        var rows = native.SelectMany(r => (JArray)r["resources"]!).Cast<JObject>().ToArray();
        if (rows.Length == 0) throw new IOException("Bootstrap resource inventory is missing.");
        var sql = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master", Pooling = false, ConnectRetryCount = 0, ConnectTimeout = 15 };
        using var connection = new SqlConnection(sql.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        using (var server = connection.CreateCommand())
        {
            server.CommandTimeout = 10; server.CommandText = "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'));";
            var name = (string)await server.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (ProtectedJsonFile.Digest(Encoding.UTF8.GetBytes(name)) != plan.Text("sqlServerSha256"))
                throw new IOException("Bootstrap final SQL observation reached a different server.");
        }
        var environment = new Dictionary<string, string>();
        foreach (var name in new[] { "SystemRoot", "WINDIR", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) environment[name] = value;
        var queues = await BootstrapToolchain.RunOwnedAsync(new BootstrapProcessRequest(
            Path.Combine(Environment.SystemDirectory, "netsh.exe"), plan.TaskRoot,
            new[] { "http", "show", "servicestate", "view=requestq", "verbose=yes" }, environment, TimeSpan.FromSeconds(30)), token).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(queues)) throw new IOException("Bootstrap HTTP queue observation is absent.");
        RequireQueuesAbsent(queues, rows.Where(r => (string?)r["kind"] == "queue").Select(r => BootstrapJson.Text(r, "name")));
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested();
            var kind = BootstrapJson.Text(row, "kind");
            var name = BootstrapJson.Text(row, "name");
            if (Excluded.Any(s => name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                throw new IOException("Historical quarantine inventory is forbidden.");
            switch (kind)
            {
                case "database":
                case "login":
                    if (!BootstrapJson.IsLowerHex((string?)row["ownershipSha256"] ?? "", 64))
                        throw new IOException("Bootstrap SQL resource has no original ownership receipt.");
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 10;
                        command.CommandText = kind == "database" ? "SELECT CASE WHEN DB_ID(@name) IS NULL THEN 0 ELSE 1 END;" :
                            "SELECT CASE WHEN SUSER_ID(@name) IS NULL THEN 0 ELSE 1 END;";
                        command.Parameters.AddWithValue("@name", name);
                        if ((int)await command.ExecuteScalarAsync(token).ConfigureAwait(false) != 0)
                            throw new IOException("Bootstrap owned SQL resource is not absent.");
                    }
                    break;
                case "process":
                    if (!int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0)
                        throw new IOException("Bootstrap process identity is invalid.");
                    try
                    {
                        using var process = Process.GetProcessById(pid);
                        if (!process.HasExited) throw new IOException("Bootstrap process identifier has not drained or was reused.");
                    }
                    catch (ArgumentException) { }
                    break;
                case "queue":
                    break;
                case "deletedDirectory":
                    RequireAllowedResource(name, plan.TaskRoot);
                    if (Directory.Exists(name) || File.Exists(name)) throw new IOException("Bootstrap owned path remains present.");
                    break;
                default: throw new IOException("Bootstrap resource inventory kind is unknown.");
            }
        }
        var observed = DateTimeOffset.UtcNow;
        var nativePath = Path.Combine(plan.EvidenceRoot, "disposition-observation.json");
        var digest = ProtectedJsonFile.WriteNew(nativePath, BootstrapJson.Serialize(new JObject
        {
            ["version"] = 1, ["runId"] = state.RunId, ["candidateId"] = state.CandidateId,
            ["recoveryTransitionSha256"] = recovery.Sha256, ["observedAtUtc"] = observed.ToString("O", CultureInfo.InvariantCulture),
            ["checkedResourceCount"] = rows.Length,
            ["gateReceipts"] = new JArray(receipts.Select(r => new JObject { ["gate"] = r.Receipt.Gate,
                ["sha256"] = r.Receipt.NativeSha256 })),
            ["action"] = "independent-absence-observation",
            ["retainedEvidenceOnly"] = true, ["deletionsPerformed"] = 0,
        }));
        return ProtectedDispositionReceipt.Write(Path.Combine(plan.EvidenceRoot, "disposition.json"), state.RunId, state.CandidateId,
            nativePath, digest, recovery.Sha256, observed, true, true, true, true);
    }

    internal static void RequireQueuesAbsent(string snapshot, IEnumerable<string> names)
    {
        IReadOnlyList<Web5HttpRequestQueue> observed;
        try { observed = Web5HttpServiceState.Parse(snapshot); }
        catch (InvalidDataException) { throw new IOException("Bootstrap HTTP queue snapshot could not be verified."); }
        var expected = names.ToArray();
        if (expected.Length == 0 || expected.Any(string.IsNullOrWhiteSpace) ||
            observed.Any(q => expected.Contains(q.Name, StringComparer.Ordinal)))
            throw new IOException("Bootstrap exact HTTP request queue absence is not proved.");
    }

    internal static void RequireOwnershipEvidence(Web6SelectorPlan plan, string runId, JObject receipt)
    {
        RequireCleanupInventory(plan, runId, receipt);
        foreach (var row in ((JArray)receipt["resources"]!).OfType<JObject>().Where(r => (string?)r["kind"] is "database" or "login"))
        {
            var path = BootstrapJson.Text(row, "ownershipPath");
            RequireAllowedResource(path, plan.EvidenceRoot);
            using var file = ProtectedJsonFile.Open(path, BootstrapJson.Text(row, "ownershipSha256"));
            var original = PreparedCandidateReceipt.ReadCanonical(file.Bytes);
            if ((string?)original["runId"] != runId || (string?)original["gate"] != (string?)receipt["gate"])
                throw new IOException("Original allocation proof belongs to another attempt.");
        }
    }

    internal static void RequireCleanupInventory(Web6SelectorPlan plan, string runId, JObject receipt)
    {
        var path = BootstrapJson.Text(receipt, "cleanupInventoryPath");
        if (path != Path.Combine(plan.EvidenceRoot, BootstrapJson.Text(receipt, "gate") + "-cleanup-inventory.json"))
            throw new IOException("Bootstrap cleanup inventory belongs to another gate.");
        using var file = ProtectedJsonFile.Open(path, BootstrapJson.Text(receipt, "cleanupInventorySha256"));
        var inventory = PreparedCandidateReceipt.ReadCanonical(file.Bytes);
        if ((string?)inventory["runId"] != runId || (string?)inventory["gate"] != (string?)receipt["gate"] ||
            (bool?)inventory["deletionAuthority"] != false || !JToken.DeepEquals(inventory["resources"], receipt["resources"]))
            throw new IOException("Bootstrap acceptance differs from its sealed pre-cleanup resource inventory.");
    }
}
