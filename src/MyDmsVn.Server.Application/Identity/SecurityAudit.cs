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
            DateTime occurredAtUtc)
        {
            if (occurredAtUtc.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Audit timestamps must be UTC.", nameof(occurredAtUtc));
            }

            Action = action;
            Outcome = outcome;
            UserId = userId;
            OccurredAtUtc = occurredAtUtc;
        }

        public SecurityAuditAction Action { get; }
        public SecurityAuditOutcome Outcome { get; }
        public int? UserId { get; }
        public DateTime OccurredAtUtc { get; }
    }

    public interface ISecurityAuditSink
    {
        Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken);
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
}
