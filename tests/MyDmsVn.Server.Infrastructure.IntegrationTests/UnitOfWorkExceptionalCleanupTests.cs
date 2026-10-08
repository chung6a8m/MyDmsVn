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
    public void Commit_records_terminal_state_before_transaction_disposal_throws()
    {
        var connection = new ThrowingTransactionConnection(throwOnRollback: false, throwOnDispose: true);
        var unitOfWork = new SqlUnitOfWorkFactory(new StubConnectionFactory(connection)).Create();
        unitOfWork.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => unitOfWork.Commit());

        Assert.Equal(UnitOfWorkState.Committed, unitOfWork.State);
        Assert.Equal(1, connection.Transaction.CommitCount);
        Assert.Equal(0, connection.Transaction.RollbackCount);
        Assert.Equal(1, connection.Transaction.DisposeCount);
        unitOfWork.Dispose();
        Assert.Equal(0, connection.Transaction.RollbackCount);
        Assert.Equal(1, connection.DisposeCount);
    }

    [Fact]
    public void Rollback_records_terminal_state_before_transaction_disposal_throws()
    {
        var connection = new ThrowingTransactionConnection(throwOnRollback: false, throwOnDispose: true);
        var unitOfWork = new SqlUnitOfWorkFactory(new StubConnectionFactory(connection)).Create();
        unitOfWork.BeginTransaction();

        Assert.Throws<InvalidOperationException>(() => unitOfWork.Rollback());

        Assert.Equal(UnitOfWorkState.RolledBack, unitOfWork.State);
        Assert.Equal(0, connection.Transaction.CommitCount);
        Assert.Equal(1, connection.Transaction.RollbackCount);
        Assert.Equal(1, connection.Transaction.DisposeCount);
        unitOfWork.Dispose();
        Assert.Equal(1, connection.Transaction.RollbackCount);
        Assert.Equal(1, connection.DisposeCount);
    }

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
        public ThrowingTransactionConnection(
            bool throwOnRollback = true,
            bool throwOnDispose = true)
        {
            Transaction = new ThrowingTransaction(throwOnRollback, throwOnDispose);
        }

        public ThrowingTransaction Transaction { get; }

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
        private readonly bool _throwOnRollback;
        private readonly bool _throwOnDispose;

        public ThrowingTransaction(bool throwOnRollback, bool throwOnDispose)
        {
            _throwOnRollback = throwOnRollback;
            _throwOnDispose = throwOnDispose;
        }

        public int CommitCount { get; private set; }

        public int RollbackCount { get; private set; }

        public int DisposeCount { get; private set; }

        public IDbConnection? Connection => null;

        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        public void Commit()
        {
            CommitCount++;
        }

        public void Rollback()
        {
            RollbackCount++;
            if (_throwOnRollback)
            {
                throw new InvalidOperationException("Expected rollback failure.");
            }
        }

        public void Dispose()
        {
            DisposeCount++;
            if (_throwOnDispose)
            {
                throw new InvalidOperationException("Expected transaction disposal failure.");
            }
        }
    }
}
