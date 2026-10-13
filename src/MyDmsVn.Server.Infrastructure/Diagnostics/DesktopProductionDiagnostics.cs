using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
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
using Serilog.Parsing;

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
        private static readonly MessageTemplate AuditMessageTemplate =
            new MessageTemplateParser().Parse(
                "Security audit {Action} {Outcome} actor {ActorUserId} document {DocumentType}/{DocumentId} at {OccurredAtUtc} correlation {CorrelationId}");
        private readonly object _auditSync = new object();
        private readonly DesktopDiagnosticsOptions _options;
        private readonly JsonFormatter _auditFormatter = new JsonFormatter(renderMessage: true);
        private bool _fileLoggingEnabled;
        private string? _safeErrorCode;

        public DesktopDiagnosticLoggers(DesktopDiagnosticsOptions options)
        {
            _options = options;
            try
            {
                Directory.CreateDirectory(options.LogDirectory);
                Operational = CreateLogger(
                    options,
                    Path.Combine(options.LogDirectory, "application-.json"));
                _fileLoggingEnabled = true;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                _safeErrorCode = "Diagnostics.FileSinkUnavailable";
                Operational = CreateFallbackLogger(options);
                Trace.TraceError(
                    "{0}: {1}",
                    _safeErrorCode,
                    SensitiveDataRedactor.ToSafeDiagnostic(exception));
            }

            Serilog.Debugging.SelfLog.Enable(
                message => Trace.TraceError(SensitiveDataRedactor.Redact(message)));
        }

        public ILogger Operational { get; }
        public bool FileLoggingEnabled => _fileLoggingEnabled;
        public string? SafeErrorCode => _safeErrorCode;

        public void Dispose()
        {
            (Operational as IDisposable)?.Dispose();
        }

        public void WriteAudit(SecurityAuditEntry entry)
        {
            if (!_fileLoggingEnabled)
            {
                TraceAuditFallback(entry);
                throw new IOException("The security audit file sink is unavailable.");
            }

            try
            {
                var auditPath = Path.Combine(
                    _options.LogDirectory,
                    "audit-" + entry.OccurredAtUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".json");
                var payload = FormatAudit(entry);
                var bytes = new UTF8Encoding(false).GetBytes(payload);
                lock (_auditSync)
                {
                    using (var stream = new FileStream(
                        auditPath,
                        FileMode.Append,
                        FileAccess.Write,
                        FileShare.Read))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(flushToDisk: true);
                    }

                    TryEnforceAuditRetention();
                }
            }
            catch (IOException exception) when (IsTransientFileContention(exception))
            {
                Trace.TraceWarning(
                    "{0}: {1}",
                    "Diagnostics.AuditFileTemporarilyUnavailable",
                    SensitiveDataRedactor.ToSafeDiagnostic(exception));
                TraceAuditFallback(entry);
                throw;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                _safeErrorCode = "Diagnostics.AuditFileSinkUnavailable";
                _fileLoggingEnabled = false;
                Trace.TraceError(
                    "{0}: {1}",
                    _safeErrorCode,
                    SensitiveDataRedactor.ToSafeDiagnostic(exception));
                TraceAuditFallback(entry);
                throw;
            }
        }

        private static bool IsTransientFileContention(IOException exception)
        {
            const int ErrorSharingViolation = 32;
            const int ErrorLockViolation = 33;
            var nativeErrorCode = exception.HResult & 0xFFFF;
            return nativeErrorCode == ErrorSharingViolation ||
                nativeErrorCode == ErrorLockViolation;
        }

        private string FormatAudit(SecurityAuditEntry entry)
        {
            var properties = new List<LogEventProperty>
            {
                new LogEventProperty("Action", new ScalarValue(entry.Action.ToString())),
                new LogEventProperty("Outcome", new ScalarValue(entry.Outcome.ToString())),
                new LogEventProperty("ActorUserId", new ScalarValue(entry.UserId)),
                new LogEventProperty("DocumentType", new ScalarValue(entry.DocumentType)),
                new LogEventProperty("DocumentId", new ScalarValue(entry.DocumentId)),
                new LogEventProperty("OccurredAtUtc", new ScalarValue(entry.OccurredAtUtc)),
                new LogEventProperty("CorrelationId", new ScalarValue(entry.CorrelationId)),
                new LogEventProperty("ApplicationVersion", new ScalarValue(GetApplicationVersion())),
                new LogEventProperty("Host", new ScalarValue(_options.HostName)),
                new LogEventProperty("Runtime", new ScalarValue(Environment.Version.ToString())),
            };
            var logEvent = new LogEvent(
                new DateTimeOffset(entry.OccurredAtUtc),
                LogEventLevel.Information,
                null,
                AuditMessageTemplate,
                properties);
            using (var writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                _auditFormatter.Format(logEvent, writer);
                return writer.ToString();
            }
        }

        private void EnforceAuditRetention()
        {
            var files = Directory.GetFiles(_options.LogDirectory, "audit-*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            var removeCount = files.Length - _options.RetainedFileCountLimit;
            for (var index = 0; index < removeCount; index++)
            {
                File.Delete(files[index]);
            }
        }

        private void TryEnforceAuditRetention()
        {
            try
            {
                EnforceAuditRetention();
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is NotSupportedException)
            {
                Trace.TraceWarning(
                    "{0}: {1}",
                    "Diagnostics.AuditRetentionCleanupFailed",
                    SensitiveDataRedactor.ToSafeDiagnostic(exception));
            }
        }

        private static void TraceAuditFallback(SecurityAuditEntry entry)
        {
            Trace.TraceError(
                "Security audit fallback Action={0}; Outcome={1}; ActorUserId={2}; DocumentType={3}; DocumentId={4}; OccurredAtUtc={5:o}; CorrelationId={6}",
                entry.Action,
                entry.Outcome,
                entry.UserId,
                entry.DocumentType,
                entry.DocumentId,
                entry.OccurredAtUtc,
                entry.CorrelationId);
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
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationVersion", GetApplicationVersion())
                .Enrich.WithProperty("Host", options.HostName)
                .Enrich.WithProperty("Runtime", Environment.Version.ToString());
        }

        private static string GetApplicationVersion()
        {
            var assembly = typeof(DesktopDiagnosticLoggers).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
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
        private readonly DesktopDiagnosticLoggers _loggers;

        public SerilogSecurityAuditSink(DesktopDiagnosticLoggers loggers) =>
            _loggers = loggers;

        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _loggers.WriteAudit(entry);
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
