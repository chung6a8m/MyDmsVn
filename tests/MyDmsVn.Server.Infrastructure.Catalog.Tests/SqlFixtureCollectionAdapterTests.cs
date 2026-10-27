using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

[Collection(CatalogSqlCollectionFixture.CollectionName)]
public sealed class SqlFixtureCollectionAdapterTests
{
    private readonly CatalogSqlCollectionFixture _fixture;
    private readonly ITestOutputHelper _output;
    public SqlFixtureCollectionAdapterTests(CatalogSqlCollectionFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SqlServerFact]
    public Task Fact_receives_reset_and_seed_before_body() =>
        _fixture.RunAsync(nameof(Fact_receives_reset_and_seed_before_body), _output, AssertBaselineAndMutateAsync, SeedUserAsync);

    [SqlServerTheory]
    [InlineData(1)]
    [InlineData(2)]
    public Task Every_theory_row_receives_reset_and_seed_before_body(int row) =>
        _fixture.RunAsync(nameof(Every_theory_row_receives_reset_and_seed_before_body) + "/row-" + row,
            _output, AssertBaselineAndMutateAsync, SeedUserAsync);

    private static async Task SeedUserAsync(SqlFixtureTestScope scope, CancellationToken token)
    {
        using var connection = new SqlConnection(scope.ConnectionString);
        await connection.ExecuteAsync(new SqlTestCommand(@"
INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm)
VALUES(N'adapter-user',N'ADAPTER-USER',N'Baseline',N'Local',N'hash',N'',N'BCrypt');", cancellationToken: token));
        Assert.Equal(1, await connection.QuerySingleAsync<int>(new SqlTestCommand(
            "SELECT COUNT(*) FROM dbo.Users;", cancellationToken: token)));
    }

    private static async Task AssertBaselineAndMutateAsync(SqlFixtureTestScope scope, CancellationToken token)
    {
        var connection = CatalogSqlCollectionFixture.Track(scope, new SqlConnection(scope.ConnectionString));
        Assert.Equal("Baseline", await connection.QuerySingleAsync<string>(new SqlTestCommand(
            "SELECT DisplayName FROM dbo.Users;", cancellationToken: token)));
        await connection.ExecuteAsync(new SqlTestCommand("UPDATE dbo.Users SET DisplayName=N'changed';", cancellationToken: token));
    }
}
