using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.Operator;

public interface IOperatorSqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(InspectOptions options, CancellationToken cancellationToken);
}

public sealed class OperatorSqlConnectionFactory : IOperatorSqlConnectionFactory
{
    private readonly string _connectionString;

    public OperatorSqlConnectionFactory(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The dedicated operator connection is not configured.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog) || builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("model", StringComparison.OrdinalIgnoreCase) || builder.InitialCatalog.Equals("msdb", StringComparison.OrdinalIgnoreCase) ||
            builder.InitialCatalog.Equals("tempdb", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(builder.AttachDBFilename) || builder.UserInstance)
            throw new InvalidOperationException("A fixed application database operator connection is required.");
        builder.Pooling = false;
        _connectionString = builder.ConnectionString;
    }

    public async Task<SqlConnection> OpenAsync(InspectOptions options, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString);
        if (!builder.InitialCatalog.Equals(options.DatabaseName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The configured operator database does not match the explicit target.");
        builder.ConnectTimeout = builder.ConnectTimeout <= 0
            ? options.TimeoutSeconds
            : Math.Min(builder.ConnectTimeout, options.TimeoutSeconds);
        var connection = new SqlConnection(builder.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}

public sealed class SqlOperatorInspectionClient : IOperatorInspectionClient
{
    private readonly IOperatorSqlConnectionFactory _connections;
    public SqlOperatorInspectionClient(IOperatorSqlConnectionFactory connections) =>
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));

    public async Task<WebOperationInspection> InspectAsync(InspectOptions options, CancellationToken cancellationToken)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        using var connection = await _connections.OpenAsync(options, cancellationToken).ConfigureAwait(false);
        using (var target = connection.CreateCommand())
        {
            target.CommandText = "SELECT DB_NAME();";
            target.CommandTimeout = options.TimeoutSeconds;
            var actual = Convert.ToString(await target.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
            if (!string.Equals(actual, options.DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The connected database does not match the explicit target.");
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var context = new InspectionContext(connection, transaction);
        try
        {
            WebOperationInspection result;
            try
            {
                result = await new SqlWebOperationResolutionMaintenance(context).InspectAsync(options.DeploymentEpoch,
                    options.ActorUserId, options.OperationId, options.FingerprintBytes(), cancellationToken).ConfigureAwait(false);
            }
            catch (WebOperationException exception) when (exception.Code == "Operation.Unavailable" && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The bounded SQL inspection was cancelled.", exception, cancellationToken);
            }
            transaction.Rollback();
            return result;
        }
        catch
        {
            if (transaction.Connection != null)
            {
                try { transaction.Rollback(); }
                catch { /* Disposal still closes the non-pooled operator connection. */ }
            }
            throw;
        }
    }

    private sealed class InspectionContext : ISqlExecutionContext
    {
        public InspectionContext(SqlConnection connection, SqlTransaction transaction)
        {
            Connection = connection;
            Transaction = transaction;
        }
        public IDbConnection Connection { get; }
        public IDbTransaction? Transaction { get; }
    }
}
