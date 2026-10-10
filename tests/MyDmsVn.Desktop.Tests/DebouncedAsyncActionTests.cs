using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DebouncedAsyncActionTests
    {
        [Fact]
        public async Task Rapid_schedules_execute_only_the_final_operation()
        {
            var delay = new ControllableDelay();
            using (var debouncer = new DebouncedAsyncAction(delay, TimeSpan.FromMilliseconds(300)))
            {
                var executed = new List<string>();

                var first = debouncer.Schedule(_ =>
                {
                    executed.Add("first");
                    return Task.CompletedTask;
                });
                var second = debouncer.Schedule(_ =>
                {
                    executed.Add("second");
                    return Task.CompletedTask;
                });

                delay.ReleaseLatest();
                await Task.WhenAll(first, second);

                Assert.Equal(new[] { "second" }, executed);
                Assert.Equal(TimeSpan.FromMilliseconds(300), delay.LatestInterval);
            }
        }

        [Fact]
        public async Task Cancel_prevents_the_pending_operation()
        {
            var delay = new ControllableDelay();
            using (var debouncer = new DebouncedAsyncAction(delay, TimeSpan.FromMilliseconds(300)))
            {
                var executions = 0;
                var pending = debouncer.Schedule(_ =>
                {
                    executions++;
                    return Task.CompletedTask;
                });

                debouncer.Cancel();
                delay.ReleaseLatest();
                await pending;

                Assert.Equal(0, executions);
            }
        }

        [Fact]
        public async Task Dispose_prevents_delayed_work_and_rejects_new_schedules()
        {
            var delay = new ControllableDelay();
            var debouncer = new DebouncedAsyncAction(delay, TimeSpan.FromMilliseconds(300));
            var executions = 0;
            var pending = debouncer.Schedule(_ =>
            {
                executions++;
                return Task.CompletedTask;
            });

            debouncer.Dispose();
            delay.ReleaseLatest();
            await pending;

            Assert.Equal(0, executions);
            var exception = Record.Exception(
                (Action)(() => debouncer.Schedule(_ => Task.CompletedTask)));
            Assert.IsType<ObjectDisposedException>(exception);
        }

        private sealed class ControllableDelay : IAsyncDelay
        {
            private readonly List<PendingDelay> _pending = new List<PendingDelay>();

            public TimeSpan LatestInterval { get; private set; }

            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
            {
                LatestInterval = interval;
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var registration = cancellationToken.Register(() => completion.TrySetCanceled());
                _pending.Add(new PendingDelay(completion, registration));
                return completion.Task;
            }

            public void ReleaseLatest()
            {
                if (_pending.Count == 0)
                {
                    throw new InvalidOperationException("No pending delay exists.");
                }

                _pending[_pending.Count - 1].Release();
            }

            private sealed class PendingDelay
            {
                private readonly TaskCompletionSource<bool> _completion;
                private readonly CancellationTokenRegistration _registration;

                public PendingDelay(
                    TaskCompletionSource<bool> completion,
                    CancellationTokenRegistration registration)
                {
                    _completion = completion;
                    _registration = registration;
                }

                public void Release()
                {
                    _registration.Dispose();
                    _completion.TrySetResult(true);
                }
            }
        }
    }
}
