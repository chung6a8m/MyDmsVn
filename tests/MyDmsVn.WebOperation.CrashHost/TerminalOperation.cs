using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDmsVn.WebOperation.CrashHost;

// Test-only operator child: separate SQL identity, actual production maintenance call,
// and externally killed OS process at the exact caller-owned Commit boundary.
internal static class TerminalOperation
{
    internal static async Task RunAsync(ControlChannel channel, CrashRequest request)
    {
        using var text = new StringReader(request.Body ?? throw new InvalidDataException());
        using var json = new JsonTextReader(text) { MaxDepth = 4, DateParseHandling = DateParseHandling.None };
        var body = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        var fields = new[] { "operationId", "epoch", "session", "kind", "target", "fingerprint", "actor", "decisionId", "caseId", "reason", "evidence" };
        if (json.Read() || body.Count != fields.Length || fields.Any(field => body.Property(field) == null)) throw new InvalidDataException();
        var operation = new WebOperationContext(Guid.Parse((string)body["operationId"]!), Guid.Parse((string)body["epoch"]!), Guid.Parse((string)body["session"]!),
            (WebOperationKind)(int)body["kind"]!, (long?)body["target"], Convert.FromBase64String((string)body["fingerprint"]!), new UnusedCodec());
        var actor = (int)body["actor"]!; var decision = Guid.Parse((string)body["decisionId"]!);
        var caseId = (string)body["caseId"]!; var reason = (string)body["reason"]!; var evidence = (string)body["evidence"]!;
        if (actor <= 0 || caseId.Length > 128 || reason.Length > 64 || evidence.Length > 256) throw new InvalidDataException();
        await channel.SendAsync(new { @event = "ready", processId = Process.GetCurrentProcess().Id }).ConfigureAwait(false);
        await channel.ExpectAsync("dispatch").ConfigureAwait(false);
        await ControlChannel.BoundedAsync(Task.Run(async () =>
        {
            using var stop = new CancellationTokenSource(ControlChannel.TimeoutMilliseconds);
            using var sql = new SqlConnection(request.ConnectionString); await sql.OpenAsync(stop.Token).ConfigureAwait(false);
            using (var impersonate = sql.CreateCommand())
            {
                impersonate.CommandTimeout = 10; impersonate.CommandText = "EXECUTE AS USER=N'WebOperationOperatorProbe';";
                await impersonate.ExecuteNonQueryAsync(stop.Token).ConfigureAwait(false);
            }
            using var transaction = sql.BeginTransaction();
            await new SqlWebOperationResolutionMaintenance(new Context(sql, transaction)).RecordNotExecutedAsync(operation, actor, decision, caseId, reason, evidence, stop.Token).ConfigureAwait(false);
            if (request.Phase == "before-commit") await Barrier(channel, request.Phase, sql.ServerProcessId).ConfigureAwait(false);
            transaction.Commit();
            if (request.Phase == "after-commit") await Barrier(channel, request.Phase, sql.ServerProcessId).ConfigureAwait(false);
        })).ConfigureAwait(false);
        await channel.SendAsync(new { @event = "response", status = 204, body = "{}", headers = new Dictionary<string, string[]>() }).ConfigureAwait(false);
    }
    private static async Task Barrier(ControlChannel channel, string phase, int session)
    {
        await channel.SendAsync(new { @event = phase, sqlSessionId = session }).ConfigureAwait(false);
        await channel.ExpectAsync("release").ConfigureAwait(false);
    }
    private sealed class Context : ISqlExecutionContext
    {
        internal Context(SqlConnection connection, SqlTransaction transaction) { Connection = connection; Transaction = transaction; }
        public IDbConnection Connection { get; } public IDbTransaction? Transaction { get; }
    }
    private sealed class UnusedCodec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException();
        public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
}
