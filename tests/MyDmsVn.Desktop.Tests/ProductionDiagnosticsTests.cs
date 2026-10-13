using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
            var exception = new InvalidOperationException(
                "outer PasswordHash=legacy-hash; PasswordSalt=legacy-salt",
                new Exception(
                    "Server=db;Database=prod;User Id=sa;Password=secret;AuthToken=abc.def"));

            var safe = SensitiveDataRedactor.ToSafeDiagnostic(exception);

            Assert.DoesNotContain("legacy-hash", safe);
            Assert.DoesNotContain("legacy-salt", safe);
            Assert.DoesNotContain("secret", safe);
            Assert.DoesNotContain("sa", safe);
            Assert.DoesNotContain("abc.def", safe);
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
                    await provider.GetRequiredService<ISecurityAuditSink>().WriteAsync(
                        new SecurityAuditEntry(
                            SecurityAuditAction.Authentication,
                            SecurityAuditOutcome.Failed,
                            null,
                            new DateTime(2026, 10, 13, 1, 2, 3, DateTimeKind.Utc)),
                        CancellationToken.None);
                }
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
    }
}
