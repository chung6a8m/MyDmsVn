using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopDatabaseStartupCoordinatorTests
    {
        private const string RuntimeConnection =
            "Server=runtime.example;Database=MyDmsVn;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";

        [Fact]
        public async Task Compatible_runtime_configuration_is_returned_for_host_composition()
        {
            var checker = new StubSchemaChecker(
                new SchemaCompatibilityResult(
                    SchemaCompatibilityStatus.Compatible,
                    "Schema.Compatible",
                    "Compatible"));
            var coordinator = new DesktopDatabaseStartupCoordinator(
                CreateLoader(SqlConnectionConfigurationPurpose.Runtime, RuntimeConnection),
                connectionString =>
                {
                    Assert.Equal(RuntimeConnection, connectionString);
                    return checker;
                });

            var result = await coordinator.PrepareAsync(CancellationToken.None);

            Assert.Equal(RuntimeConnection, result.ConnectionString);
            Assert.Equal(1, checker.CallCount);
        }

        [Fact]
        public async Task Incompatible_schema_fails_closed_with_safe_upgrade_instruction()
        {
            var coordinator = new DesktopDatabaseStartupCoordinator(
                CreateLoader(SqlConnectionConfigurationPurpose.Runtime, RuntimeConnection),
                _ => new StubSchemaChecker(
                    new SchemaCompatibilityResult(
                        SchemaCompatibilityStatus.Partial,
                        "Schema.PartialMigration",
                        "Cần quản trị viên xử lý phục hồi.")));

            var exception = await Assert.ThrowsAsync<DesktopStartupException>(
                () => coordinator.PrepareAsync(CancellationToken.None));

            Assert.Equal("Schema.PartialMigration", exception.Code);
            Assert.Contains("quản trị viên", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(RuntimeConnection, exception.SafeMessage, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Migration_configuration_is_never_used_by_desktop_startup()
        {
            var coordinator = new DesktopDatabaseStartupCoordinator(
                CreateLoader(SqlConnectionConfigurationPurpose.Migration, RuntimeConnection),
                _ => throw new InvalidOperationException("Schema checker must not be created."));

            var exception = await Assert.ThrowsAsync<OperationalConfigurationException>(
                () => coordinator.PrepareAsync(CancellationToken.None));

            Assert.Equal("Startup.DatabaseConfigurationMissing", exception.Code);
        }

        private static SqlConnectionConfigurationLoader CreateLoader(
            SqlConnectionConfigurationPurpose purpose,
            string connectionString)
        {
            var variable = purpose == SqlConnectionConfigurationPurpose.Runtime
                ? SqlConnectionConfigurationLoader.RuntimeConnectionStringEnvironmentVariable
                : SqlConnectionConfigurationLoader.MigrationConnectionStringEnvironmentVariable;
            var environment = new Dictionary<string, string?>
            {
                [variable] = connectionString,
            };
            return new SqlConnectionConfigurationLoader(
                name => environment.TryGetValue(name, out var value) ? value : null,
                _ => throw new FileNotFoundException(),
                _ => "missing");
        }

        private sealed class StubSchemaChecker : ISchemaCompatibilityChecker
        {
            private readonly SchemaCompatibilityResult _result;

            public StubSchemaChecker(SchemaCompatibilityResult result)
            {
                _result = result;
            }

            public int CallCount { get; private set; }

            public Task<SchemaCompatibilityResult> CheckAsync(CancellationToken cancellationToken)
            {
                CallCount++;
                return Task.FromResult(_result);
            }
        }
    }
}
