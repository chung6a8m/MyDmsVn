using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Application.Identity;
using Newtonsoft.Json.Linq;
using Xunit;
namespace MyDmsVn.Server.HttpHost.Tests;
[Collection("WEB2 environment")]
public sealed class AuditTests
{
    [Fact]
    public async Task protected_file_sink_writes_only_allowlisted_fields_and_surfaces_acl_io_failure()
    {
        using var destination = new PrivateAuditDestination();
        var options = CandidateOptions(destination);
        var sink = new ProtectedFileSecurityAuditSink(options);
        await sink.WriteAsync(new SecurityAuditEntry(SecurityAuditAction.Authentication, SecurityAuditOutcome.Denied, 7,
            HostFixture.Now, "WebAuthRefresh", "secret-document-id", "audit-123"), CancellationToken.None);
        await sink.WriteAsync(new SecurityAuditEntry((SecurityAuditAction)999, (SecurityAuditOutcome)999, -1,
            HostFixture.Now, "secret-category", "secret-payload", "secret\r\ncorrelation"), CancellationToken.None);

        var lines = File.ReadAllLines(destination.Path);
        Assert.Equal(2, lines.Length);
        var first = JObject.Parse(lines[0]);
        Assert.Equal(new[] { "action", "category", "correlationId", "outcome", "userId", "utc" },
            first.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("Authentication", (string?)first["action"]);
        Assert.Equal("Denied", (string?)first["outcome"]);
        Assert.Equal("WebAuthRefresh", (string?)first["category"]);
        Assert.Equal(7, (int?)first["userId"]);
        Assert.Equal("audit-123", (string?)first["correlationId"]);
        Assert.Equal(HostFixture.Now, (DateTime?)first["utc"]);
        Assert.Equal("Unknown", (string?)JObject.Parse(lines[1])["action"]);
        Assert.DoesNotContain("secret", string.Concat(lines));

        using (var locked = File.Open(destination.Path, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            var error = await Assert.ThrowsAsync<IOException>(() => sink.WriteAsync(
                new SecurityAuditEntry(SecurityAuditAction.Authentication, SecurityAuditOutcome.Denied, null, HostFixture.Now),
                CancellationToken.None));
            Assert.Null(error.InnerException);
            Assert.DoesNotContain(destination.Path, error.Message);
        }

        var security = File.GetAccessControl(destination.Path);
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            FileSystemRights.Read, AccessControlType.Allow));
        File.SetAccessControl(destination.Path, security);
        var aclError = Assert.Throws<IOException>(() => new ProtectedFileSecurityAuditSink(options));
        Assert.Null(aclError.InnerException);
        Assert.DoesNotContain(destination.Path, aclError.Message);
    }

    [Fact]
    public async Task Candidate_health_refuses_degraded_audit_and_preserves_request_correlation()
    {
        using var destination = new PrivateAuditDestination();
        var options = CandidateOptions(destination);
        Action<HttpConfiguration>? configureApi = null;
        ISecurityAuditReadiness? readiness = null;
        using var host = new AuthFixture(services =>
        {
            configureApi = Startup.ConfigureSecurityAudit(services, options);
            readiness = (ISecurityAuditReadiness?)services.Single(service => service.ServiceType == typeof(ISecurityAuditReadiness)).ImplementationInstance;
        }, options: options, configureApi: config => configureApi?.Invoke(config));

        using var healthy = await host.Server.HttpClient.GetAsync(HostFixture.Origin + "/api/v1/health");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
        Assert.Equal(SecurityAuditReadinessStatus.Healthy, readiness!.Snapshot.Status);
        Assert.Null(readiness.Snapshot.LastFailureAtUtc);

        using (File.Open(destination.Path, FileMode.Open, FileAccess.Write, FileShare.None))
        using (var denied = await host.Post("login", "{\"username\":\"alice\",\"password\":\"wrong-secret\"}"))
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        Assert.Equal(SecurityAuditReadinessStatus.Degraded, readiness.Snapshot.Status);
        Assert.Equal(host.Clock.UtcNow, readiness.Snapshot.LastFailureAtUtc);
        using var auditedLogin = await host.Login();
        Assert.Equal(HttpStatusCode.OK, auditedLogin.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, HostFixture.Origin + "/api/v1/health");
        request.Headers.Add("X-Correlation-ID", "audit-readiness-test");
        using var degraded = await host.Server.HttpClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, degraded.StatusCode);
        var payload = await degraded.Content.ReadAsStringAsync();
        Assert.Contains("SecurityAudit.Unavailable", payload);
        Assert.Contains("audit-readiness-test", payload);
        Assert.DoesNotContain(destination.Path, payload);
    }

    private static WebHostOptions CandidateOptions(PrivateAuditDestination destination)
    {
        WebHostOptions? options = null;
        AccessLifetimeTests.WithEnvironment(null, () => options = WebHostOptions.FromEnvironment(),
            "web5", "receipt-v1", "candidate-1", destination.Root, destination.Path);
        return options!;
    }

    internal sealed class PrivateAuditDestination : IDisposable
    {
        public PrivateAuditDestination()
        {
            Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mydmsvn-audit-" + Guid.NewGuid().ToString("N"));
            Path = System.IO.Path.Combine(Root, "audit.jsonl");
            using var identity = WindowsIdentity.GetCurrent();
            var owner = identity.User!;
            var security = new DirectorySecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            Directory.CreateDirectory(Root, security);
            File.WriteAllBytes(Path, Array.Empty<byte>());
            var fileSecurity = File.GetAccessControl(Path);
            fileSecurity.SetOwner(owner);
            File.SetAccessControl(Path, fileSecurity);
        }

        public string Root { get; }
        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
            Directory.Delete(Root);
        }
    }

    [Fact]
    public async Task Host_audit_contains_only_approved_fields_and_never_document_or_token_text()
    {
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        var source = new TraceSource("Web2AuditTests", SourceLevels.Information);
        source.Listeners.Clear(); source.Listeners.Add(new TextWriterTraceListener(output));
        // This test owns an enabled capture source; net48 lazy configuration initialization can leave its default switch Off.
        source.Switch.Level = SourceLevels.Information;
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
