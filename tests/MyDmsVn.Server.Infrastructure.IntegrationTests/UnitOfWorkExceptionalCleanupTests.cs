using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class UnitOfWorkExceptionalCleanupTests
{
    [Fact]
    public void Dispose_closes_connection_and_records_terminal_state_when_transaction_cleanup_throws()
    {
        var connection = new ThrowingTransactionConnection();
        var unitOfWork = new SqlUnitOfWorkFactory(new StubConnectionFactory(connection)).Create();
        unitOfWork.BeginTransaction();

        Assert.ThrowsAny<Exception>(() => unitOfWork.Dispose());

        Assert.Equal(1, connection.DisposeCount);
        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal(1, connection.Transaction.RollbackCount);
        Assert.Equal(1, connection.Transaction.DisposeCount);
        Assert.Equal(UnitOfWorkState.Disposed, unitOfWork.State);
        unitOfWork.Dispose();
        Assert.Equal(1, connection.DisposeCount);
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        private readonly IDbConnection _connection;

        public StubConnectionFactory(IDbConnection connection)
        {
            _connection = connection;
        }

        public IDbConnection OpenConnection()
        {
            return _connection;
        }

        public Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_connection);
        }
    }

    private sealed class ThrowingTransactionConnection : IDbConnection
    {
        public ThrowingTransaction Transaction { get; } = new();

        public int DisposeCount { get; private set; }

#if NET8_0_OR_GREATER
        [AllowNull]
#endif
        public string ConnectionString { get; set; } = string.Empty;

        public int ConnectionTimeout => 0;

        public string Database => "test";

        public ConnectionState State { get; private set; } = ConnectionState.Open;

        public IDbTransaction BeginTransaction()
        {
            return Transaction;
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return Transaction;
        }

        public void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        public void Close()
        {
            State = ConnectionState.Closed;
        }

        public IDbCommand CreateCommand()
        {
            throw new NotSupportedException();
        }

        public void Open()
        {
            State = ConnectionState.Open;
        }

        public void Dispose()
        {
            DisposeCount++;
            State = ConnectionState.Closed;
        }
    }

    private sealed class ThrowingTransaction : IDbTransaction
    {
        public int RollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IDbConnection? Connection => null;

        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        public void Commit()
        {
            throw new NotSupportedException();
        }

        public void Rollback()
        {
            RollbackCount++;
            throw new InvalidOperationException("Expected rollback failure.");
        }

        public void Dispose()
        {
            DisposeCount++;
            throw new InvalidOperationException("Expected transaction disposal failure.");
        }
    }
}
