using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class DatabaseMigratorCommandTests
{
    [SqlServerFact]
    public async Task Unknown_command_exits_without_modifying_the_database()
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            var executable = Path.Combine(
                AppContext.BaseDirectory,
                "MyDmsVn.Server.DbMigrator.exe");
            Assert.True(File.Exists(executable), "DbMigrator executable was not copied to test output.");
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "permissions sycn --dry-run",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.EnvironmentVariables[
                SqlConnectionConfigurationLoader.MigrationConnectionStringEnvironmentVariable] =
                database.ConnectionString;

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            var standardOutput = process!.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                Assert.Fail("Unknown-command subprocess did not exit within 15 seconds.");
            }

            var output = await standardOutput;
            var error = await standardError;
            Assert.Equal(2, process.ExitCode);
            Assert.Equal(string.Empty, output);
            Assert.Contains("DbMigrator.InvalidArguments", error, StringComparison.Ordinal);

            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Null(await connection.ExecuteScalarAsync<int?>(
                "SELECT OBJECT_ID(N'dbo.SchemaVersions', N'U');"));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public void Permission_sync_apply_requires_exact_plan_id_before_loading_configuration()
    {
        var loader = new SqlConnectionConfigurationLoader(
            _ => "Server=secret-host;Database=secret-db;User ID=secret;Password=secret-password",
            _ => throw new InvalidOperationException(),
            _ => "unused");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = PermissionSyncCommand.Run(
            new[] { "--apply", "--approvals", "approval.json" },
            loader,
            output,
            error,
            new DateTime(2026, 10, 14, 1, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, exitCode);
        Assert.Contains("PermissionSync.InvalidArguments", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Bootstrap_rejects_password_command_line_argument_without_echoing_it()
    {
        var loader = new SqlConnectionConfigurationLoader(
            _ => throw new InvalidOperationException("configuration must not be loaded"),
            _ => throw new InvalidOperationException(),
            _ => "unused");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = InitialAdministratorProvisioningCommand.Run(
            new[] { "--apply", "--password", "DoNotEcho!456" },
            loader,
            new NonInteractiveSecretReader(),
            output,
            error,
            new DateTime(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, exitCode);
        Assert.Contains("SecurityBootstrap.InvalidArguments", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DoNotEcho", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Administrator_grant_change_requires_exact_operation_and_target_before_configuration()
    {
        var loader = new SqlConnectionConfigurationLoader(
            _ => throw new InvalidOperationException("configuration must not be loaded"),
            _ => throw new InvalidOperationException(),
            _ => "unused");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = AdministratorGrantCommand.Run(
            new[] { "--revoke", "--restore" },
            loader,
            output,
            error,
            new DateTime(2026, 10, 14, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, exitCode);
        Assert.Contains("PermissionSync.InvalidArguments", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Missing_migration_configuration_returns_safe_actionable_error()
    {
        var loader = new SqlConnectionConfigurationLoader(
            _ => null,
            _ => throw new FileNotFoundException("sensitive installation path"),
            _ => "sensitive installation path");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = DatabaseMigratorCommand.Run(loader, output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Startup.DatabaseConfigurationMissing", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sensitive installation path", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Password", error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Runtime_setting_is_never_used_as_migration_fallback()
    {
        var environment = new Dictionary<string, string?>
        {
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringEnvironmentVariable] =
                "Server=runtime-only;Database=MyDmsVn;User ID=runtime;Password=runtime-secret",
        };
        var loader = new SqlConnectionConfigurationLoader(
            name => environment.TryGetValue(name, out var value) ? value : null,
            _ => throw new FileNotFoundException(),
            _ => "missing");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = DatabaseMigratorCommand.Run(loader, output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Startup.DatabaseConfigurationMissing", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-secret", error.ToString(), StringComparison.Ordinal);
    }

    private sealed class NonInteractiveSecretReader : IProvisioningSecretReader
    {
        public bool IsInteractive => false;

        public string ReadSecret(string prompt, TextWriter output)
        {
            throw new InvalidOperationException("Secret input must not be requested.");
        }
    }
}
