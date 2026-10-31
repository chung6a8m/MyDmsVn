using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using Xunit;
namespace MyDmsVn.Server.HttpHost.Tests;
public sealed class AuditTests
{
    [Fact]
    public async Task Host_audit_contains_only_approved_fields_and_never_document_or_token_text()
    {
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var source = new TraceSource("Web2AuditTests", SourceLevels.Information);
        source.Listeners.Clear(); source.Listeners.Add(new TextWriterTraceListener(output));
        using var sink = new TraceSecurityAuditSink(source);
        await sink.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.Authentication, SecurityAuditOutcome.Denied, 7,
            HostFixture.Now, "WebAuthSessionReplay", "secret-refresh-token-or-hash", "audit-123"), CancellationToken.None);
        await sink.WriteAsync(new SecurityAuditEntry((SecurityAuditAction)999, (SecurityAuditOutcome)999, -1,
            HostFixture.Now, "secret-document-type", "secret-password", "secret\r\ncorrelation"), CancellationToken.None);
        var text = output.ToString();
        Assert.Contains("category=WebAuthSessionReplay", text);
        Assert.Contains("action=Authentication", text);
        Assert.Contains("outcome=Denied", text);
        Assert.Contains("userId=7", text);
        Assert.Contains("correlationId=audit-123", text);
        Assert.Contains("action=Unknown", text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("999", text);
        Assert.DoesNotContain("userId=-1", text);
    }
}
