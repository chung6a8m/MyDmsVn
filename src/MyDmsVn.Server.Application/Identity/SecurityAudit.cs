using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Identity
{
    public enum SecurityAuditAction
    {
        Authentication = 0,
        UserChanged = 1,
        RoleChanged = 2,
        PermissionChanged = 3,
        GoodsReceiptPost = 4,
    }

    public enum SecurityAuditOutcome
    {
        Succeeded = 0,
        Failed = 1,
        Disabled = 2,
        RehashFailed = 3,
        Denied = 4,
    }

    public sealed class SecurityAuditEntry
    {
        public SecurityAuditEntry(
            SecurityAuditAction action,
            SecurityAuditOutcome outcome,
            int? userId,
            DateTime occurredAtUtc,
            string? documentType = null,
            string? documentId = null,
            string? correlationId = null)
        {
            if (occurredAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Audit timestamps must be UTC.", nameof(occurredAtUtc));
            }

            Action = action;
            Outcome = outcome;
            UserId = userId;
            OccurredAtUtc = occurredAtUtc;
            DocumentType = documentType;
            DocumentId = documentId;
            CorrelationId = correlationId;
        }

        public SecurityAuditAction Action { get; }
        public SecurityAuditOutcome Outcome { get; }
        public int? UserId { get; }
        public DateTime OccurredAtUtc { get; }
        public string? DocumentType { get; }
        public string? DocumentId { get; }
        public string? CorrelationId { get; }
    }

    public interface ISecurityAuditSink
    {
        Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken);
    }

    public interface ISecurityAuditFailureReporter
    {
        void Report(
            SecurityAuditEntry entry,
            Exception exception,
            bool businessOperationCommitted);
    }

    public interface ISecurityAuditWriter
    {
        Task WriteAsync(
            SecurityAuditEntry entry,
            bool businessOperationCommitted,
            CancellationToken cancellationToken);
    }

    public interface ISecurityAuditedRequest
    {
        SecurityAuditAction AuditAction { get; }
        string? AuditDocumentType { get; }
        string? AuditDocumentId { get; }
    }

    public interface IUtcClock
    {
        DateTime UtcNow { get; }
    }

    internal sealed class SystemUtcClock : IUtcClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    internal sealed class NoOpSecurityAuditSink : ISecurityAuditSink
    {
        public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    internal sealed class TraceSecurityAuditFailureReporter : ISecurityAuditFailureReporter
    {
        public void Report(
            SecurityAuditEntry entry,
            Exception exception,
            bool businessOperationCommitted)
        {
            System.Diagnostics.Trace.TraceError(
                "Security audit write failed for {0}; committed={1}; exception={2}",
                entry.Action,
                businessOperationCommitted,
                exception.GetType().FullName);
        }
    }

    internal sealed class SecurityAuditWriter : ISecurityAuditWriter
    {
        private readonly ISecurityAuditSink _sink;
        private readonly ISecurityAuditFailureReporter _failureReporter;
        private readonly IRequestCorrelationAccessor _correlationAccessor;

        public SecurityAuditWriter(
            ISecurityAuditSink sink,
            ISecurityAuditFailureReporter failureReporter,
            IRequestCorrelationAccessor correlationAccessor)
        {
            _sink = sink;
            _failureReporter = failureReporter;
            _correlationAccessor = correlationAccessor;
        }

        public async Task WriteAsync(
            SecurityAuditEntry entry,
            bool businessOperationCommitted,
            CancellationToken cancellationToken)
        {
            var enrichedEntry = string.IsNullOrWhiteSpace(entry.CorrelationId) &&
                !string.IsNullOrWhiteSpace(_correlationAccessor.CorrelationId)
                ? new SecurityAuditEntry(
                    entry.Action,
                    entry.Outcome,
                    entry.UserId,
                    entry.OccurredAtUtc,
                    entry.DocumentType,
                    entry.DocumentId,
                    _correlationAccessor.CorrelationId)
                : entry;
            try
            {
                await _sink.WriteAsync(enrichedEntry, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                try
                {
                    _failureReporter.Report(
                        enrichedEntry,
                        exception,
                        businessOperationCommitted);
                }
                catch (Exception reporterException)
                {
                    System.Diagnostics.Trace.TraceError(
                        "Security audit failure reporter failed for {0}; committed={1}; exception={2}",
                        enrichedEntry.Action,
                        businessOperationCommitted,
                        reporterException.GetType().FullName);
                }
            }
        }
    }
}
