using System;
using System.Diagnostics;

namespace MyDmsVn.Server.Application
{
    public interface IApplicationExceptionReporter
    {
        void Report(Type requestType, Exception exception);
    }

    internal sealed class TraceApplicationExceptionReporter : IApplicationExceptionReporter
    {
        public void Report(Type requestType, Exception exception)
        {
            Trace.TraceError(
                "Unhandled application request {0}: {1}",
                requestType.FullName,
                exception);
        }
    }
}
