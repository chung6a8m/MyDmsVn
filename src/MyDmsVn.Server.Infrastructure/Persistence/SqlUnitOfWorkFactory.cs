using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Persistence;

namespace MyDmsVn.Server.Infrastructure.Persistence;

public sealed class SqlUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IReadOnlyDictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>> _repositoryFactories;
    private readonly RepoDbMappingInitializer? _mappingInitializer;
    private readonly IServiceScopeFactory? _serviceScopeFactory;

    public SqlUnitOfWorkFactory(IDbConnectionFactory connectionFactory)
        : this(
            connectionFactory,
            null,
            null,
            new Dictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>>())
    {
    }

    internal SqlUnitOfWorkFactory(
        IDbConnectionFactory connectionFactory,
        RepoDbMappingInitializer? mappingInitializer,
        IServiceScopeFactory? serviceScopeFactory,
        IReadOnlyDictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>> repositoryFactories)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _mappingInitializer = mappingInitializer;
        _serviceScopeFactory = serviceScopeFactory;
        _repositoryFactories = repositoryFactories ?? throw new ArgumentNullException(nameof(repositoryFactories));
    }

    public IUnitOfWork Create()
    {
        _mappingInitializer?.Initialize();
        var scope = _serviceScopeFactory?.CreateScope();
        try
        {
            return new SqlUnitOfWork(_connectionFactory.OpenConnection(), scope, _repositoryFactories);
        }
        catch
        {
            scope?.Dispose();
            throw;
        }
    }

    public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken)
    {
        _mappingInitializer?.Initialize();
        var scope = _serviceScopeFactory?.CreateScope();
        try
        {
            var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            return new SqlUnitOfWork(connection, scope, _repositoryFactories);
        }
        catch
        {
            scope?.Dispose();
            throw;
        }
    }
}

internal sealed class SqlUnitOfWork : IUnitOfWork, ISqlExecutionContext
{
    private readonly IDbConnection _connection;
    private readonly IServiceScope? _serviceScope;
    private readonly IReadOnlyDictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>> _repositoryFactories;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbTransaction? _transaction;

    public SqlUnitOfWork(
        IDbConnection connection,
        IServiceScope? serviceScope,
        IReadOnlyDictionary<Type, Func<IServiceProvider, ISqlExecutionContext, object>> repositoryFactories)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _serviceScope = serviceScope;
        _repositoryFactories = repositoryFactories ?? throw new ArgumentNullException(nameof(repositoryFactories));

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Dispose();
            throw new ArgumentException("The UnitOfWork connection must already be open.", nameof(connection));
        }
    }

    public UnitOfWorkState State { get; private set; } = UnitOfWorkState.Created;

    IDbConnection ISqlExecutionContext.Connection => GetOpenConnection();

    IDbTransaction? ISqlExecutionContext.Transaction => _transaction;

    public void BeginTransaction()
    {
        EnsureState(UnitOfWorkState.Created, nameof(BeginTransaction));
        _transaction = _connection.BeginTransaction();
        State = UnitOfWorkState.ActiveTransaction;
    }

    public TRepository Repository<TRepository>() where TRepository : class
    {
        EnsureUsable(nameof(Repository));
        var repositoryType = typeof(TRepository);
        if (_repositories.TryGetValue(repositoryType, out var existing))
        {
            return (TRepository)existing;
        }

        if (!_repositoryFactories.TryGetValue(repositoryType, out var factory))
        {
            throw new InvalidOperationException(
                $"Repository interface '{repositoryType.FullName}' is not explicitly registered.");
        }

        var serviceProvider = _serviceScope?.ServiceProvider
            ?? throw new InvalidOperationException("Repository creation requires an owned UnitOfWork service scope.");
        var repository = factory(serviceProvider, this) as TRepository
            ?? throw new InvalidOperationException(
                $"The registered factory did not create '{repositoryType.FullName}'.");
        _repositories.Add(repositoryType, repository);
        return repository;
    }

    public void Commit()
    {
        EnsureState(UnitOfWorkState.ActiveTransaction, nameof(Commit));
        _transaction!.Commit();
        _transaction.Dispose();
        _transaction = null;
        State = UnitOfWorkState.Committed;
    }

    public void Rollback()
    {
        EnsureState(UnitOfWorkState.ActiveTransaction, nameof(Rollback));
        _transaction!.Rollback();
        _transaction.Dispose();
        _transaction = null;
        State = UnitOfWorkState.RolledBack;
    }

    public void Dispose()
    {
        if (State == UnitOfWorkState.Disposed)
        {
            return;
        }

        Exception? cleanupError = null;
        if (State == UnitOfWorkState.ActiveTransaction && _transaction is not null)
        {
            CaptureCleanupFailure(ref cleanupError, _transaction.Rollback);
        }

        foreach (var repository in _repositories.Values.OfType<IDisposable>())
        {
            CaptureCleanupFailure(ref cleanupError, repository.Dispose);
        }

        _repositories.Clear();
        if (_transaction is not null)
        {
            CaptureCleanupFailure(ref cleanupError, _transaction.Dispose);
            _transaction = null;
        }

        if (_serviceScope is not null)
        {
            CaptureCleanupFailure(ref cleanupError, _serviceScope.Dispose);
        }

        CaptureCleanupFailure(ref cleanupError, _connection.Dispose);
        State = UnitOfWorkState.Disposed;

        if (cleanupError is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupError).Throw();
        }
    }

    private IDbConnection GetOpenConnection()
    {
        EnsureUsable(nameof(ISqlExecutionContext.Connection));
        return _connection;
    }

    private void EnsureUsable(string operation)
    {
        if (State == UnitOfWorkState.Disposed)
        {
            throw new ObjectDisposedException(nameof(SqlUnitOfWork));
        }

        if (State == UnitOfWorkState.Committed || State == UnitOfWorkState.RolledBack)
        {
            throw new InvalidOperationException(
                $"Cannot call {operation} after the UnitOfWork has completed with state {State}.");
        }
    }

    private void EnsureState(UnitOfWorkState expected, string operation)
    {
        if (State == UnitOfWorkState.Disposed)
        {
            throw new ObjectDisposedException(nameof(SqlUnitOfWork));
        }

        if (State != expected)
        {
            throw new InvalidOperationException(
                $"Cannot call {operation} while the UnitOfWork is in state {State}; expected {expected}.");
        }
    }

    private static void CaptureCleanupFailure(ref Exception? firstError, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            firstError ??= exception;
        }
    }
}
