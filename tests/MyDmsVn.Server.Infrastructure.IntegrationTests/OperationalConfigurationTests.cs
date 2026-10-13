using System;
using System.Collections.Generic;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class OperationalConfigurationTests
{
    private const string RuntimeConnection =
        "Server=runtime.example;Database=MyDmsVn;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";

    private const string MigrationConnection =
        "Server=migration.example;Database=MyDmsVn;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";

    [Fact]
    public void Purpose_specific_environment_value_overrides_file_without_cross_purpose_fallback()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringEnvironmentVariable] = RuntimeConnection,
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringFileEnvironmentVariable] = "runtime.secret",
            [SqlConnectionConfigurationLoader.MigrationConnectionStringEnvironmentVariable] = MigrationConnection,
            [SqlConnectionConfigurationLoader.MigrationConnectionStringFileEnvironmentVariable] = "migration.secret",
        };
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["runtime.secret"] = "Server=file-runtime;Database=Wrong;Integrated Security=true",
            ["migration.secret"] = "Server=file-migration;Database=Wrong;Integrated Security=true",
        };
        var loader = CreateLoader(environment, files);

        var runtime = loader.Load(SqlConnectionConfigurationPurpose.Runtime);
        var migration = loader.Load(SqlConnectionConfigurationPurpose.Migration);

        Assert.Equal(RuntimeConnection, runtime.ConnectionString);
        Assert.Equal(SqlConnectionConfigurationSource.Environment, runtime.Source);
        Assert.Equal(MigrationConnection, migration.ConnectionString);
        Assert.Equal(SqlConnectionConfigurationSource.Environment, migration.Source);
    }

    [Fact]
    public void Purpose_specific_file_precedes_default_installation_file()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringFileEnvironmentVariable] = "override.secret",
        };
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["override.secret"] = RuntimeConnection,
            ["runtime.default"] = "Server=default-runtime;Database=Wrong;Integrated Security=true",
        };
        var loader = CreateLoader(environment, files);

        var result = loader.Load(SqlConnectionConfigurationPurpose.Runtime);

        Assert.Equal(RuntimeConnection, result.ConnectionString);
        Assert.Equal(SqlConnectionConfigurationSource.EnvironmentFile, result.Source);
    }

    [Fact]
    public void Missing_runtime_configuration_fails_closed_with_safe_code()
    {
        var loader = CreateLoader(
            new Dictionary<string, string?>(),
            new Dictionary<string, string>());

        var exception = Assert.Throws<OperationalConfigurationException>(
            () => loader.Load(SqlConnectionConfigurationPurpose.Runtime));

        Assert.Equal("Startup.DatabaseConfigurationMissing", exception.Code);
        Assert.DoesNotContain("Password", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unreadable_explicit_file_maps_security_failures_without_exposing_path()
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringFileEnvironmentVariable] =
                "C:\\sensitive\\runtime.connection",
        };
        var loader = new SqlConnectionConfigurationLoader(
            name => environment.TryGetValue(name, out var value) ? value : null,
            _ => throw new System.Security.SecurityException("C:\\sensitive\\runtime.connection"),
            _ => "unused");

        var exception = Assert.Throws<OperationalConfigurationException>(
            () => loader.Load(SqlConnectionConfigurationPurpose.Runtime));

        Assert.Equal("Startup.DatabaseConfigurationUnreadable", exception.Code);
        Assert.DoesNotContain("C:\\sensitive", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Server=secret-host;Database=master;User ID=secret-user;Password=secret-password")]
    [InlineData("Server=secret-host;User ID=secret-user;Password=secret-password")]
    [InlineData("not-a-connection-string;Password=secret-password")]
    public void Invalid_target_is_rejected_without_echoing_secret(string connectionString)
    {
        var environment = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [SqlConnectionConfigurationLoader.RuntimeConnectionStringEnvironmentVariable] = connectionString,
        };
        var loader = CreateLoader(environment, new Dictionary<string, string>());

        var exception = Assert.Throws<OperationalConfigurationException>(
            () => loader.Load(SqlConnectionConfigurationPurpose.Runtime));

        Assert.Equal("Startup.DatabaseConfigurationInvalid", exception.Code);
        Assert.DoesNotContain("secret-host", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-user", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-password", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static SqlConnectionConfigurationLoader CreateLoader(
        IReadOnlyDictionary<string, string?> environment,
        IReadOnlyDictionary<string, string> files)
    {
        return new SqlConnectionConfigurationLoader(
            name => environment.TryGetValue(name, out var value) ? value : null,
            path => files.TryGetValue(path, out var value)
                ? value
                : throw new System.IO.FileNotFoundException("Configuration file not found."),
            purpose => purpose == SqlConnectionConfigurationPurpose.Runtime
                ? "runtime.default"
                : "migration.default");
    }
}
