using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class Web6CutoverSeedTests
{
    private static readonly Guid Epoch = Guid.Parse("11111111-2222-4333-8444-555555555555");
    private const string Update = "UPDATE dbo.WebDeploymentState SET DeploymentEpoch=@epoch WHERE Id=1;";
    private const string Count = " SELECT CONVERT(int, @@ROWCOUNT);";

    [Theory]
    [InlineData("11111111-2222-4333-8444-555555555555")]
    [InlineData("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee")]
    public async Task Epoch_seed_requires_explicit_single_row_evidence_when_nocount_suppresses_provider_count(string epochText)
    {
        using var session = new SqlConnection();
        var epoch = Guid.Parse(epochText);
        var calls = 0;
        await WebHostIisExpressTests.InitializeCutoverEpoch(session, epoch, command =>
        {
            calls++;
            Assert.Same(session, command.Connection);
            Assert.Null(command.Transaction);
            Assert.Equal(CommandType.Text, command.CommandType);
            Assert.Equal(30, command.CommandTimeout);
            var parameter = Assert.IsType<SqlParameter>(Assert.Single(command.Parameters));
            Assert.Equal("@epoch", parameter.ParameterName);
            Assert.Equal(SqlDbType.UniqueIdentifier, parameter.SqlDbType);
            Assert.Equal(epoch, parameter.Value);
            // A NOCOUNT session suppresses provider counts even when the UPDATE affects one row.
            return Task.FromResult<object?>(command.CommandText == Update + Count ? 1 : -1);
        });
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    [InlineData(1L)]
    [InlineData("1")]
    [InlineData(null)]
    public async Task Epoch_seed_refuses_missing_multiple_or_non_int_row_evidence(object? rowCount)
    {
        using var session = new SqlConnection();
        var calls = 0;
        await Assert.ThrowsAsync<IOException>(() => WebHostIisExpressTests.InitializeCutoverEpoch(session, Epoch, command =>
        {
            calls++;
            Assert.Same(session, command.Connection);
            return Task.FromResult(rowCount);
        }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Epoch_seed_refuses_sql_null()
    {
        using var session = new SqlConnection();
        await Assert.ThrowsAsync<IOException>(() => WebHostIisExpressTests.InitializeCutoverEpoch(
            session, Epoch, _ => Task.FromResult<object?>(DBNull.Value)));
    }
}
