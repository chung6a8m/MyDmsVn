using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Operations;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.CrashHost;

internal sealed class CommitBarrier
{
    private readonly ControlChannel _channel;
    private readonly string _phase;
    private readonly CrashMutation? _mutation;
    private int _armed;
    private int _claimed;

    internal CommitBarrier(ControlChannel channel, CrashRequest request)
    { _channel = channel; _phase = request.Phase; _mutation = request.Mutation; }
    internal void Arm() => Interlocked.Exchange(ref _armed, 1);

    private bool Matches(WebOperationContext? context) => context != null && _mutation != null &&
        context.OperationId == _mutation.OperationId && context.DeploymentEpoch == _mutation.DeploymentEpoch &&
        context.Kind == _mutation.Kind && context.RequestTargetId == _mutation.RequestTargetId;

    private void Commit(IUnitOfWork inner, WebOperationContext? completed)
    {
        // A replay, status read or unrelated transaction has no matching successful
        // journal completion on THIS UoW and must never masquerade as the selected barrier.
        if (_phase == "none" || Volatile.Read(ref _armed) == 0 || !Matches(completed) || Interlocked.CompareExchange(ref _claimed, 1, 0) != 0)
        { inner.Commit(); return; }
        // This is the actual UoW connection, not the latest authentication/query connection.
        var sqlSessionId = ((SqlConnection)((ISqlExecutionContext)inner).Connection).ServerProcessId;
        if (_phase == "before-commit") Wait("before-commit", sqlSessionId);
        if (_phase == "commit-window")
        {
            Wait("commit-ready", sqlSessionId);
            Emit("commit-enter", sqlSessionId);
        }
        inner.Commit();
        if (_phase == "after-commit") Wait("after-commit", sqlSessionId);
        // A fast Commit remains unacknowledged until the supervisor kills or explicitly releases it.
        if (_phase == "commit-window") Wait("commit-return", sqlSessionId);
    }

    private void Emit(string name, int sqlSessionId) => _channel.SendAsync(new { @event = name, sqlSessionId }).GetAwaiter().GetResult();
    private void Wait(string name, int sqlSessionId)
    { Emit(name, sqlSessionId); _channel.ExpectAsync("release").GetAwaiter().GetResult(); }

    internal static void Decorate(IServiceCollection services, CommitBarrier barrier)
    {
        var descriptor = services.Single(item => item.ServiceType == typeof(IUnitOfWorkFactory));
        // Preserve the production scoped factory and its registered repository factories.
        // Reconstructing SqlUnitOfWorkFactory here would silently lose that graph.
        if (descriptor.ImplementationFactory == null || descriptor.Lifetime != ServiceLifetime.Scoped) throw new InvalidOperationException();
        services.Remove(descriptor);
        services.Add(new ServiceDescriptor(typeof(IUnitOfWorkFactory), provider =>
            new BarrierFactory((IUnitOfWorkFactory)descriptor.ImplementationFactory(provider), barrier), descriptor.Lifetime));
    }

    private sealed class BarrierFactory : IUnitOfWorkFactory
    {
        private readonly IUnitOfWorkFactory _inner;
        private readonly CommitBarrier _barrier;
        internal BarrierFactory(IUnitOfWorkFactory inner, CommitBarrier barrier) { _inner = inner; _barrier = barrier; }
        public IUnitOfWork Create() => new BarrierUnitOfWork(_inner.Create(), _barrier);
        public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) =>
            new BarrierUnitOfWork(await _inner.CreateAsync(cancellationToken).ConfigureAwait(false), _barrier);
    }

    private sealed class BarrierUnitOfWork : IUnitOfWork, ISqlExecutionContext
    {
        private readonly IUnitOfWork _inner;
        private readonly CommitBarrier _barrier;
        private IWebOperationJournal? _journal;
        private WebOperationContext? _completed;
        internal BarrierUnitOfWork(IUnitOfWork inner, CommitBarrier barrier) { _inner = inner; _barrier = barrier; }
        public UnitOfWorkState State => _inner.State;
        public IDbConnection Connection => ((ISqlExecutionContext)_inner).Connection;
        public IDbTransaction? Transaction => ((ISqlExecutionContext)_inner).Transaction;
        public void BeginTransaction() => _inner.BeginTransaction();
        public TRepository Repository<TRepository>() where TRepository : class
        {
            if (typeof(TRepository) != typeof(IWebOperationJournal)) return _inner.Repository<TRepository>();
            _journal ??= new CompletionJournal(_inner.Repository<IWebOperationJournal>(), this);
            return (TRepository)(object)_journal;
        }
        public void Commit() => _barrier.Commit(_inner, _completed);
        private void Completed(WebOperationContext context)
        {
            if (!_barrier.Matches(context)) return;
            if (_completed != null) throw new InvalidOperationException();
            _completed = context;
        }
        public void Rollback() => _inner.Rollback();
        public void Dispose() => _inner.Dispose();

        private sealed class CompletionJournal : IWebOperationJournal
        {
            private readonly IWebOperationJournal _inner;
            private readonly BarrierUnitOfWork _owner;
            internal CompletionJournal(IWebOperationJournal inner, BarrierUnitOfWork owner) { _inner = inner; _owner = owner; }
            public Task<WebOperationReplay?> AdmitAsync(WebOperationContext context, int actorUserId, CancellationToken token) =>
                _inner.AdmitAsync(context, actorUserId, token);
            public async Task CompleteAsync(WebOperationContext context, int actorUserId, long resultDocumentId, byte[] bytes, CancellationToken token)
            {
                await _inner.CompleteAsync(context, actorUserId, resultDocumentId, bytes, token).ConfigureAwait(false);
                _owner.Completed(context);
            }
        }
    }
}
