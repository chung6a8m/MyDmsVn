using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

// Explicit operator construction only. Never registered in a runtime container. The caller owns
// its maintenance transaction and durable audit sink; an audit failure prevents compaction.
public sealed class SqlWebOperationMaintenance
{
    private readonly ISqlExecutionContext _context;
    public SqlWebOperationMaintenance(ISqlExecutionContext context)
    {
        _context = context;
    }
    public async Task<int> CompactAsync(Guid currentEpoch, Guid storedEpoch, int actor, Guid operation, Guid correlation,
        Func<string, Guid, Guid, int, Guid, Guid, Task> auditAttempt, CancellationToken token)
    {
        if (currentEpoch == Guid.Empty || storedEpoch == Guid.Empty || actor <= 0 || operation == Guid.Empty || correlation == Guid.Empty || auditAttempt == null)
            throw new ArgumentException("An exact key, correlation and audited operator are required.");
        if (_context.Transaction == null)
            throw new InvalidOperationException("An explicit maintenance transaction is required.");
        string login;
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "IF ISNULL(HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL'),0)<>1 THROW 51202, 'Maintenance authority required.', 1; SELECT ORIGINAL_LOGIN();"))
            login = (string)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        await SqlWebOperationJournal.FenceAsync(_context, currentEpoch, actor, operation, token).ConfigureAwait(false);
        // Audit describes an attempt, never a successful commit before the caller has committed.
        await auditAttempt(login, currentEpoch, storedEpoch, actor, operation, correlation).ConfigureAwait(false);
        using (var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "UPDATE dbo.WebOperationJournal SET Disposition='EXPIRED',ResultBytes=NULL WHERE ActorUserId=@actor AND OperationId=@operation AND DeploymentEpoch=@epoch AND Disposition='COMMITTED' AND ResultExpiresAtUtc<=SYSUTCDATETIME();",
            SqlRead.P("@actor", SqlDbType.Int, actor), SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operation), SqlRead.P("@epoch", SqlDbType.UniqueIdentifier, storedEpoch)))
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
