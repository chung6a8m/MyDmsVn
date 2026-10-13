using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Diagnostics;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class ProductionDiagnosticsTests
    {
        [Fact]
        public async Task Production_registration_writes_structured_audit_with_request_correlation()
        {
            var directory = NewTemporaryDirectory();
            try
            {
                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddLocalDesktopAdapter();
                services.AddDesktopProductionDiagnostics(
                    new DesktopDiagnosticsOptions("MyDmsVn.Net8", directory, 7));

                using (var provider = services.BuildServiceProvider())
                {
                    var sink = provider.GetRequiredService<ISecurityAuditSink>();
                    Assert.DoesNotContain("NoOp", sink.GetType().Name, StringComparison.Ordinal);
                    var scopeFactory = provider.GetRequiredService<IRequestDiagnosticsScopeFactory>();
                    using (var scope = scopeFactory.Begin("PostGoodsReceiptCommand"))
                    {
                        await sink.WriteAsync(
                            new SecurityAuditEntry(
                                SecurityAuditAction.GoodsReceiptPost,
                                SecurityAuditOutcome.Succeeded,
                                42,
                                new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc),
                                "GoodsReceipt",
                                "41",
                                scope.CorrelationId),
                            CancellationToken.None);
                    }
                }

                var auditFile = Assert.Single(Directory.GetFiles(directory, "audit-*.json"));
                var payload = File.ReadAllText(auditFile);
                Assert.Contains("GoodsReceiptPost", payload);
                Assert.Contains("GoodsReceipt", payload);
                Assert.Contains("\"DocumentId\":\"41\"", payload);
                Assert.Contains("\"CorrelationId\":", payload);
                Assert.DoesNotContain("Password", payload, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void Redactor_removes_credentials_and_nested_sql_exception_details()
        {
            const string jwt = "eyJhbGciOiJIUzI1NiJ9.abc.signature";
            var exception = new InvalidOperationException(
                "outer PasswordHash=legacy-hash; PasswordSalt=legacy-salt; Password='two words'",
                new Exception(
                    "Authorization: Bearer " + jwt + Environment.NewLine +
                    "Server=db;Database=prod;User Id=sa;Password=secret;AuthToken=abc.def"));

            var safe = SensitiveDataRedactor.ToSafeDiagnostic(exception);

            Assert.DoesNotContain("legacy-hash", safe);
            Assert.DoesNotContain("legacy-salt", safe);
            Assert.DoesNotContain("secret", safe);
            Assert.DoesNotContain("sa", safe);
            Assert.DoesNotContain("abc.def", safe);
            Assert.DoesNotContain(jwt, safe);
            Assert.DoesNotContain("two words", safe);
            Assert.Contains("[REDACTED]", safe);
        }

        [Fact]
        public async Task Unwritable_log_location_uses_non_modal_fallback_sink()
        {
            var directory = NewTemporaryDirectory();
            var fileInsteadOfDirectory = Path.Combine(directory, "blocked");
            File.WriteAllText(fileInsteadOfDirectory, "not a directory");
            try
            {
                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddLocalDesktopAdapter();
                services.AddDesktopProductionDiagnostics(
                    new DesktopDiagnosticsOptions("MyDmsVn.Net48", fileInsteadOfDirectory, 7));

                using (var provider = services.BuildServiceProvider())
                {
                    var status = provider.GetRequiredService<IDiagnosticsStartupStatus>();
                    Assert.False(status.FileLoggingEnabled);
                    Assert.Equal("Diagnostics.FileSinkUnavailable", status.SafeErrorCode);
                    await provider.GetRequiredService<ISecurityAuditWriter>().WriteAsync(
                        new SecurityAuditEntry(
                            SecurityAuditAction.Authentication,
                            SecurityAuditOutcome.Failed,
                            null,
                            new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc)),
                        businessOperationCommitted: false,
                        CancellationToken.None);
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Runtime_audit_file_failure_degrades_health_and_reports_committed_state()
        {
            var directory = NewTemporaryDirectory();
            try
            {
                var reporter = new CapturingAuditFailureReporter();
                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddLocalDesktopAdapter();
                services.AddDesktopProductionDiagnostics(
                    new DesktopDiagnosticsOptions("MyDmsVn.Net8", directory, 7));
                services.RemoveAll<ISecurityAuditFailureReporter>();
                services.AddSingleton<ISecurityAuditFailureReporter>(reporter);

                using (var provider = services.BuildServiceProvider())
                {
                    var writer = provider.GetRequiredService<ISecurityAuditWriter>();
                    var entry = new SecurityAuditEntry(
                        SecurityAuditAction.GoodsReceiptPost,
                        SecurityAuditOutcome.Succeeded,
                        42,
                        new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc),
                        "GoodsReceipt",
                        "41");
                    await writer.WriteAsync(entry, true, CancellationToken.None);
                    var auditFile = Assert.Single(Directory.GetFiles(directory, "audit-*.json"));
                    File.Delete(auditFile);
                    Directory.CreateDirectory(auditFile);

                    await writer.WriteAsync(entry, true, CancellationToken.None);

                    var status = provider.GetRequiredService<IDiagnosticsStartupStatus>();
                    Assert.False(status.FileLoggingEnabled);
                    Assert.Equal("Diagnostics.AuditFileSinkUnavailable", status.SafeErrorCode);
                    Assert.Equal(1, reporter.Failures);
                    Assert.True(reporter.BusinessOperationCommitted);
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task Retention_cleanup_failure_does_not_disable_subsequent_audit_writes()
        {
            var directory = NewTemporaryDirectory();
            try
            {
                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddLocalDesktopAdapter();
                services.AddDesktopProductionDiagnostics(
                    new DesktopDiagnosticsOptions("MyDmsVn.Net8", directory, 1));

                using (var provider = services.BuildServiceProvider())
                {
                    var sink = provider.GetRequiredService<ISecurityAuditSink>();
                    await sink.WriteAsync(
                        AuditEntry(new DateTime(2026, 10, 11, 1, 2, 3, DateTimeKind.Utc), "old"),
                        CancellationToken.None);
                    var oldAuditFile = Assert.Single(Directory.GetFiles(directory, "audit-20261011.json"));
                    using (File.Open(oldAuditFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        await sink.WriteAsync(
                            AuditEntry(new DateTime(2026, 10, 12, 1, 2, 3, DateTimeKind.Utc), "first"),
                            CancellationToken.None);
                        await sink.WriteAsync(
                            AuditEntry(new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc), "second"),
                            CancellationToken.None);

                        var status = provider.GetRequiredService<IDiagnosticsStartupStatus>();
                        Assert.True(status.FileLoggingEnabled);
                        Assert.Null(status.SafeErrorCode);
                        var latestPayload = File.ReadAllText(
                            Path.Combine(directory, "audit-20261013.json"));
                        Assert.Contains("\"DocumentId\":\"second\"", latestPayload);
                    }
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public void Desktop_exception_reporter_writes_redacted_diagnostic_with_support_id()
        {
            var directory = NewTemporaryDirectory();
            try
            {
                string correlationId;
                var services = new ServiceCollection();
                services.AddServerApplication();
                services.AddLocalDesktopAdapter();
                services.AddDesktopProductionDiagnostics(
                    new DesktopDiagnosticsOptions("MyDmsVn.Net8", directory, 7));

                using (var provider = services.BuildServiceProvider())
                {
                    correlationId = provider.GetRequiredService<IDesktopExceptionReporter>().Report(
                        "GoodsReceiptEditorViewModel",
                        new InvalidOperationException("Authorization: Bearer secret.jwt.value"));
                }

                var applicationFile = Assert.Single(
                    Directory.GetFiles(directory, "application-*.json"));
                var payload = File.ReadAllText(applicationFile);
                Assert.Matches("^[a-f0-9]{32}$", correlationId);
                Assert.Contains(correlationId, payload);
                Assert.Contains("GoodsReceiptEditorViewModel", payload);
                Assert.Contains("InvalidOperationException", payload);
                Assert.DoesNotContain("secret.jwt.value", payload);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static string NewTemporaryDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "MyDmsVn.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static SecurityAuditEntry AuditEntry(DateTime occurredAtUtc, string documentId) =>
            new SecurityAuditEntry(
                SecurityAuditAction.GoodsReceiptPost,
                SecurityAuditOutcome.Succeeded,
                42,
                occurredAtUtc,
                "GoodsReceipt",
                documentId);

        private sealed class CapturingAuditFailureReporter : ISecurityAuditFailureReporter
        {
            public int Failures { get; private set; }
            public bool BusinessOperationCommitted { get; private set; }

            public void Report(
                SecurityAuditEntry entry,
                Exception exception,
                bool businessOperationCommitted)
            {
                Failures++;
                BusinessOperationCommitted = businessOperationCommitted;
            }
        }
    }
}
