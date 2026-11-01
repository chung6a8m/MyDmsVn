using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.Operations;
using MyDmsVn.Testing.Sql;
using Xunit;
namespace MyDmsVn.Server.Infrastructure.IntegrationTests;
public sealed class WebPrincipalProvisioningTests
{
    private static Task<SqlTestDatabase> Create() => SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
    private static string Script(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "deployment", "sql", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(name);
    }
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public async Task Separate_web_grants_work_and_desktop_cannot_read_auth_or_migrate()
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString); await c.OpenAsync();
            await c.ExecuteAsync("CREATE USER WebProbe WITHOUT LOGIN; CREATE USER DesktopProbe WITHOUT LOGIN; CREATE USER MigrationProbe WITHOUT LOGIN;");
            await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync(Script("Provision-MyDmsVnWebPrincipals.sql").Replace("$(WebRuntimeUser)", "WebProbe").Replace("$(MigrationUser)", "WebProbe")));
            await c.ExecuteAsync(Script("Provision-MyDmsVnPrincipals.sql").Replace("$(RuntimeUser)", "DesktopProbe").Replace("$(MigrationUser)", "MigrationProbe"));
            await c.ExecuteAsync(Script("Provision-MyDmsVnWebPrincipals.sql").Replace("$(WebRuntimeUser)", "WebProbe").Replace("$(MigrationUser)", "MigrationProbe"));
            // Provisioning is idempotent and startup accepts the actual effective grants.
            await c.ExecuteAsync(Script("Provision-MyDmsVnWebPrincipals.sql").Replace("$(WebRuntimeUser)", "WebProbe").Replace("$(MigrationUser)", "MigrationProbe"));
            await c.ExecuteAsync("INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm) VALUES(N'web',N'WEB',N'Web',N'Local',N'hash',N'',N'BCrypt');");
            await c.ExecuteAsync("EXECUTE AS USER=N'WebProbe';");
            try
            {
                await new SqlWebRuntimePrincipalValidator().ValidateConnectionAsync(c, CancellationToken.None);
                await Assert.ThrowsAsync<DesktopStartupException>(() => new SqlRuntimePrincipalValidator().ValidateConnectionAsync(c, CancellationToken.None));
                await c.ExecuteAsync("UPDATE dbo.Users SET PasswordHash=N'rehashed',PasswordSalt=N'',PasswordAlgorithm=N'BCrypt',UpdatedAtUtc=SYSUTCDATETIME(),UpdatedByUserId=UserId;");
                Assert.True(await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.SalesReceiptNumberSequence;") > 0);
                Assert.True(await c.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;") > 0);
                await c.ExecuteAsync("DECLARE @s uniqueidentifier=NEWID(),@n datetime2=SYSUTCDATETIME(); INSERT dbo.WebAuthSessions VALUES(@s,1,@n,DATEADD(day,30,@n),DATEADD(day,7,@n),NULL); INSERT dbo.WebRefreshTokens VALUES(HASHBYTES('SHA2_256','probe'),@s,@n,DATEADD(day,7,@n),NULL,NULL,NULL); UPDATE dbo.WebAuthSessions SET RevokedAtUtc=@n WHERE SessionId=@s;");
                foreach (var denied in new[] { "CREATE TABLE dbo.Forbidden(Id int);", "UPDATE dbo.GoodsReceiptLines SET Quantity=1;", "DELETE dbo.GoodsReceipts;", "UPDATE dbo.StockLedger SET QuantityChange=1;", "DELETE dbo.StockLedger;", "UPDATE dbo.StockValuationEntries SET ValueChange=1;", "DELETE dbo.StockValuationEntries;", "UPDATE dbo.Users SET IsActive=0;", "INSERT dbo.Products(Code,Name,Unit) VALUES(N'blocked',N'blocked',N'EA');", "DELETE dbo.WebRefreshTokens;", "UPDATE dbo.SchemaVersions SET Applied=SYSUTCDATETIME();", "ALTER ROLE db_owner ADD MEMBER WebProbe;", "EXEC dbo.ApplySecurityAdministration;" })
                    await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync(denied));
            }
            finally { await c.ExecuteAsync("REVERT;"); }
            await c.ExecuteAsync("EXECUTE AS USER=N'DesktopProbe';");
            try
            {
                await new SqlRuntimePrincipalValidator().ValidateConnectionAsync(c, CancellationToken.None);
                await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync("SELECT * FROM dbo.WebAuthSessions;"));
                await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync("UPDATE dbo.WebRefreshTokens SET RevokedAtUtc=SYSUTCDATETIME();"));
            }
            finally { await c.ExecuteAsync("REVERT;"); }
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
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_User_IsActive_column_is_refused() => RejectExcess("GRANT UPDATE(IsActive) ON dbo.Users TO WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_unlisted_column_is_refused() => RejectExcess("GRANT SELECT(ActorUserId) ON dbo.SecurityAuditEvents TO WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_schema_grant_is_refused() => RejectExcess("GRANT SELECT ON SCHEMA::dbo TO WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_owner_role_is_refused() => RejectExcess("ALTER ROLE db_owner ADD MEMBER WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_receipt_line_update_is_refused() => RejectExcess("GRANT UPDATE ON dbo.GoodsReceiptLines TO WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_receipt_line_column_update_is_refused() => RejectExcess("GRANT UPDATE(Quantity) ON dbo.GoodsReceiptLines TO WebProbe;");
    [SqlServerFact(SqlTestCategory.SecurityProvisioning)]
    public Task Excess_receipt_header_delete_is_refused() => RejectExcess("GRANT DELETE ON dbo.GoodsReceipts TO WebProbe;");
    private async Task RejectExcess(string grant)
    {
        var db = await Create();
        Exception? scenarioFailure = null;
        try
        {
            Assert.True(new DatabaseMigrationRunner().MigrateWeb(db.ConnectionString).Successful);
            using var c = new SqlConnection(db.ConnectionString); await c.OpenAsync();
            await c.ExecuteAsync("CREATE USER WebProbe WITHOUT LOGIN; CREATE USER MigrationProbe WITHOUT LOGIN;");
            var script = Script("Provision-MyDmsVnWebPrincipals.sql").Replace("$(WebRuntimeUser)", "WebProbe").Replace("$(MigrationUser)", "MigrationProbe");
            await c.ExecuteAsync(script); await c.ExecuteAsync(grant); await c.ExecuteAsync("EXECUTE AS USER=N'WebProbe';");
            try { Assert.Equal("Security.WebRuntimePrincipalOverprivileged", (await Assert.ThrowsAsync<DesktopStartupException>(() => new SqlWebRuntimePrincipalValidator().ValidateConnectionAsync(c, CancellationToken.None))).Code); }
            finally { await c.ExecuteAsync("REVERT;"); }
            await Assert.ThrowsAsync<SqlException>(() => c.ExecuteAsync(script));
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
