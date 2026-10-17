using System;
using System.IO;
using MyDmsVn.Server.DbMigrator.SecurityProvisioning;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SuperuserMaintenanceCommandTests
{
    [Fact]
    public void Recovery_rejects_password_argument_before_loading_maintenance_configuration()
    {
        var loader = CreateUnreachableConfigurationLoader();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SuperuserMaintenanceCommand.RunRecovery(
            new[] { "--target", "MyDmsVn", "--expected-user-id", "7", "--password", "DoNotEcho!456" },
            loader,
            new NonInteractiveSecretReader(),
            new StringReader(string.Empty),
            output,
            error,
            new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, exitCode);
        Assert.Contains("SecurityMaintenance.Failed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("DoNotEcho", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Rename_rejects_unknown_options_before_loading_maintenance_configuration()
    {
        var loader = CreateUnreachableConfigurationLoader();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = SuperuserMaintenanceCommand.RunRename(
            new[]
            {
                "--target", "MyDmsVn", "--expected-user-id", "7",
                "--new-username", "root.next", "--unexpected", "value",
            },
            loader,
            new NonInteractiveSecretReader(),
            new StringReader(string.Empty),
            output,
            error,
            new DateTime(2026, 10, 17, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, exitCode);
        Assert.Contains("SecurityMaintenance.Failed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("root.next", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    private static SqlConnectionConfigurationLoader CreateUnreachableConfigurationLoader() =>
        new(
            _ => throw new InvalidOperationException("Configuration must not be loaded."),
            _ => throw new InvalidOperationException("A file must not be read."),
            _ => "unused");

    private sealed class NonInteractiveSecretReader : IProvisioningSecretReader
    {
        public bool IsInteractive => false;

        public string ReadSecret(string prompt, TextWriter output) =>
            throw new InvalidOperationException("Secret input must not be requested.");
    }
}
