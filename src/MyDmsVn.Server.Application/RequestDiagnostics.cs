using System;

namespace MyDmsVn.Server.Application
{
    public interface IRequestCorrelationAccessor
    {
        string? CorrelationId { get; }
    }

    public interface IRequestDiagnosticsScope : IDisposable
    {
        string CorrelationId { get; }

        void RecordError(string errorCode);
    }

    public interface IRequestDiagnosticsScopeFactory
    {
        IRequestDiagnosticsScope Begin(string operationName);
    }

    internal sealed class EmptyRequestCorrelationAccessor : IRequestCorrelationAccessor
    {
        public string? CorrelationId => null;
    }
}
