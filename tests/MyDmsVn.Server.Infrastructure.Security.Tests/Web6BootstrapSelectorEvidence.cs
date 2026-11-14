using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using MyDmsVn.Web6.CandidateBootstrap;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal static class Web6BootstrapSelectorEvidence
{
    internal static JObject Resource(string kind, string name, string? ownership = null, string? ownershipPath = null) => new()
    { ["kind"] = kind, ["name"] = name, ["ownershipSha256"] = ownership, ["ownershipPath"] = ownershipPath };

    internal static void Host(JArray resources, Web5IisExpressIdentity identity, string queue)
    {
        var row = Resource("process", identity.Process.ProcessId.ToString(CultureInfo.InvariantCulture));
        row["startedAtUtc"] = identity.Process.StartTimeUtc.ToString("O", CultureInfo.InvariantCulture);
        resources.Add(row); resources.Add(Resource("queue", queue));
    }

    internal static async Task<(string Path, string Hash)> Sql(Web6SelectorOutput output, SqlTestDatabase database, SqlTestOwnershipManifest control)
    {
        using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync();
        await database.VerifyOwnershipAsync(sql, CancellationToken.None);
        var original = await ReadOriginalAllocation(sql);
        var payload = new JObject { ["version"] = 1, ["runId"] = output.RunId, ["gate"] = output.Gate,
            ["controlSha256"] = control.VerifiedDigest(), ["originalAllocation"] = original };
        var path = Path.Combine(output.Plan.EvidenceRoot, output.Gate + "-allocation-" + Guid.NewGuid().ToString("N") + ".json");
        return (path, ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(payload)));
    }

    private static async Task<JObject> ReadOriginalAllocation(SqlConnection sql, CancellationToken token = default)
    {
        var original = await sql.QuerySingleAsync<string>(new SqlTestCommand("SELECT (SELECT DB_NAME() AS databaseName, DB_ID() AS databaseId," +
            "CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS serverName, d.create_date AS createdAtUtc," +
            "CONVERT(nvarchar(128),p.value) AS originalToken FROM sys.databases d CROSS JOIN sys.extended_properties p " +
            "WHERE d.database_id=DB_ID() AND p.class=0 AND p.name=N'MyDmsVn.TestOwnership' FOR JSON PATH,WITHOUT_ARRAY_WRAPPER);",
            cancellationToken: token));
        return JObject.Parse(original);
    }

    internal static (string Path, string Hash) SealCutoverDatabase(Web6SelectorOutput output, Web6CutoverPair pair,
        SqlTestDatabase database, SqlTestOwnershipManifest control, string intentHash)
    {
        var identity = JObject.FromObject(database.OriginalCutoverOwnership(control));
        identity["controlSha256"] = control.VerifiedDigest();
        identity["intentSha256"] = intentHash;
        return CutoverOwnership(output.Plan.EvidenceRoot, output.RunId, pair, "database", "confirmed", database.DatabaseName, identity);
    }

    internal static (string Path, string Hash) CutoverOwnership(string evidenceRoot, string runId, Web6CutoverPair pair,
        string kind, string phase, string name, JObject identity)
    {
        var path = Path.Combine(evidenceRoot, $"cutover-{pair.ToString().ToLowerInvariant()}-{kind}-{phase}.json");
        var payload = new JObject
        {
            ["version"] = 1, ["runId"] = runId, ["gate"] = "cutover", ["pair"] = pair.ToString(),
            ["kind"] = kind, ["phase"] = phase, ["name"] = name, ["deletionAuthority"] = false,
            ["identity"] = identity.DeepClone(),
        };
        return (path, ProtectedJsonFile.WriteNew(path, BootstrapJson.Serialize(payload)));
    }

    internal static async Task<(string Path, string Hash)> CreateCutoverLogin(string evidenceRoot, string runId,
        Web6CutoverPair pair, string name, string server, string sid, Func<Task> createAndVerify)
    {
        var identity = new JObject { ["serverName"] = server, ["sid"] = sid };
        var intent = CutoverOwnership(evidenceRoot, runId, pair, "login", "intent", name, identity);
        await createAndVerify().ConfigureAwait(false);
        identity["intentSha256"] = intent.Hash;
        return CutoverOwnership(evidenceRoot, runId, pair, "login", "confirmed", name, identity);
    }

    internal static async Task PreflightSqlCleanup(string admin, IEnumerable<(SqlTestDatabase Database, SqlTestOwnershipManifest Control)> allocations)
    {
        foreach (var item in allocations)
        {
            item.Control.Verify();
            using (var owned = new SqlConnection(item.Database.ConnectionString))
            {
                await owned.OpenAsync(); await item.Database.VerifyOwnershipAsync(owned, CancellationToken.None);
            }
            using var master = new SqlConnection(new SqlConnectionStringBuilder(admin) { InitialCatalog = "master", Pooling = false, ConnectRetryCount = 0 }.ConnectionString);
            await master.OpenAsync();
            var remaining = await master.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id=DB_ID(@name)) + " +
                "(SELECT COUNT(*) FROM sys.dm_tran_locks WHERE resource_database_id=DB_ID(@name));", new { name = item.Database.DatabaseName });
            if (remaining != 0) throw new IOException("Bootstrap SQL cleanup preflight found sessions or locks; retain every resource.");
        }
    }

    internal static void Complete(Web6SelectorOutput output, JArray resources, string sqlReceipt,
        JArray? profiles = null, long? rpo = null, long? rto = null, string? manifestPath = null, string? manifestHash = null)
    {
        var payload = output.Payload();
        payload["processDrained"] = true; payload["httpQueuesAbsent"] = true; payload["sqlQuiescent"] = true; payload["resourcesVerified"] = true;
        payload["resources"] = resources; payload["sqlOwnershipReceiptSha256"] = sqlReceipt;
        payload["profiles"] = profiles; payload["recoveryRpo"] = rpo; payload["recoveryRtoMilliseconds"] = rto;
        payload["retainedManifestPath"] = manifestPath; payload["retainedManifestSha256"] = manifestHash;
        output.Complete(payload);
    }
}
