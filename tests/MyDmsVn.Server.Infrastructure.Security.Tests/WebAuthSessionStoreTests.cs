using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class WebAuthSessionStoreTests
{
    [Theory]
    [InlineData("")]
    [InlineData("b935c2ae2de743b5a1ee79ad2ce6ce1d")]
    [InlineData("B935C2AE-2DE7-43B5-A1EE-79AD2CE6CE1D")]
    [InlineData(" b935c2ae-2de7-43b5-a1ee-79ad2ce6ce1d")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("b935c2ae-2de7-13b5-a1ee-79ad2ce6ce1d")]
    [InlineData("b935c2ae-2de7-43b5-71ee-79ad2ce6ce1d")]
    public async Task Noncanonical_or_non_v4_refresh_is_rejected_without_SQL(string value)
    {
        var store = new SqlWebAuthSessionStore(new RefusingConnectionFactory(), new FixedClock());
        Assert.Null(await store.RotateAsync(value, CancellationToken.None));
        await store.RevokeAsync(value, CancellationToken.None);
    }

    [Fact]
    public async Task Invalid_actor_identity_is_rejected_without_SQL()
    {
        var store = new SqlWebAuthSessionStore(new RefusingConnectionFactory(), new FixedClock());
        Assert.Null(await store.IssueAsync(0, CancellationToken.None));
        Assert.Null(await store.GetActiveUserAsync(Guid.Empty, 1, CancellationToken.None));
        Assert.Null(await store.GetActiveUserAsync(Guid.NewGuid(), -1, CancellationToken.None));
    }

    [Fact]
    public async Task Maintenance_refuses_short_retention_without_SQL()
    {
        var maintenance = new SqlWebAuthSessionMaintenance(new RefusingConnectionFactory(), new FixedClock());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => maintenance.CleanupExpiredAsync(TimeSpan.FromDays(6), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => maintenance.CleanupExpiredAsync(TimeSpan.FromDays(366), CancellationToken.None));
    }

    private sealed class RefusingConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection OpenConnection() => throw new InvalidOperationException("Unexpected SQL access.");
        public Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected SQL access.");
    }

    private sealed class FixedClock : IUtcClock
    {
        public DateTime UtcNow => new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    }
}
