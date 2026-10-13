using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace MyDmsVn.Server.Infrastructure.Diagnostics
{
    public static class DesktopProductionDiagnosticsRegistration
    {
        public static IServiceCollection AddDesktopProductionDiagnostics(
            this IServiceCollection services,
            DesktopDiagnosticsOptions options)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            services.RemoveAll<IRequestDiagnosticsScopeFactory>();
            services.RemoveAll<IRequestCorrelationAccessor>();
            services.RemoveAll<IApplicationExceptionReporter>();
            services.RemoveAll<ISecurityAuditSink>();
            services.RemoveAll<ISecurityAuditFailureReporter>();
            services.AddSingleton(options);
            services.AddSingleton<DesktopDiagnosticLoggers>();
            services.AddSingleton<IDiagnosticsStartupStatus>(
                provider => provider.GetRequiredService<DesktopDiagnosticLoggers>());
            services.AddSingleton<SerilogRequestDiagnostics>();
            services.AddSingleton<IRequestDiagnosticsScopeFactory>(
                provider => provider.GetRequiredService<SerilogRequestDiagnostics>());
            services.AddSingleton<IRequestCorrelationAccessor>(
                provider => provider.GetRequiredService<SerilogRequestDiagnostics>());
            services.AddSingleton<IApplicationExceptionReporter, SerilogApplicationExceptionReporter>();
            services.AddSingleton<ISecurityAuditSink, SerilogSecurityAuditSink>();
            services.AddSingleton<ISecurityAuditFailureReporter, SerilogSecurityAuditFailureReporter>();
            return services;
        }
    }

    internal sealed class DesktopDiagnosticLoggers : IDiagnosticsStartupStatus, IDisposable
    {
        public DesktopDiagnosticLoggers(DesktopDiagnosticsOptions options)
        {
            try
            {
                Directory.CreateDirectory(options.LogDirectory);
                Operational = CreateLogger(
                    options,
                    Path.Combine(options.LogDirectory, "application-.json"));
                Audit = CreateLogger(
                    options,
                    Path.Combine(options.LogDirectory, "audit-.json"));
                FileLoggingEnabled = true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                SafeErrorCode = "Diagnostics.FileSinkUnavailable";
                Operational = CreateFallbackLogger(options);
                Audit = CreateFallbackLogger(options);
                Trace.TraceError(
                    "{0}: {1}",
                    SafeErrorCode,
                    SensitiveDataRedactor.ToSafeDiagnostic(exception));
            }

            Serilog.Debugging.SelfLog.Enable(
                message => Trace.TraceError(SensitiveDataRedactor.Redact(message)));
        }

        public ILogger Operational { get; }
        public ILogger Audit { get; }
        public bool FileLoggingEnabled { get; }
        public string? SafeErrorCode { get; }

        public void Dispose()
        {
            (Operational as IDisposable)?.Dispose();
            if (!ReferenceEquals(Operational, Audit))
            {
                (Audit as IDisposable)?.Dispose();
            }
        }

        private static ILogger CreateLogger(
            DesktopDiagnosticsOptions options,
            string path)
        {
            return ConfigureBase(options)
                .WriteTo.File(
                    new JsonFormatter(renderMessage: true),
                    path,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.RetainedFileCountLimit,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();
        }

        private static ILogger CreateFallbackLogger(DesktopDiagnosticsOptions options)
        {
            return ConfigureBase(options)
                .WriteTo.Sink(new RedactedTraceLogEventSink())
                .CreateLogger();
        }

        private static LoggerConfiguration ConfigureBase(DesktopDiagnosticsOptions options)
        {
            var assembly = typeof(DesktopDiagnosticLoggers).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationVersion", version)
                .Enrich.WithProperty("Host", options.HostName)
                .Enrich.WithProperty("Runtime", Environment.Version.ToString());
        }
    }

    internal sealed class SerilogRequestDiagnostics
        : IRequestDiagnosticsScopeFactory, IRequestCorrelationAccessor
    {
        private readonly AsyncLocal<Scope?> _current = new AsyncLocal<Scope?>();
        private readonly DesktopDiagnosticLoggers _loggers;

        public SerilogRequestDiagnostics(DesktopDiagnosticLoggers loggers) =>
            _loggers = loggers;

        public string? CorrelationId => _current.Value?.CorrelationId;

        public IRequestDiagnosticsScope Begin(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("An operation name is required.", nameof(operationName));
            }

            var correlationId = Guid.NewGuid().ToString("N");
            var properties = new IDisposable[]
            {
                LogContext.PushProperty("CorrelationId", correlationId),
                LogContext.PushProperty("OperationName", operationName),
            };
            var scope = new Scope(
                this,
                _current.Value,
                correlationId,
                operationName,
                properties,
                _loggers.Operational);
            _current.Value = scope;
            _loggers.Operational.Information("Application operation started");
            return scope;
        }

        private sealed class Scope : IRequestDiagnosticsScope
        {
            private readonly SerilogRequestDiagnostics _owner;
            private readonly Scope? _parent;
            private readonly string _operationName;
            private readonly IDisposable[] _properties;
            private readonly ILogger _logger;
            private string? _errorCode;
            private bool _disposed;

            public Scope(
                SerilogRequestDiagnostics owner,
                Scope? parent,
                string correlationId,
                string operationName,
                IDisposable[] properties,
                ILogger logger)
            {
                _owner = owner;
                _parent = parent;
                CorrelationId = correlationId;
                _operationName = operationName;
                _properties = properties;
                _logger = logger;
            }

            public string CorrelationId { get; }

            public void RecordError(string errorCode) => _errorCode = errorCode;

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _logger.Information(
                    "Application operation completed with {ErrorCode}",
                    _errorCode ?? "None");
                if (ReferenceEquals(_owner._current.Value, this))
                {
                    _owner._current.Value = _parent;
                }

                for (var index = _properties.Length - 1; index >= 0; index--)
                {
                    _properties[index].Dispose();
                }
            }
        }
    }

    internal sealed class SerilogSecurityAuditSink : ISecurityAuditSink
    {
        private readonly ILogger _logger;

        public SerilogSecurityAuditSink(DesktopDiagnosticLoggers loggers) =>
            _logger = loggers.Audit;

        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.Information(
                "Security audit {Action} {Outcome} actor {ActorUserId} document {DocumentType}/{DocumentId} at {OccurredAtUtc} correlation {CorrelationId}",
                entry.Action,
                entry.Outcome,
                entry.UserId,
                entry.DocumentType,
                entry.DocumentId,
                entry.OccurredAtUtc,
                entry.CorrelationId);
            return Task.CompletedTask;
        }
    }

    internal sealed class SerilogApplicationExceptionReporter : IApplicationExceptionReporter
    {
        private readonly ILogger _logger;

        public SerilogApplicationExceptionReporter(DesktopDiagnosticLoggers loggers) =>
            _logger = loggers.Operational;

        public void Report(Type requestType, Exception exception)
        {
            _logger.Error(
                "Unhandled application request {OperationName}: {SafeDiagnostic}",
                requestType.Name,
                SensitiveDataRedactor.ToSafeDiagnostic(exception));
        }
    }

    internal sealed class SerilogSecurityAuditFailureReporter : ISecurityAuditFailureReporter
    {
        private readonly ILogger _logger;

        public SerilogSecurityAuditFailureReporter(DesktopDiagnosticLoggers loggers) =>
            _logger = loggers.Operational;

        public void Report(
            SecurityAuditEntry entry,
            Exception exception,
            bool businessOperationCommitted)
        {
            _logger.Error(
                "Security audit write failed for {Action}; BusinessOperationCommitted={BusinessOperationCommitted}; {SafeDiagnostic}",
                entry.Action,
                businessOperationCommitted,
                SensitiveDataRedactor.ToSafeDiagnostic(exception));
        }
    }

    internal sealed class RedactedTraceLogEventSink : ILogEventSink
    {
        private readonly JsonFormatter _formatter = new JsonFormatter(renderMessage: true);

        public void Emit(LogEvent logEvent)
        {
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                _formatter.Format(logEvent, writer);
                Trace.TraceError(SensitiveDataRedactor.Redact(writer.ToString()));
            }
        }
    }
}
