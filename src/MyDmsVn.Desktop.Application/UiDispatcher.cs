using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Application
{
    public interface IUiDispatcher
    {
        Task InvokeAsync(Action action, CancellationToken cancellationToken);
    }

    public sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }
    }

    public sealed class SynchronizationContextUiDispatcher : IUiDispatcher
    {
        private readonly SynchronizationContext _context;

        public SynchronizationContextUiDispatcher(SynchronizationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task InvokeAsync(Action action, CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (ReferenceEquals(SynchronizationContext.Current, _context))
            {
                action();
                return Task.CompletedTask;
            }

            var state = new DispatchState(action, cancellationToken);
            _context.Post(value => ((DispatchState)value!).Run(), state);
            state.RegisterCancellation();
            return state.Completion;
        }

        private sealed class DispatchState
        {
            private readonly Action _action;
            private readonly CancellationToken _cancellationToken;
            private readonly TaskCompletionSource<bool> _completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration _registration;
            private int _completed;

            public DispatchState(Action action, CancellationToken cancellationToken)
            {
                _action = action;
                _cancellationToken = cancellationToken;
            }

            public Task Completion => _completion.Task;

            public void RegisterCancellation()
            {
                _registration = _cancellationToken.Register(
                    () => Complete(() => _completion.TrySetCanceled(_cancellationToken)));
                if (Volatile.Read(ref _completed) != 0)
                {
                    _registration.Dispose();
                }
            }

            public void Run()
            {
                if (_cancellationToken.IsCancellationRequested)
                {
                    Complete(() => _completion.TrySetCanceled(_cancellationToken));
                    return;
                }

                try
                {
                    _action();
                    Complete(() => _completion.TrySetResult(true));
                }
                catch (Exception exception)
                {
                    Complete(() => _completion.TrySetException(exception));
                }
            }

            private void Complete(Action completion)
            {
                if (Interlocked.Exchange(ref _completed, 1) != 0)
                {
                    return;
                }

                completion();
                _registration.Dispose();
            }
        }
    }
}
