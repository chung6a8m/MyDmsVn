using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Desktop.Tests;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.HttpHost;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Newtonsoft.Json.Linq;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Test-only exact-key transaction gate. Not an HTTP endpoint or runtime service.
// Caller must durably record the owned database/trigger identity before DDL and
// drain the browser process tree before invoking Cleanup.
internal sealed class Web5OriginalFirstRace
{
    private readonly SqlTestDatabase _database;
    private readonly Web5OriginalFirstState _state = new Web5OriginalFirstState();
    private SqlConcurrencyGate? _gate;
    private WebOperationJournalTests.Context? _operator;
    private Task? _operatorTask;
    private WebOperationHttpSqlTests.Intent? _intent;
    private Guid _epoch; private int _actor, _holder;
    private Web5WriterIdentity? _writer;
    private string? _trigger, _definition;
    private bool _ddlAttempted, _ddlConfirmed, _released;
    private Web5OriginalFirstRace(SqlTestDatabase database) { _database = database; }

    internal static async Task<Web5OriginalFirstRace> Arm(SqlTestDatabase database, JObject frozen, Action<string, Guid, string> recordOwnedIdentity, CancellationToken token)
    {
        if (database == null || frozen == null || recordOwnedIdentity == null) throw new ArgumentNullException();
        var race = new Web5OriginalFirstRace(database);
        try
        {
            using var sql = new SqlConnection(database.ConnectionString); await sql.OpenAsync(token);
            await database.VerifyOwnershipAsync(sql, token);
            race._actor = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT UserId FROM dbo.Users WHERE Username=N'integratedEditor';", cancellationToken: token));
            race._epoch = await sql.QuerySingleAsync<Guid>(new SqlTestCommand("SELECT DeploymentEpoch FROM dbo.WebDeploymentState WHERE Id=1;", cancellationToken: token));
            race._intent = Web5NegativePilot.Validate(frozen, race._actor, race._epoch);
            await Web5NegativePilot.ValidateBaseline(sql, race._intent, race._actor, token);
            var exists = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ActorUserId=@actor AND OperationId=@key;", new { actor = race._actor, key = race._intent.Key }, cancellationToken: token));
            if (exists != 0) throw new InvalidDataException("Original-first operation already exists.");
            race._state.Arm(race._intent.Key);
            race._trigger = "Web5Race_" + Guid.NewGuid().ToString("N");
            recordOwnedIdentity(database.DatabaseName, race._intent.Key, race._trigger);
            race._gate = await SqlConcurrencyGate.AcquireAsync(database.ConnectionString, "web5-original-first", token);
            race._definition = "CREATE TRIGGER dbo.[" + race._trigger + "] ON dbo.WebOperationJournal AFTER INSERT AS BEGIN IF EXISTS(SELECT 1 FROM inserted WHERE OperationId='" + race._intent.Key.ToString("D") + "' AND ActorUserId=" + race._actor.ToString(CultureInfo.InvariantCulture) + ") BEGIN " + race._gate.TransactionWaitSql + " END END;";
            race._ddlAttempted = true;
            await sql.ExecuteAsync(new SqlTestCommand(race._definition, cancellationToken: token));
            var definition = await sql.QuerySingleAsync<string>(new SqlTestCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@name));", new { name = "dbo." + race._trigger }, cancellationToken: token));
            if (definition != race._definition) throw new InvalidDataException("Race trigger definition mismatch.");
            race._ddlConfirmed = true;
            return race;
        }
        catch (Exception error)
        {
            // If DDL may have executed, retain the entire allocation; no guessed drop.
            if (race._ddlAttempted) GuiTestSafety.Poison(error);
            if (race._gate != null) { try { await race._gate.ReleaseAsync(CancellationToken.None); race._gate.Dispose(); } catch (Exception cleanup) { GuiTestSafety.Poison(cleanup); throw new AggregateException(error, cleanup); } }
            throw;
        }
    }
    internal async Task ObserveWriterAndOperator(Guid key, CancellationToken token)
    {
        if (_gate == null || _intent == null || key != _intent.Key) throw new InvalidDataException("Original-first key mismatch.");
        using var sql = new SqlConnection(_database.ConnectionString); await sql.OpenAsync(token);
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            _writer = await Web5WriterDrain.Observe(sql, _gate.ControllerSessionId, token);
            if (_writer != null) { _holder = _writer.SessionId; break; }
            await Task.Delay(25, token);
        }
        if (_holder <= 0) throw new TimeoutException("Original writer did not reach the owned transaction gate.");
        _state.WriterObserved(key, _holder);
        Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.WriterObserved);
        var session = await sql.QuerySingleAsync<Guid>(new SqlTestCommand("SELECT TOP(1) SessionId FROM dbo.WebAuthSessions WHERE UserId=@actor AND RevokedAtUtc IS NULL ORDER BY CreatedAtUtc DESC;", new { actor = _actor }, cancellationToken: token));
        var kind = (WebOperationKind)Enum.Parse(typeof(WebOperationKind), _intent.Kind.Replace(".", ""));
        var hash = Enumerable.Range(0, 32).Select(i => byte.Parse(_intent.Fingerprint.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        var target = _intent.Target == null ? (long?)null : long.Parse(_intent.Target, CultureInfo.InvariantCulture);
        _operator = new WebOperationJournalTests.Context(new SqlConnection(_database.ConnectionString));
        await _operator.Sql.OpenAsync(token); _operator.Tx = _operator.Sql.BeginTransaction();
        var operation = new WebOperationContext(key, _epoch, session, kind, target, hash, new WebOperationResultCodec());
        _operatorTask = new SqlWebOperationResolutionMaintenance(_operator).RecordNotExecutedAsync(operation, _actor, Guid.NewGuid(), "WEB5-IIS-RACE", "OPERATOR_VERIFIED", "owned-iis-race", token);
        clock.Restart();
        while (clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            if (_operatorTask.IsCompleted) throw new InvalidDataException("Operator finished before observed contention.");
            var waiting = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM sys.dm_exec_requests r WHERE r.session_id=@waiter AND r.blocking_session_id=@holder AND r.database_id=DB_ID() AND EXISTS(SELECT 1 FROM sys.dm_tran_locks l WHERE l.request_session_id=r.session_id AND l.resource_type='APPLICATION' AND l.request_status='WAIT');", new { waiter = _operator.Sql.ServerProcessId, holder = _holder }, cancellationToken: token));
            if (waiting == 1) { _state.OperatorObserved(key, _operator.Sql.ServerProcessId); Web5OriginalFirstTelemetry.Record(Web5OriginalFirstStage.OperatorObserved); return; }
            await Task.Delay(25, token);
        }
        throw new TimeoutException("Operator was not observed waiting on the original writer fence.");
    }
    internal async Task Release(Guid key, CancellationToken token)
    {
        if (_gate == null || _intent == null || _operatorTask == null || _operator == null) throw new InvalidOperationException("Original-first race not armed.");
        _state.Release(key); await _gate.ReleaseAsync(token); _released = true;
        try { await _operatorTask; throw new InvalidDataException("Operator unexpectedly recorded a terminal negative."); }
        catch (WebOperationException error) when (error.Code == "Operation.ResolutionConflict") { }
        if (_operator.Tx.Connection != null) _operator.Tx.Rollback();
        using var sql = new SqlConnection(_database.ConnectionString); await sql.OpenAsync(token);
        var hash = Enumerable.Range(0, 32).Select(i => byte.Parse(_intent.Fingerprint.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
        var kind = (byte)Enum.Parse(typeof(WebOperationKind), _intent.Kind.Replace(".", ""));
        var target = _intent.Target == null ? (long?)null : long.Parse(_intent.Target, CultureInfo.InvariantCulture);
        var count = await sql.QuerySingleAsync<int>(new SqlTestCommand("SELECT COUNT(*) FROM dbo.WebOperationJournal j WHERE j.ActorUserId=@actor AND j.OperationId=@key AND j.DeploymentEpoch=@epoch AND j.OperationKind=@kind AND (j.RequestTargetId=@target OR (j.RequestTargetId IS NULL AND @target IS NULL)) AND j.RequestFingerprint=@hash AND j.Disposition='COMMITTED' AND j.ResultBytes IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.WebOperationTerminalAudit a WHERE a.ActorUserId=j.ActorUserId AND a.OperationId=j.OperationId);", new { actor = _actor, key, epoch = _epoch, kind, target, hash }, cancellationToken: token));
        if (count != 1) throw new InvalidDataException("Original-first durable result or audit mismatch.");
        _state.Complete(key);
    }
    internal async Task CleanupAfterBrowserDrain(CancellationToken token)
    {
        try
        {
            if (_gate != null && !_released) { await _gate.ReleaseAsync(token); _released = true; }
            if (_operatorTask != null && await Task.WhenAny(_operatorTask, Task.Delay(TimeSpan.FromSeconds(15), token)) != _operatorTask) throw new TimeoutException("Operator task did not drain.");
            if (_operatorTask != null) try { await _operatorTask; } catch (Exception) { /* Original scenario error remains authoritative. */ }
            if (_operator?.Tx.Connection != null) _operator.Tx.Rollback(); _operator?.Dispose(); _operator = null;
            // A new physical observer cannot borrow the writer's pooled connection.
            var observerConnection = new SqlConnectionStringBuilder(_database.ConnectionString) { Pooling = false, MultipleActiveResultSets = false };
            using var sql = new SqlConnection(observerConnection.ConnectionString); await sql.OpenAsync(token); await _database.VerifyOwnershipAsync(sql, token);
            if (_ddlAttempted && _writer == null) throw new InvalidOperationException("Original writer identity was never observed; retain allocation.");
            if (_writer != null) await Web5WriterDrain.Wait(cancellation => Web5WriterDrain.IsBusy(sql, _writer, cancellation), TimeSpan.FromSeconds(5), token);
            if (_intent != null)
            {
                var resource = "MyDmsVn.WebOperation:" + _actor.ToString(CultureInfo.InvariantCulture) + ":" + _intent.Key.ToString("N");
                using var fence = sql.BeginTransaction();
                var result = await sql.QuerySingleAsync<int>(new SqlTestCommand("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; SELECT @result;", new { resource }, transaction: fence, cancellationToken: token));
                if (result < 0) throw new InvalidOperationException("Original key fence remains held; retain allocation.");
                fence.Rollback();
            }
            if (_ddlAttempted)
            {
                if (!_ddlConfirmed || _trigger == null || _definition == null) throw new InvalidOperationException("Race trigger ownership is uncertain.");
                var definition = await sql.QuerySingleAsync<string>(new SqlTestCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@name));", new { name = "dbo." + _trigger }, cancellationToken: token));
                if (definition != _definition) throw new InvalidDataException("Race trigger ownership changed.");
                await sql.ExecuteAsync(new SqlTestCommand("DROP TRIGGER dbo.[" + _trigger + "];", cancellationToken: token));
            }
            _gate?.Dispose(); _gate = null;
        }
        catch (Exception error) { GuiTestSafety.Poison(error); throw; }
    }
}
