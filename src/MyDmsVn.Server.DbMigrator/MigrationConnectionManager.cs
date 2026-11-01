using System;
using System.Collections.Generic;
using System.Data;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;

namespace MyDmsVn.Server.DbMigrator;

// DbUp 6.1.1 disposes its physical connection at the end of OperationStarting. SQL server
// disconnect cleanup can lag the next zero-timeout lock admission. Explicitly release the
// session lock first, on the same still-open connection, after per-script transactions end.
internal sealed class MigrationConnectionManager : IConnectionManager
{
    private readonly IConnectionManager _inner;
    private readonly Action _release;
    public MigrationConnectionManager(IConnectionManager inner, Action release)
    {
        _inner = inner;
        _release = release;
    }
    public IDisposable OperationStarting(IUpgradeLog log, List<SqlScript> scripts)
        => new Operation(_inner.OperationStarting(log, scripts), _release);
    public void ExecuteCommandsWithManagedConnection(Action<Func<IDbCommand>> action) => _inner.ExecuteCommandsWithManagedConnection(action);
    public T ExecuteCommandsWithManagedConnection<T>(Func<Func<IDbCommand>, T> action) => _inner.ExecuteCommandsWithManagedConnection(action);
    public TransactionMode TransactionMode
    {
        get => _inner.TransactionMode; set => _inner.TransactionMode = value;
    }
    public bool IsScriptOutputLogged
    {
        get => _inner.IsScriptOutputLogged; set => _inner.IsScriptOutputLogged = value;
    }
    public int? ExecutionTimeoutSeconds
    {
        get => _inner.ExecutionTimeoutSeconds; set => _inner.ExecutionTimeoutSeconds = value;
    }
    public IEnumerable<string> SplitScriptIntoCommands(string contents) => _inner.SplitScriptIntoCommands(contents);
    public bool TryConnect(IUpgradeLog log, out string error) => _inner.TryConnect(log, out error);
    private sealed class Operation : IDisposable
    {
        private readonly IDisposable _scope;
        private readonly Action _release;
        private bool _disposed;
        public Operation(IDisposable scope, Action release)
        {
            _scope = scope;
            _release = release;
        }
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Exception? failure = null;
            try
            {
                _release();
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                try
                {
                    _scope.Dispose();
                }
                catch (Exception cleanup) when (failure != null) { throw new AggregateException(failure, cleanup); }
            }
        }
    }
}
