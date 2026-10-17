using System;
using MyDmsVn.Server.Infrastructure.Operations;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.Security.Tests;

public sealed class SuperuserConfigurationTests
{
    [Fact]
    public void Required_setting_is_normalized_for_application_registration()
    {
        var loader = new SuperuserConfigurationLoader(
            name => name == SuperuserConfigurationLoader.EnvironmentVariable
                ? "  root.admin  "
                : null);

        Assert.Equal("root.admin", loader.Load());
    }

    [Fact]
    public void Missing_setting_stops_startup_with_safe_configuration_error()
    {
        foreach (var value in new string?[] { null, string.Empty, "   " })
        {
            var loader = new SuperuserConfigurationLoader(_ => value);
            var exception = Assert.Throws<OperationalConfigurationException>(() => loader.Load());

            Assert.Equal("Startup.SuperuserConfigurationMissing", exception.Code);
            Assert.DoesNotContain("root.admin", exception.SafeMessage, StringComparison.Ordinal);
        }
    }
}
