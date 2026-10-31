using System;
using MyDmsVn.Server.Application.Identity;
using Xunit;

namespace MyDmsVn.Server.HttpHost.Tests;

public sealed class WebAuthSessionConfigurationTests
{
    [Fact]
    public void Missing_lifetime_settings_preserve_seven_day_idle_and_thirty_day_absolute()
    {
        var options = WebAuthSessionConfiguration.Read(_ => null);
        Assert.Equal(7, options.IdleLifetimeDays);
        Assert.Equal(30, options.AbsoluteLifetimeDays);
    }

    [Fact]
    public void Operator_can_shorten_refresh_lifetimes()
    {
        var options = WebAuthSessionConfiguration.Read(name => name.EndsWith("IDLE_DAYS", StringComparison.Ordinal) ? "2" : "5");
        Assert.Equal(2, options.IdleLifetimeDays);
        Assert.Equal(5, options.AbsoluteLifetimeDays);
    }

    [Theory]
    [InlineData("0", "30")]
    [InlineData("8", "30")]
    [InlineData("7", "31")]
    [InlineData("7", "6")]
    [InlineData("", "30")]
    [InlineData("secret-invalid-setting", "30")]
    public void Invalid_operator_settings_fail_closed_without_echoing_values(string idle, string absolute)
    {
        var error = Assert.Throws<InvalidOperationException>(() => WebAuthSessionConfiguration.Read(
            name => name.EndsWith("IDLE_DAYS", StringComparison.Ordinal) ? idle : absolute));
        Assert.Equal("WEB2 refresh lifetime configuration is invalid.", error.Message);
        Assert.Null(error.InnerException);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(8, 30)]
    [InlineData(1, 0)]
    [InlineData(1, 31)]
    [InlineData(7, 6)]
    public void Direct_store_configuration_cannot_bypass_lifetime_bounds(int idle, int absolute)
    {
        Assert.ThrowsAny<ArgumentException>(() => new WebAuthSessionOptions(idle, absolute));
    }
}
