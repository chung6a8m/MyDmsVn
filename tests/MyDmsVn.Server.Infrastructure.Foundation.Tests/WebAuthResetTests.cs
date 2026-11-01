using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class WebAuthResetTests
{
    [SqlServerFact]
    public async Task Explicit_web_reset_clears_active_consumed_revoked_families_and_preserves_journal_trigger_and_owner()
    {
        var db = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(FrozenWeb11Migration.Install(db.ConnectionString).Successful);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RespawnResetProfile.CreateAsync(db));
            var profile = await RespawnResetProfile.CreateAsync(db, web: true);
            for (var iteration=0; iteration<2; iteration++)
            {
                using (var c = new SqlConnection(db.ConnectionString))
                {
                    await c.ExecuteAsync(@"INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'web',N'WEB',N'Web',N'Local',N'hash',N'',N'BCrypt');
DECLARE @s uniqueidentifier=NEWID(),@n datetime2=SYSUTCDATETIME(),@a binary(32)=HASHBYTES('SHA2_256','parent'),@b binary(32)=HASHBYTES('SHA2_256','child');
INSERT dbo.WebAuthSessions VALUES(@s,1,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);
INSERT dbo.WebRefreshTokens VALUES(@b,@s,@n,DATEADD(day,7,@n),NULL,NULL,NULL);
INSERT dbo.WebRefreshTokens VALUES(@a,@s,@n,DATEADD(day,7,@n),@n,@b,@n);
UPDATE dbo.Users SET IsActive=0; UPDATE dbo.Users SET IsActive=1;
SET @s=NEWID(); INSERT dbo.WebAuthSessions VALUES(@s,1,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL);
INSERT dbo.WebRefreshTokens VALUES(HASHBYTES('SHA2_256','active-session'),@s,@n,DATEADD(day,7,@n),NULL,NULL,NULL);");
                    Assert.Equal(1, await c.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.WebAuthSessions WHERE RevokedAtUtc IS NOT NULL;"));
                }
                await profile.ResetAsync(); await profile.VerifyAsync();
                using var check = new SqlConnection(db.ConnectionString); await check.OpenAsync();
                Assert.Equal(0, await check.QuerySingleAsync<int>("SELECT (SELECT COUNT(*) FROM dbo.WebAuthSessions)+(SELECT COUNT(*) FROM dbo.WebRefreshTokens)+(SELECT COUNT(*) FROM dbo.Users);"));
                Assert.Equal(11, await check.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.SchemaVersions;"));
                Assert.Equal(3, await check.QuerySingleAsync<int>("SELECT COUNT(*) FROM sys.triggers WHERE is_ms_shipped=0 AND is_disabled=0;"));
                await db.VerifyOwnershipAsync(check, CancellationToken.None);
            }
            Assert.False(profile.IsPoisoned);
        }
        catch (Exception error) { scenarioFailure = error; throw; }
        finally
        {
            try { await db.DisposeAsync(); }
            catch (Exception cleanup) when (scenarioFailure is not null)
            {
                throw new AggregateException(scenarioFailure, cleanup);
            }
        }
    }
}
