using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Persistence;

/// <summary>Creates operation-scoped wrappers; the caller must materialize results before disposal.</summary>
internal sealed class LinqToDbSessionFactory : ILinqToDbSessionFactory
{
    private readonly IDbConnectionFactory _connections;
    private readonly LinqToDbMappingSchemaProvider _mappings;

    public LinqToDbSessionFactory(IDbConnectionFactory connections, LinqToDbMappingSchemaProvider mappings)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _mappings = mappings ?? throw new ArgumentNullException(nameof(mappings));
    }

    public DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        // Read the guarded connection first and the CURRENT transaction on every operation.
        var connection = RequireOpenSqlConnection(context.Connection);
        var transaction = context.Transaction;
        if (transaction is not null &&
            (transaction is not SqlTransaction sqlTransaction || !ReferenceEquals(sqlTransaction.Connection, connection)))
            throw new InvalidOperationException("The SQL transaction must belong to the current connection.");
        if (requireTransaction && transaction is null)
            throw new InvalidOperationException("This operation requires an active SQL transaction.");
        return Create(connection, (SqlTransaction?)transaction, ownsConnection: false);
    }

    public async Task<DataConnection> OpenOwnedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = await _connections.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Create(RequireOpenSqlConnection(connection), null, ownsConnection: true);
        }
        catch (Exception constructionError)
        {
            try { connection.Dispose(); }
            catch (Exception cleanupError)
            { throw new AggregateException("SQL session construction and connection cleanup failed.", constructionError, cleanupError); }
            throw;
        }
    }

    private DataConnection Create(SqlConnection connection, SqlTransaction? transaction, bool ownsConnection)
    {
        // An explicit conservative dialect avoids provider auto-detection opening a hidden connection.
        var provider = SqlServerTools.GetDataProvider(SqlServerVersion.v2012, SqlServerProvider.MicrosoftDataSqlClient);
        // These are mutually exclusive: DbConnection takes precedence over DbTransaction in 6.3.0.
        var options = new DataOptions().UseMappingSchema(_mappings.GetSchema()).UseCommandTimeout(30);
        options = transaction is null
            ? options.UseConnection(provider, connection, ownsConnection)
            : options.UseTransaction(provider, transaction);
        return new DataConnection(options);
    }

    private static SqlConnection RequireOpenSqlConnection(IDbConnection connection)
    {
        if (connection is not SqlConnection sqlConnection)
            throw new ArgumentException("A Microsoft.Data.SqlClient connection is required.", nameof(connection));
        if (sqlConnection.State != ConnectionState.Open)
            throw new InvalidOperationException("The SQL connection must already be open.");
        return sqlConnection;
    }
}
