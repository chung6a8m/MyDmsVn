using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
namespace MyDmsVn.Server.HttpHost;

// Operator TraceSource listeners own destination/retention. Payload is a fixed allowlist.
public sealed class TraceSecurityAuditSink : ISecurityAuditSink, IDisposable
{
    private readonly TraceSource _source;
    public TraceSecurityAuditSink() : this(new TraceSource("MyDmsVn.Web.Security", SourceLevels.Information)) { }
    public TraceSecurityAuditSink(TraceSource source) { _source = source ?? throw new ArgumentNullException(nameof(source)); }
    public Task WriteAsync(SecurityAuditEntry entry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var category = entry.DocumentType == "WebAuthSessionReplay" || entry.DocumentType == "WebAuthRefresh" ||
            entry.DocumentType == "WebAuthLogout" || entry.DocumentType == "WebAuthThrottled" ? entry.DocumentType : "Application";
        var action = Enum.IsDefined(typeof(SecurityAuditAction), entry.Action) ? entry.Action.ToString() : "Unknown";
        var outcome = Enum.IsDefined(typeof(SecurityAuditOutcome), entry.Outcome) ? entry.Outcome.ToString() : "Unknown";
        var actor = entry.UserId > 0 ? entry.UserId.Value.ToString(CultureInfo.InvariantCulture) : "-";
        var correlation = entry.CorrelationId != null && Regex.IsMatch(entry.CorrelationId, "\\A[A-Za-z0-9_-]{1,128}\\z") ? entry.CorrelationId : "-";
        _source.TraceEvent(TraceEventType.Information, 1, string.Format(CultureInfo.InvariantCulture,
            "category={0} action={1} outcome={2} userId={3} correlationId={4} utc={5:O}",
            category, action, outcome, actor, correlation, entry.OccurredAtUtc));
        _source.Flush();
        return Task.CompletedTask;
    }
    public void Dispose() => _source.Close();
}
