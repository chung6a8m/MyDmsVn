using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Operations;

// Explicit operator construction only; never registered in the application runtime container.
// Success means commands completed in the caller's transaction, not that Commit succeeded.
public sealed class SqlWebOperationResolutionMaintenance
{
    private readonly ISqlExecutionContext _context;
    public SqlWebOperationResolutionMaintenance(ISqlExecutionContext context) { _context = context; }
    public async Task<WebOperationInspection> InspectAsync(Guid deploymentEpoch, int actorUserId, Guid operationId,
        byte[] expectedFingerprint, CancellationToken cancellationToken)
    {
        if (_context.Transaction == null) throw new InvalidOperationException("An explicit maintenance transaction is required.");
        if (deploymentEpoch == Guid.Empty || actorUserId <= 0 ||
            !Regex.IsMatch(operationId.ToString("D"), "\\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\z") ||
            expectedFingerprint == null || expectedFingerprint.Length != 32)
            throw new ArgumentException("An exact original identity is required.");
        using var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "EXEC dbo.InspectWebOperationResolution @epoch,@actor,@operation,@hash;",
            SqlRead.P("@epoch", SqlDbType.UniqueIdentifier, deploymentEpoch), SqlRead.P("@actor", SqlDbType.Int, actorUserId),
            SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operationId), SqlRead.P("@hash", SqlDbType.Binary, (byte[])expectedFingerprint.Clone()));
        try
        {
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || reader.FieldCount != 12 ||
                reader.GetGuid(0) != deploymentEpoch || reader.GetInt32(1) != actorUserId || reader.GetGuid(2) != operationId)
                throw new WebOperationException("Operation.Unavailable");
            var kind = reader.IsDBNull(3) ? (WebOperationKind?)null : (WebOperationKind)reader.GetByte(3);
            var target = reader.IsDBNull(4) ? (long?)null : reader.GetInt64(4);
            var disposition = reader.GetString(5);
            var retained = reader.GetBoolean(6); var version = reader.GetBoolean(7); var lines = reader.GetBoolean(8);
            var terminal = reader.IsDBNull(9) ? null : new WebOperationTerminalDecision(reader.GetGuid(9), reader.GetString(10),
                DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc));
            if ((disposition != "COMMITTED" && disposition != "EXPIRED" && disposition != "CANCELLED" && disposition != "INDETERMINATE") ||
                (terminal != null) != (disposition == "CANCELLED") || (retained && disposition != "COMMITTED") ||
                ((version || lines) && !retained) || await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new WebOperationException("Operation.Unavailable");
            return new WebOperationInspection(deploymentEpoch, actorUserId, operationId, kind, target, disposition, retained, version, lines, terminal);
        }
        catch (SqlException error)
        {
            throw new WebOperationException(error.Number == 51307 ? "Idempotency.FingerprintMismatch"
                : error.Number == 51305 ? "Deployment.EpochMismatch" : error.Number == 51302 || error.Number == 229 ? "Auth.Forbidden" : "Operation.Unavailable");
        }
    }

    public async Task RecordNotExecutedAsync(WebOperationContext operation, int actorUserId, Guid decisionId,
        string caseId, string reasonCode, string evidenceReference, CancellationToken cancellationToken)
    {
        if (_context.Transaction == null) throw new InvalidOperationException("An explicit maintenance transaction is required.");
        if (operation == null || actorUserId <= 0 || !Regex.IsMatch(decisionId.ToString("D"), "\\A[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\\z")
            || caseId == null || !Regex.IsMatch(caseId, "\\A[A-Za-z0-9_-]{1,128}\\z")
            || reasonCode == null || !Regex.IsMatch(reasonCode, "\\A[A-Za-z0-9_-]{1,64}\\z")
            || evidenceReference == null || !Regex.IsMatch(evidenceReference, "\\A[A-Za-z0-9_.:/-]{1,256}\\z"))
            throw new ArgumentException("An exact original identity and bounded operator audit are required.");
        using var command = SqlRead.Command(_context.Connection, _context.Transaction,
            "EXEC dbo.RecordWebOperationNotExecuted @epoch,@actor,@operation,@kind,@target,@hash,@decision,@case,@reason,@evidence;",
            SqlRead.P("@epoch", SqlDbType.UniqueIdentifier, operation.DeploymentEpoch), SqlRead.P("@actor", SqlDbType.Int, actorUserId),
            SqlRead.P("@operation", SqlDbType.UniqueIdentifier, operation.OperationId), SqlRead.P("@kind", SqlDbType.TinyInt, (byte)operation.Kind),
            SqlRead.P("@target", SqlDbType.BigInt, (object?)operation.RequestTargetId ?? DBNull.Value), SqlRead.P("@hash", SqlDbType.Binary, operation.Fingerprint),
            SqlRead.P("@decision", SqlDbType.UniqueIdentifier, decisionId), SqlRead.P("@case", SqlDbType.VarChar, caseId),
            SqlRead.P("@reason", SqlDbType.VarChar, reasonCode), SqlRead.P("@evidence", SqlDbType.VarChar, evidenceReference));
        try { await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false); }
        catch (SqlException error) when (error.Number >= 51301 && error.Number <= 51306)
        {
            throw new WebOperationException(error.Number == 51303 ? "Operation.ResolutionConflict" : error.Number == 51302 ? "Auth.Forbidden"
                : error.Number == 51305 ? "Deployment.EpochMismatch" : "Operation.Unavailable");
        }
    }
}
