using System;
using System.Collections.Generic;
using System.IO;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class DatabaseMigratorCommandTests
{
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
}
