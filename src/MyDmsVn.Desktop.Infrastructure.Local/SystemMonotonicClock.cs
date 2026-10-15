using System;
using System.Diagnostics;
using MyDmsVn.Desktop.Application;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal sealed class SystemMonotonicClock : IMonotonicClock
    {
        private readonly long _origin = Stopwatch.GetTimestamp();

        public TimeSpan Elapsed => TimeSpan.FromSeconds(
            (Stopwatch.GetTimestamp() - _origin) / (double)Stopwatch.Frequency);
    }
}
