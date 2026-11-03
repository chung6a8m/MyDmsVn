using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.ExceptionServices;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

internal sealed class Web5WriterIdentity
{
    internal int SessionId { get; }
    internal DateTime LoginTime { get; }
    internal Guid ConnectionId { get; }
    internal Web5WriterIdentity(int sessionId, DateTime loginTime, Guid connectionId)
    {
        if (sessionId <= 0 || loginTime == default || connectionId == Guid.Empty) throw new InvalidDataException("Invalid writer identity.");
        SessionId = sessionId; LoginTime = loginTime; ConnectionId = connectionId;
    }
    internal bool IsActive(int session, DateTime loginTime, Guid connection, int observer, int requests, int locks)
        => session == SessionId && loginTime == LoginTime && connection == ConnectionId && session != observer && (requests > 0 || locks > 0);
}

internal static class Web5WriterDrain
{
    internal static void ThrowFailures(Exception? scenario, Exception? cleanup, Exception? worker)
    {
        var failures = new[] { scenario, cleanup, worker }.Where(error => error != null).Distinct().Cast<Exception>().ToArray();
        if (failures.Length == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Length > 1) throw new AggregateException("Owned SQL writer scenario and cleanup failed.", failures);
    }
    internal static async Task<Exception?> AwaitWorker(Task worker, TimeSpan timeout, CancellationToken token)
    {
        if (await Task.WhenAny(worker, Task.Delay(timeout, token)) != worker)
        {
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("Owned SQL writer did not drain.");
        }
        try { await worker; return null; } catch (Exception error) { return error; }
    }
    internal static async Task<Web5WriterIdentity?> Observe(SqlConnection sql, int controller, CancellationToken token)
    {
        var rows = (await sql.QueryAsync<(int SessionId, DateTime LoginTime, Guid ConnectionId)>(new SqlTestCommand("SELECT r.session_id,s.login_time,c.connection_id FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id JOIN sys.dm_exec_connections c ON c.session_id=r.session_id AND c.parent_connection_id IS NULL WHERE r.blocking_session_id=@controller AND r.database_id=DB_ID();", new { controller }, cancellationToken: token))).ToArray();
        if (rows.Length > 1) throw new InvalidDataException("Ambiguous original writer.");
        return rows.Length == 0 ? null : new Web5WriterIdentity(rows[0].SessionId, rows[0].LoginTime, rows[0].ConnectionId);
    }
    internal static async Task<bool> IsBusy(SqlConnection sql, Web5WriterIdentity writer, CancellationToken token)
    {
        var rows = (await sql.QueryAsync<(int SessionId, DateTime LoginTime, Guid ConnectionId, int Requests, int Locks)>(new SqlTestCommand(
            "SELECT s.session_id,s.login_time,c.connection_id,(SELECT COUNT(*) FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id AND r.session_id<>@@SPID),(SELECT COUNT(*) FROM sys.dm_tran_locks l WHERE l.request_session_id=s.session_id AND l.resource_database_id=DB_ID() AND l.resource_type='APPLICATION') FROM sys.dm_exec_sessions s JOIN sys.dm_exec_connections c ON c.session_id=s.session_id WHERE s.session_id=@session AND c.connection_id=@connection;",
            new { session = writer.SessionId, connection = writer.ConnectionId }, cancellationToken: token))).ToArray();
        if (rows.Length > 1) throw new InvalidOperationException("Ambiguous original writer connection; retain allocation.");
        return rows.Length == 1 && writer.IsActive(rows[0].SessionId, rows[0].LoginTime, rows[0].ConnectionId, sql.ServerProcessId, rows[0].Requests, rows[0].Locks);
    }
    internal static async Task Wait(Func<CancellationToken, Task<bool>> active, TimeSpan timeout, CancellationToken token)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
        bounded.CancelAfter(timeout);
        try
        {
            while (await active(bounded.Token)) await Task.Delay(25, bounded.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested && bounded.IsCancellationRequested)
        { throw new TimeoutException("Original writer did not drain."); }
    }
}
