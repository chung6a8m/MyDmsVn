using System;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class SqlTestDatabaseNameTests
{
    [Fact]
    public void Create_includes_test_prefix_and_compact_identifier()
    {
        var name = SqlTestDatabaseName.Create();

        Assert.StartsWith("MyDmsVn_Test_", name, StringComparison.Ordinal);
        Assert.DoesNotContain("-", name, StringComparison.Ordinal);
        Assert.True(name.Length <= 128);
    }

    [Theory]
    [InlineData("MyDmsVn")]
    [InlineData("Production")]
    [InlineData("MyDmsVn_Test_")]
    [InlineData("MyDmsVn_Test_not-a-guid")]
    public void EnsureDisposable_rejects_names_not_created_by_the_harness(string name)
    {
        Assert.Throws<InvalidOperationException>(() => SqlTestDatabaseName.EnsureDisposable(name));
    }

    [Fact]
    public void EnsureDisposable_accepts_a_generated_name()
    {
        var name = SqlTestDatabaseName.Create();

        SqlTestDatabaseName.EnsureDisposable(name);
    }
}
