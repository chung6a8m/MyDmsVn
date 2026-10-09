using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class UiDispatcherTests
    {
        [Fact]
        public async Task Immediate_dispatcher_executes_action_synchronously()
        {
            var dispatcher = new ImmediateUiDispatcher();
            var executed = false;

            await dispatcher.InvokeAsync(() => executed = true, CancellationToken.None);

            Assert.True(executed);
        }

        [Fact]
        public async Task Synchronization_context_dispatcher_posts_to_the_captured_context()
        {
            var context = new QueuedSynchronizationContext();
            var dispatcher = new SynchronizationContextUiDispatcher(context);
            var executed = false;

            var pending = dispatcher.InvokeAsync(() => executed = true, CancellationToken.None);
            Assert.False(executed);

            context.RunNext();
            await pending;

            Assert.True(executed);
        }

        [Fact]
        public async Task Synchronization_context_dispatcher_skips_action_canceled_before_dispatch()
        {
            var context = new QueuedSynchronizationContext();
            var dispatcher = new SynchronizationContextUiDispatcher(context);
            var executed = false;
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = dispatcher.InvokeAsync(() => executed = true, cancellation.Token);
                cancellation.Cancel();
                context.RunNext();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                Assert.False(executed);
            }
        }

        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly Queue<Action> _callbacks = new Queue<Action>();

            public override void Post(SendOrPostCallback callback, object? state)
            {
                _callbacks.Enqueue(() => callback(state));
            }

            public void RunNext()
            {
                if (_callbacks.Count == 0)
                {
                    throw new InvalidOperationException("No callback is queued.");
                }

                _callbacks.Dequeue()();
            }
        }
    }
}
