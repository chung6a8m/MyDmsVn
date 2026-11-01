using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebOperationJournalTests
{
    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Same_key_waits_replays_original_and_changed_hash_conflicts_without_second_effect() => Owned(async s =>
    {
        var op = s.Operation();
        var first = await s.Transaction();
        Assert.Null(await s.Journal(first).AdmitAsync(op, s.Actor, s.Stop.Token));
        await first.Sql.ExecuteAsync("INSERT dbo.Products(Code,Name,Unit) VALUES(N'one',N'Original',N'EA');", transaction: first.Tx);
        await s.JournalFor(first).CompleteAsync(op, s.Actor, 1, Encoding.UTF8.GetBytes("original"), s.Stop.Token);
        var second = await s.Transaction();
        var waiting = s.Track(s.Journal(second).AdmitAsync(op, s.Actor, s.Stop.Token));
        await s.Blocked(second.Sql.ServerProcessId, first.Sql.ServerProcessId);
        first.Tx.Commit();
        Assert.Equal("original", Encoding.UTF8.GetString((await waiting)!));
        second.Tx.Commit();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
        var changed = s.Operation(op.OperationId, new byte[32].Select(_ => (byte)1).ToArray());
        var third = await s.Transaction();
        Assert.Equal("Idempotency.KeyConflict", (await Assert.ThrowsAsync<WebOperationException>(() => s.Journal(third).AdmitAsync(changed, s.Actor, s.Stop.Token))).Code);
        third.Tx.Rollback();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Rollback_discards_business_and_journal_then_explicit_same_intent_can_admit() => Owned(async s =>
    {
        var op = s.Operation();
        var first = await s.Transaction();
        var journal = s.Journal(first);
        Assert.Null(await journal.AdmitAsync(op, s.Actor, s.Stop.Token));
        await first.Sql.ExecuteAsync("INSERT dbo.Products(Code,Name,Unit) VALUES(N'rollback',N'Rollback',N'EA');", transaction: first.Tx);
        await journal.CompleteAsync(op, s.Actor, 1, Encoding.UTF8.GetBytes("original"), s.Stop.Token);
        first.Tx.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Products;"));
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
        var second = await s.Transaction();
        Assert.Null(await s.Journal(second).AdmitAsync(op, s.Actor, s.Stop.Token));
        second.Tx.Rollback();
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("session", "Auth.Unauthorized")]
    [InlineData("account", "Auth.Unauthorized")]
    [InlineData("deny", "Auth.Forbidden")]
    [InlineData("membership", "Auth.Forbidden")]
    [InlineData("role", "Auth.Forbidden")]
    [InlineData("grant", "Auth.Forbidden")]
    [InlineData("binding", "Auth.Forbidden")]
    public Task Authority_is_rechecked_after_key_wait(string change, string error) => Owned(async s =>
    {
        var op = s.Operation();
        var holder = await s.Transaction();
        await holder.Sql.ExecuteAsync("EXEC sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;", new
        {
            key = "MyDmsVn.WebOperation:" + s.Actor + ":" + op.OperationId.ToString("N")
        }, holder.Tx);
        var worker = await s.Transaction();
        var waiting = s.Track(s.Journal(worker).AdmitAsync(op, s.Actor, s.Stop.Token));
        await s.Blocked(worker.Sql.ServerProcessId, holder.Sql.ServerProcessId);
        await s.Observer.ExecuteAsync(s.Change(change));
        holder.Tx.Commit();
        Assert.Equal(error, (await Assert.ThrowsAsync<WebOperationException>(async () => await waiting)).Code);
        worker.Tx.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData("session")]
    [InlineData("account")]
    [InlineData("deny")]
    [InlineData("membership")]
    [InlineData("role")]
    [InlineData("grant")]
    [InlineData("binding")]
    public Task Admitted_authority_and_absent_direct_deny_range_are_held_until_completion(string change) => Owned(async s =>
    {
        var holder = await s.Transaction();
        Assert.Null(await s.Journal(holder).AdmitAsync(s.Operation(), s.Actor, s.Stop.Token));
        var worker = await s.Transaction();
        var update = s.Track(worker.Sql.ExecuteAsync(new SqlTestCommand(s.Change(change), transaction: worker.Tx, cancellationToken: s.Stop.Token)));
        await s.Blocked(worker.Sql.ServerProcessId, holder.Sql.ServerProcessId);
        holder.Tx.Rollback();
        await update;
        worker.Tx.Rollback();
    });

    [SqlServerFact(SqlTestCategory.SqlConcurrency)]
    public Task Epoch_is_stable_across_connections_rotation_waits_and_old_epoch_cannot_disclose() => Owned(async s =>
    {
        var holder = await s.Transaction();
        var op = s.Operation();
        var journal = s.Journal(holder);
        Assert.Null(await journal.AdmitAsync(op, s.Actor, s.Stop.Token));
        await journal.CompleteAsync(op, s.Actor, 1, Encoding.UTF8.GetBytes("original"), s.Stop.Token);
        var worker = await s.Transaction();
        Assert.Equal(s.Epoch, await worker.Sql.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;", transaction: worker.Tx));
        var rotation = s.Track(worker.Sql.ExecuteAsync(new SqlTestCommand("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME();", transaction: worker.Tx, cancellationToken: s.Stop.Token)));
        await s.Blocked(worker.Sql.ServerProcessId, holder.Sql.ServerProcessId);
        holder.Tx.Commit();
        await rotation;
        worker.Tx.Commit();
        var stale = await s.Transaction();
        Assert.Equal("Deployment.EpochMismatch", (await Assert.ThrowsAsync<WebOperationException>(() => s.Journal(stale).AdmitAsync(op, s.Actor, s.Stop.Token))).Code);
        stale.Tx.Rollback();
    });

    [SqlServerTheory(SqlTestCategory.SqlConcurrency)]
    [InlineData(true)]
    [InlineData(false)]
    public Task Cancelled_or_timed_out_key_wait_never_retries_or_writes(bool cancel) => Owned(async s =>
    {
        var op = s.Operation();
        var holder = await s.Transaction();
        Assert.Null(await s.Journal(holder).AdmitAsync(op, s.Actor, s.Stop.Token));
        var waiter = await s.Transaction();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(s.Stop.Token);
        var waiting = s.Track(s.Journal(waiter).AdmitAsync(op, s.Actor, cancellation.Token));
        await s.Blocked(waiter.Sql.ServerProcessId, holder.Sql.ServerProcessId);
        if (cancel)
        {
            cancellation.Cancel();
            var failure = await Record.ExceptionAsync(async () => await waiting);
                Assert.True(cancellation.IsCancellationRequested);
                Assert.NotNull(failure);
                if (failure is SqlException sql)
                    Assert.Contains(sql.Errors.Cast<SqlError>(), error => error.Number == 0);
                else
                    Assert.IsAssignableFrom<OperationCanceledException>(failure);
        }
        else
            Assert.Equal("Operation.Unavailable", (await Assert.ThrowsAsync<WebOperationException>(async () => await waiting)).Code);
        waiter.Tx.Rollback();
        holder.Tx.Rollback();
        Assert.Equal(0, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Other_actor_same_key_is_independent_and_wrong_session_is_denied() => Owned(async s =>
    {
        var op = s.Operation();
        var first = await s.Transaction();
        var journal = s.Journal(first);
        Assert.Null(await journal.AdmitAsync(op, s.Actor, s.Stop.Token));
        await journal.CompleteAsync(op, s.Actor, 1, Encoding.UTF8.GetBytes("private"), s.Stop.Token);
        first.Tx.Commit();
        var other = await s.Observer.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'other',N'OTHER',N'Other',N'Local',N'hash',N'',N'BCrypt');");
        var session = Guid.NewGuid();
        await s.Observer.ExecuteAsync("INSERT dbo.UserRoles(UserId,RoleId) VALUES(@other,@role);DECLARE @n datetime2(7)=SYSUTCDATETIME();INSERT dbo.WebAuthSessions VALUES(@session,@other,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);", new
        {
            other,
            role = s.Role,
            session
        });
        var registry = PermissionRegistry.Create(new[] { new PermissionDefinition(PermissionKeys.SalesReceiptsWrite, "Sales", "Write") });
        var wrong = await s.Transaction();
        Assert.Equal("Auth.Unauthorized", (await Assert.ThrowsAsync<WebOperationException>(() => new SqlWebOperationJournal(wrong, new ActorAccessor(other), registry).AdmitAsync(op, other, s.Stop.Token))).Code);
        wrong.Tx.Rollback();
        var own = await s.Transaction();
        var ownOp = new WebOperationContext(op.OperationId, s.Epoch, session, op.Kind, null, op.Fingerprint, new Codec());
        Assert.Null(await new SqlWebOperationJournal(own, new ActorAccessor(other), registry).AdmitAsync(ownOp, other, s.Stop.Token));
        own.Tx.Rollback();
        var changed = await s.Transaction();
        var changedOp = new WebOperationContext(op.OperationId, s.Epoch, s.Session, WebOperationKind.SalesUpdate, 1, op.Fingerprint, new Codec());
        Assert.Equal("Idempotency.KeyConflict", (await Assert.ThrowsAsync<WebOperationException>(() => s.Journal(changed).AdmitAsync(changedOp, s.Actor, s.Stop.Token))).Code);
        changed.Tx.Rollback();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Expired_result_compacts_under_fences_and_tombstone_never_reexecutes_or_changes_identity() => Owned(async s =>
    {
        var op = s.Operation();
        await s.Observer.ExecuteAsync(@"DECLARE @n datetime2(7)=DATEADD(day,-8,SYSUTCDATETIME());
INSERT dbo.WebOperationJournal VALUES(@actor,@op,@epoch,1,1,1,NULL,@hash,1,'COMMITTED',0x7B7D,@n,@n,DATEADD(day,7,@n));",
            new
            {
                actor = s.Actor,
                op = op.OperationId,
                epoch = s.Epoch,
                hash = op.Fingerprint
            });
        var read = await s.Transaction();
        Assert.Equal("Operation.ResultExpired", (await Assert.ThrowsAsync<WebOperationException>(() => s.Journal(read).AdmitAsync(op, s.Actor, s.Stop.Token))).Code);
        read.Tx.Rollback();
        await s.Observer.ExecuteAsync("UPDATE dbo.WebDeploymentState SET DeploymentEpoch=NEWID(),RotatedAtUtc=SYSUTCDATETIME();");
        s.Epoch = await s.Observer.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
        var cleanup = await s.Transaction();
        var audited = false;
        Assert.Equal(1, await new SqlWebOperationMaintenance(cleanup).CompactAsync(s.Epoch, op.DeploymentEpoch, s.Actor, op.OperationId, Guid.NewGuid(), (login, currentEpoch, storedEpoch, actor, id, correlation) => { Assert.False(string.IsNullOrWhiteSpace(login)); Assert.Equal(s.Epoch, currentEpoch); Assert.Equal(op.DeploymentEpoch, storedEpoch); Assert.Equal(op.OperationId, id); audited = true; return Task.CompletedTask; }, s.Stop.Token));
        Assert.True(audited);
        cleanup.Tx.Commit();
        Assert.Equal("EXPIRED", await s.Observer.QuerySingleAsync<string>("SELECT Disposition FROM dbo.WebOperationJournal;"));
        foreach (var mutation in new[] { "DELETE dbo.WebOperationJournal;", "UPDATE dbo.WebOperationJournal SET ActorUserId=ActorUserId+1;", "UPDATE dbo.WebOperationJournal SET RequestFingerprint=0x01;", "UPDATE dbo.WebOperationJournal SET ResultDocumentId=2;" })
            await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync(mutation));
        var again = await s.Transaction();
        Assert.Equal("Idempotency.KeyConflict", (await Assert.ThrowsAsync<WebOperationException>(() => s.Journal(again).AdmitAsync(s.Operation(op.OperationId), s.Actor, s.Stop.Token))).Code);
        again.Tx.Rollback();
        Assert.Equal(1, await s.Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebOperationJournal WHERE ResultBytes IS NULL;"));
    });

    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Null_expiry_oversized_result_and_invalid_identity_are_rejected_by_sql() => Owned(async s =>
    {
        foreach (var variation in new[] { "NULL", "DATEADD(day,6,@n)" })
            await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,NEWID(),@epoch,1,1,1,NULL,@hash,1,'COMMITTED',0x7B7D,@n,@n," + variation + ");", new { actor = s.Actor, epoch = s.Epoch, hash = new byte[32] }));
        await Assert.ThrowsAsync<SqlException>(() => s.Observer.ExecuteAsync("DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebOperationJournal VALUES(@actor,NEWID(),@epoch,1,1,1,NULL,@hash,1,'COMMITTED',@bytes,@n,@n,DATEADD(day,7,@n));", new { actor = s.Actor, epoch = s.Epoch, hash = new byte[32], bytes = new byte[1048577] }));
    });

    private static async Task Owned(Func<Scenario, Task> body)
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Scenario? s = null;
        Exception? failure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Successful);
            s = await Scenario.Create(db);
            await body(s);
        }
        catch (Exception error) { failure = error; throw; }
        finally { try { if (s == null) await db.DisposeAsync(); else await s.Cleanup(); } catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); } }
    }
    private sealed class Scenario
    {
        public SqlTestDatabase Database = null!; public SqlConnection Observer = null!; public int Actor; public int Role; public Guid Epoch; public Guid Session = Guid.NewGuid();
        public CancellationTokenSource Stop = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        private readonly List<Context> _contexts = new List<Context>(); private readonly List<Task> _workers = new List<Task>();
        private readonly Dictionary<Context, SqlWebOperationJournal> _journals = new Dictionary<Context, SqlWebOperationJournal>();
        public static async Task<Scenario> Create(SqlTestDatabase db)
        {
            var s = new Scenario { Database = db, Observer = new SqlConnection(db.ConnectionString) };
            try
            {
                await s.Observer.OpenAsync();
                s.Actor = await s.Observer.QuerySingleAsync<int>("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) OUTPUT INSERTED.UserId VALUES(N'actor',N'ACTOR',N'Actor',N'Local',N'hash',N'',N'BCrypt');");
                s.Role = await s.Observer.QuerySingleAsync<int>("INSERT dbo.Roles(RoleName,NormalizedRoleName) OUTPUT INSERTED.RoleId VALUES(N'writer',N'WRITER');");
                await s.Observer.ExecuteAsync("INSERT dbo.UserRoles(UserId,RoleId) VALUES(@actor,@role); INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,N'Sales.Receipts.Write'); DECLARE @n datetime2(7)=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@session,@actor,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);", new
                {
                    actor = s.Actor,
                    role = s.Role,
                    session = s.Session
                });
                s.Epoch = await s.Observer.QuerySingleAsync<Guid>("SELECT DeploymentEpoch FROM dbo.WebDeploymentState;");
                return s;
            }
            catch { s.Observer.Dispose(); s.Stop.Dispose(); throw; }
        }
        public WebOperationContext Operation(Guid? id = null, byte[]? hash = null) => new WebOperationContext(id ?? Guid.NewGuid(), Epoch, Session, WebOperationKind.SalesCreate, null, hash ?? new byte[32], new Codec());
        public async Task<Context> Transaction()
        {
            var c = new Context(new SqlConnection(Database.ConnectionString));
            _contexts.Add(c);
            await c.Sql.OpenAsync(Stop.Token);
            c.Tx = c.Sql.BeginTransaction();
            return c;
        }
        public SqlWebOperationJournal Journal(Context c)
        {
            var j = new SqlWebOperationJournal(c, new ActorAccessor(Actor), PermissionRegistry.Create(new[] { new PermissionDefinition(PermissionKeys.SalesReceiptsWrite, "Sales", "Write") }));
            _journals[c] = j;
            return j;
        }
        public SqlWebOperationJournal JournalFor(Context c) => _journals[c];
        public T Track<T>(T task) where T : Task
        {
            _workers.Add(task);
            return task;
        }
        public string Change(string kind) => kind switch
        {
            "session" => "UPDATE dbo.WebAuthSessions SET RevokedAtUtc=SYSUTCDATETIME();",
            "account" => "UPDATE dbo.Users SET IsActive=0;",
            "deny" => "INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(" + Actor + ",N'Sales.Receipts.Write',0);",
            "membership" => "DELETE dbo.UserRoles;",
            "role" => "UPDATE dbo.Roles SET IsActive=0;",
            "grant" => "DELETE dbo.RolePermissions;",
            "binding" => "INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) VALUES(1," + Actor + ",N'ACTOR');",
            _ => throw new ArgumentException()
        };
        public async Task Blocked(int waiter, int holder)
        {
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (await Observer.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@waiter AND blocking_session_id=@holder;", new
                {
                    waiter,
                    holder
                }) == 1)
                    return;
                await Task.Delay(25, Stop.Token);
            }
            throw new TimeoutException("Independent SQL worker did not block on expected fence.");
        }
        public async Task Cleanup()
        {
            try
            {
                await SqlConcurrencyCleanup.CompleteAsync(() => { foreach (var c in _contexts) if (c.Tx?.Connection != null) c.Tx.Rollback(); return Task.CompletedTask; }, null, Stop, async () => { foreach (var c in _contexts) c.Dispose(); Observer.Dispose(); await Database.DisposeAsync(); }, TimeSpan.FromSeconds(20), _workers.ToArray());
            }
            finally { Stop.Dispose(); }
        }
    }
    private sealed class Context : ISqlExecutionContext, IDisposable
    {
        public Context(SqlConnection sql)
        {
            Sql = sql;
        }
        public SqlConnection Sql; public SqlTransaction Tx = null!;
        public IDbConnection Connection => Sql; public IDbTransaction? Transaction => Tx;
        public void Dispose()
        {
            Tx?.Dispose();
            Sql.Dispose();
        }
    }
    private sealed class ActorAccessor : ICurrentUserAccessor
    {
        public ActorAccessor(int id)
        {
            Current = CurrentUser.Authenticated(id, "actor", "Actor", true);
        }
        public CurrentUser Current
        {
            get;
        }
    }
    private sealed class Codec : IWebOperationResultCodec
    {
        public byte[] Encode<T>(WebOperationKind kind, T result) => throw new NotSupportedException(); public T Decode<T>(WebOperationKind kind, byte[] bytes) => throw new NotSupportedException();
    }
}
