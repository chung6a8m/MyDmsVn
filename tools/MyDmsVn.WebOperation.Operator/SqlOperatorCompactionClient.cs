using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.Operator;

public sealed class SqlOperatorCompactionClient : IOperatorCompactionClient
{
    private readonly IMaintenanceSqlConnectionFactory _connections;
    private readonly IMaintenanceEvidenceStore? _evidence;

    public SqlOperatorCompactionClient(IMaintenanceSqlConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public SqlOperatorCompactionClient(IMaintenanceSqlConnectionFactory connections, IMaintenanceEvidenceStore evidence) : this(connections) =>
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));

    public Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.HelpRequested || options.Execute) throw new ArgumentException("Exact preview options are required.", nameof(options));
        return PreviewCoreAsync(options, cancellationToken);
    }

    private async Task<OperatorCompactionResult> PreviewCoreAsync(CompactOptions options, CancellationToken cancellationToken)
    {
        using var connection = await _connections.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = options.TimeoutSeconds;
            // The same epoch lock, resource, lock mode and owner as SqlWebOperationJournal.FenceAsync.
            command.CommandText = @"
IF NOT EXISTS(SELECT 1 FROM dbo.WebDeploymentState WITH(HOLDLOCK) WHERE Id=1 AND DeploymentEpoch=@currentEpoch)
BEGIN
    SELECT -1;
    RETURN;
END;
DECLARE @resource nvarchar(255)=N'MyDmsVn.WebOperation:'+CONVERT(nvarchar(10),@actor)+N':'+LOWER(REPLACE(CONVERT(nvarchar(36),@operation),N'-',N''));
DECLARE @lock int;
EXEC @lock=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @lock<0
BEGIN
    SELECT CASE WHEN @lock=-1 THEN -2 ELSE -3 END;
    RETURN;
END;
SELECT COALESCE((
    SELECT CASE
        WHEN Disposition='COMMITTED' AND ResultBytes IS NOT NULL AND ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 1
        WHEN Disposition COLLATE Latin1_General_100_BIN2='EXPIRED' AND DATALENGTH(Disposition)=DATALENGTH('EXPIRED')
            AND ResultBytes IS NULL AND ResultExpiresAtUtc<=SYSUTCDATETIME() THEN 2
        ELSE 0 END
    FROM dbo.WebOperationJournal
    WHERE ActorUserId=@actor AND OperationId=@operation AND DeploymentEpoch=@storedEpoch AND RequestFingerprint=@fingerprint
),0);";
            command.Parameters.Add("@currentEpoch", SqlDbType.UniqueIdentifier).Value = options.CurrentEpoch;
            command.Parameters.Add("@storedEpoch", SqlDbType.UniqueIdentifier).Value = options.StoredEpoch;
            command.Parameters.Add("@actor", SqlDbType.Int).Value = options.ActorUserId;
            command.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = options.OperationId;
            command.Parameters.Add("@fingerprint", SqlDbType.Binary, 32).Value = options.FingerprintBytes();
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) switch
            {
                1 => new OperatorCompactionResult(OperatorCompactionOutcome.Eligible),
                2 => new OperatorCompactionResult(OperatorCompactionOutcome.NoChange),
                -1 => throw new WebOperationException("Deployment.EpochMismatch"),
                -2 => throw new TimeoutException("The journal fence could not be acquired in time."),
                0 => throw new WebOperationException("Operation.ResolutionConflict"),
                _ => new OperatorCompactionResult(OperatorCompactionOutcome.Unknown)
            };
        }
        finally
        {
            transaction.Rollback();
        }
    }

    public async Task<OperatorCompactionResult> ExecuteAsync(CompactOptions options, CancellationToken cancellationToken)
    {
        if (options == null || options.HelpRequested || !options.Execute)
            throw new ArgumentException("Exact execute options are required.", nameof(options));
        // Lazy creation keeps preview independent of evidence configuration.
        var evidence = _evidence ?? new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(
            Environment.GetEnvironmentVariable(OperatorCompactionApplication.EvidenceRootEnvironmentVariable) ?? string.Empty,
            Environment.GetEnvironmentVariable(OperatorCompactionApplication.EvidenceBackupRootEnvironmentVariable) ?? string.Empty,
            TimeSpan.FromDays(90)));
        string? immutable = null;
        return await OperatorCompactionExecution.RunAsync(options, evidence,
            token => PreviewCoreAsync(options, token), ReadLoginAsync,
            async (attempt, login, token) =>
            {
                SqlConnection? connection = null;
                SqlTransaction? transaction = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    connection = await _connections.OpenAsync(options, token).ConfigureAwait(false);
                    transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                }
                catch (Exception exception)
                {
                    var failures = OperatorCompactionCommitBoundary.Cleanup(() => connection?.Close(), CompactionCleanupFailure.Close) |
                        OperatorCompactionCommitBoundary.Cleanup(() => connection?.Dispose(), CompactionCleanupFailure.ConnectionDispose);
                    return new CompactionTransactionResult(failures == CompactionCleanupFailure.None
                        ? MaintenanceTransactionCertainty.NotStarted : MaintenanceTransactionCertainty.Unknown, exception, failures);
                }
                var context = new CompactionContext(connection, transaction);
                return await OperatorCompactionCommitBoundary.RunAsync(
                    ct => new SqlWebOperationMaintenance(context).CompactAsync(options.CurrentEpoch, options.StoredEpoch,
                        options.ActorUserId, options.OperationId, options.CorrelationId,
                        async (actualLogin, currentEpoch, storedEpoch, actor, operation, correlation) =>
                        {
                            // CompactAsync has already acquired the canonical epoch/key fence.
                            // Its primitive has no fingerprint parameter, so enforce the exact row here before UPDATE.
                            if (actualLogin != login || currentEpoch != options.CurrentEpoch || storedEpoch != options.StoredEpoch ||
                                actor != options.ActorUserId || operation != options.OperationId || correlation != options.CorrelationId ||
                                !OperatorCompactionExecution.ValidEvent(attempt, options.CaseId))
                                throw new WebOperationException("Operation.ResolutionConflict");
                            immutable = await ReadImmutableAsync(connection, transaction, options, expired: false, ct).ConfigureAwait(false);
                            if (string.IsNullOrEmpty(immutable)) throw new WebOperationException("Operation.ResolutionConflict");
                        }, ct), transaction.Commit, transaction.Rollback, connection.Close, transaction.Dispose, connection.Dispose,
                    token).ConfigureAwait(false);
            }, async token =>
            {
                using var connection = await _connections.OpenAsync(options, token).ConfigureAwait(false);
                var actual = await ReadImmutableAsync(connection, null, options, expired: true, token).ConfigureAwait(false);
                return !string.IsNullOrEmpty(immutable) && string.Equals(immutable, actual, StringComparison.Ordinal);
            }, cancellationToken).ConfigureAwait(false);

        async Task<string> ReadLoginAsync(CancellationToken token)
        {
            using var connection = await _connections.OpenAsync(options, token).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandTimeout = options.TimeoutSeconds;
            command.CommandText = "SELECT ORIGINAL_LOGIN();";
            return (string)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadImmutableAsync(SqlConnection connection, SqlTransaction? transaction,
        CompactOptions options, bool expired, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandTimeout = options.TimeoutSeconds;
        command.CommandText = @"
SELECT (SELECT ActorUserId,OperationId,DeploymentEpoch,ContractVersion,CodecVersion,OperationKind,RequestTargetId,
    RequestFingerprint,ResultDocumentId,CreatedAtUtc,CompletedAtUtc,ResultExpiresAtUtc
FROM dbo.WebOperationJournal
WHERE ActorUserId=@actor AND OperationId=@operation AND DeploymentEpoch=@storedEpoch AND RequestFingerprint=@fingerprint
    AND Disposition COLLATE Latin1_General_100_BIN2=@disposition AND DATALENGTH(Disposition)=DATALENGTH(@disposition)
    AND ((@expired=1 AND ResultBytes IS NULL) OR (@expired=0 AND ResultBytes IS NOT NULL))
    AND ResultExpiresAtUtc<=SYSUTCDATETIME()
    AND EXISTS(SELECT 1 FROM dbo.WebDeploymentState WHERE Id=1 AND DeploymentEpoch=@currentEpoch)
FOR JSON PATH,WITHOUT_ARRAY_WRAPPER,INCLUDE_NULL_VALUES);";
        command.Parameters.Add("@actor", SqlDbType.Int).Value = options.ActorUserId;
        command.Parameters.Add("@operation", SqlDbType.UniqueIdentifier).Value = options.OperationId;
        command.Parameters.Add("@storedEpoch", SqlDbType.UniqueIdentifier).Value = options.StoredEpoch;
        command.Parameters.Add("@currentEpoch", SqlDbType.UniqueIdentifier).Value = options.CurrentEpoch;
        command.Parameters.Add("@fingerprint", SqlDbType.Binary, 32).Value = options.FingerprintBytes();
        command.Parameters.Add("@disposition", SqlDbType.VarChar, 9).Value = expired ? "EXPIRED" : "COMMITTED";
        command.Parameters.Add("@expired", SqlDbType.Bit).Value = expired;
        return await command.ExecuteScalarAsync(token).ConfigureAwait(false) as string;
    }

    private sealed class CompactionContext(SqlConnection connection, SqlTransaction transaction) : ISqlExecutionContext
    {
        public IDbConnection Connection { get; } = connection;
        public IDbTransaction? Transaction { get; } = transaction;
    }
}
