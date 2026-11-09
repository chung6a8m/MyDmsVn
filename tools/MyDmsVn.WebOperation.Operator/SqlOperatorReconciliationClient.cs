using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.Operator;

public sealed class SqlOperatorReconciliationClient : IOperatorReconciliationClient
{
    private readonly IMaintenanceSqlConnectionFactory _connections;
    private readonly IMaintenanceEvidenceStore? _evidence;
    public SqlOperatorReconciliationClient(IMaintenanceSqlConnectionFactory connections, IMaintenanceEvidenceStore? evidence = null)
    { _connections = connections ?? throw new ArgumentNullException(nameof(connections)); _evidence = evidence; }

    public async Task<OperatorReconciliationResult> ReconcileAsync(ReconcileCommittedOptions options, CancellationToken token)
    {
        if (options == null || options.HelpRequested) throw new ArgumentException("An exact reconciliation target is required.", nameof(options));
        try
        {
            var evidence = _evidence ?? new MaintenanceEvidenceStore(new MaintenanceEvidencePolicy(
                Environment.GetEnvironmentVariable(OperatorCompactionApplication.EvidenceRootEnvironmentVariable)!,
                Environment.GetEnvironmentVariable(OperatorCompactionApplication.EvidenceBackupRootEnvironmentVariable)!, TimeSpan.FromDays(90)));
            using var connection = await _connections.OpenAsync(options, token).ConfigureAwait(false);
            string login;
            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = options.TimeoutSeconds;
                command.CommandText = "SELECT ORIGINAL_LOGIN();";
                login = (string)await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            }
            MaintenanceEvidenceRecord Record(MaintenanceEvidencePhase phase, MaintenanceReadback readback) =>
                MaintenanceEvidenceRecord.FromReconciliation(options.EnvironmentName, options.DatabaseName, login,
                    options.ActorUserId, options.OperationId, options.CorrelationId, options.CurrentEpoch, options.StoredEpoch,
                    options.FingerprintBytes(), options.CaseId, phase, readback);
            await evidence.WriteAttemptAsync(Record(MaintenanceEvidencePhase.Attempt, MaintenanceReadback.NotRun), token).ConfigureAwait(false);
            WebOperationBusinessReconciliation result;
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                try
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandTimeout = options.TimeoutSeconds;
                    command.CommandText = "SELECT COUNT(*) FROM dbo.WebDeploymentState WITH(HOLDLOCK) WHERE Id=1 AND DeploymentEpoch=@epoch;";
                    command.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = options.CurrentEpoch;
                    if (!Equals(await command.ExecuteScalarAsync(token).ConfigureAwait(false), 1))
                        throw new InvalidOperationException("The current deployment epoch does not match.");
                    result = await new SqlWebOperationReconciliationMaintenance(new ReadContext(connection, transaction))
                        .ReconcileAsync(options.ActorUserId, options.StoredEpoch, options.OperationId, options.FingerprintBytes(), token).ConfigureAwait(false);
                }
                finally { transaction.Rollback(); }
            }
            // Successful read rollback is required before publishing even a bounded positive outcome.
            await evidence.WriteOutcomeAsync(Record(MaintenanceEvidencePhase.Readback, result.Outcome switch
            {
                WebOperationReconciliationOutcome.Committed => MaintenanceReadback.BusinessCommittedResultUnavailable,
                WebOperationReconciliationOutcome.Superseded => MaintenanceReadback.BusinessCommittedResultUnavailableSuperseded,
                _ => MaintenanceReadback.Unknown
            }), token).ConfigureAwait(false);
            var delivered = await evidence.FinalizeAndBackupAsync(options.CaseId, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new OperatorReconciliationResult(delivered.Delivery == MaintenanceEvidenceDelivery.Delivered ? result.Outcome : WebOperationReconciliationOutcome.Unknown);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return new OperatorReconciliationResult(WebOperationReconciliationOutcome.Unknown); }
    }

    private sealed class ReadContext(SqlConnection connection, SqlTransaction transaction) : ISqlExecutionContext
    {
        public IDbConnection Connection => connection;
        public IDbTransaction? Transaction => transaction;
    }
}
