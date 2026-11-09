using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.WebOperation.Operator;

public sealed class SqlOperatorCompactionClient : IOperatorCompactionClient
{
    private readonly IMaintenanceSqlConnectionFactory _connections;

    public SqlOperatorCompactionClient(IMaintenanceSqlConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<OperatorCompactionResult> PreviewAsync(CompactOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.HelpRequested || options.Execute) throw new ArgumentException("Exact preview options are required.", nameof(options));
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
        WHEN Disposition='EXPIRED' AND ResultBytes IS NULL AND ResultExpiresAtUtc IS NOT NULL THEN 2
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
}
