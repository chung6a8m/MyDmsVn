using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Application
{
    public interface IAsyncDelay
    {
        Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken);
    }

    public sealed class SystemAsyncDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            return Task.Delay(interval, cancellationToken);
        }
    }
}
