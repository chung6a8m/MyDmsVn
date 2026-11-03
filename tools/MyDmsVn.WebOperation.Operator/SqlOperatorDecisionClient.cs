using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.Operator;

public sealed class SqlOperatorDecisionClient : IOperatorDecisionClient
{
    private readonly IOperatorSqlConnectionFactory _connections;
    public SqlOperatorDecisionClient(IOperatorSqlConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<OperatorDecisionResult> DryRunAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var inspection = await InspectAsync(options, cancellationToken).ConfigureAwait(false);
        if (inspection.Disposition != "INDETERMINATE")
            throw new WebOperationException("Operation.ResolutionConflict");
        return OperatorDecisionResult.Preview(inspection);
    }

    public async Task<OperatorDecisionResult> ExecuteAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        return await OperatorDecisionExecution.RunAsync(options,
            token => InspectAsync(options, token), token => ExecuteTransactionAsync(options, token), cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperatorDecisionCommitOutcome> ExecuteTransactionAsync(RecordNotExecutedOptions options,
        CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        SqlTransaction transaction;
        try
        {
            transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        }
        catch
        {
            try { connection.Close(); } catch { }
            try { connection.Dispose(); } catch { }
            throw;
        }
        var context = new DecisionContext(connection, transaction);
        var operation = new WebOperationTerminalIdentity(options.OperationId, options.DeploymentEpoch,
            options.Kind, options.RequestTargetId, options.FingerprintBytes());
        return await OperatorDecisionCommitBoundary.RunAsync(
            async token =>
            {
                await new SqlWebOperationResolutionMaintenance(context).RecordNotExecutedAsync(operation, options.ActorUserId,
                    options.DecisionId, options.CaseId, options.ReasonCode, options.EvidenceReference, token).ConfigureAwait(false);
            }, transaction.Commit, transaction.Rollback, connection.Close, transaction.Dispose, connection.Dispose,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<WebOperationInspection> InspectAsync(RecordNotExecutedOptions options, CancellationToken cancellationToken)
    {
        using var connection = await _connections.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var rollbackStarted = false;
        try
        {
            WebOperationInspection result;
            try
            {
                result = await new SqlWebOperationResolutionMaintenance(new DecisionContext(connection, transaction))
                    .InspectAsync(options.DeploymentEpoch, options.ActorUserId, options.OperationId,
                        options.FingerprintBytes(), cancellationToken).ConfigureAwait(false);
            }
            catch (WebOperationException exception) when (exception.Code == "Operation.Unavailable" && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The bounded SQL decision inspection was cancelled.", exception, cancellationToken);
            }
            rollbackStarted = true;
            transaction.Rollback();
            return result;
        }
        catch (OperationCanceledException)
        {
            connection.Close();
            throw;
        }
        catch
        {
            if (!rollbackStarted && transaction.Connection != null) transaction.Rollback();
            throw;
        }
    }

    private sealed class DecisionContext : ISqlExecutionContext
    {
        public DecisionContext(SqlConnection connection, SqlTransaction transaction) { Connection = connection; Transaction = transaction; }
        public IDbConnection Connection { get; }
        public IDbTransaction? Transaction { get; }
    }
}
