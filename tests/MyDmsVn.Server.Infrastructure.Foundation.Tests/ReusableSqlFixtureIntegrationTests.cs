using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Testing.Sql;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

// Dedicated fresh allocations: these probes challenge the reset boundary itself.
public sealed class ReusableSqlFixtureIntegrationTests
{
    private static readonly string[] MutableTables =
    {
        "Users", "Roles", "UserRoles", "RolePermissions", "UserPermissions",
        "SystemSecuritySettings", "Products", "Warehouses", "Employees", "Customers",
        "GoodsReceipts", "GoodsReceiptLines", "StockLedger", "StockBalances", "SecurityAuditEvents",
        "SalesReceipts", "SalesReceiptLines", "StockValuationEntries",
    };

    private const string SeedSql = @"
DECLARE @u int, @r int, @w int, @p int, @e int, @g bigint, @l bigint, @c int, @s bigint;
INSERT dbo.Users (Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm)
VALUES (N'probe',N'PROBE',N'Probe',N'Local',N'hash',N'',N'BCrypt');
SET @u=SCOPE_IDENTITY();
INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'probe',N'PROBE'); SET @r=SCOPE_IDENTITY();
INSERT dbo.UserRoles(UserId,RoleId) VALUES(@u,@r);
INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@r,N'Catalog.Products.Read');
INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) VALUES(@u,N'Catalog.Products.Read',0);
INSERT dbo.SystemSecuritySettings(Id,SuperuserUserId,BoundNormalizedUsername) VALUES(1,@u,N'PROBE');
INSERT dbo.Warehouses(Code,Name) VALUES(N'W',N'Warehouse'); SET @w=SCOPE_IDENTITY();
INSERT dbo.Products(Code,Name,Unit) VALUES(N'P',N'Product',N'EA'); SET @p=SCOPE_IDENTITY();
INSERT dbo.Employees(Code,Name,UserId) VALUES(N'E',N'Employee',@u); SET @e=SCOPE_IDENTITY();
INSERT dbo.Customers(Code,Name) VALUES(N'C',N'Customer'); SET @c=SCOPE_IDENTITY();
INSERT dbo.GoodsReceipts(ReceiptNo,ReceiptDate,WarehouseId,EmployeeId,CreatedByUserId)
VALUES(N'probe-receipt','20261018',@w,@e,@u); SET @g=SCOPE_IDENTITY();
INSERT dbo.GoodsReceiptLines(ReceiptId,[LineNo],ProductId,Quantity,UnitCost,CreatedByUserId)
VALUES(@g,1,@p,2,3,@u); SET @l=SCOPE_IDENTITY();
INSERT dbo.StockLedger(WarehouseId,ProductId,DocumentType,DocumentId,DocumentLineId,QuantityChange,PostedAtUtc,PostedByUserId)
VALUES(@w,@p,'GoodsReceipt',@g,@l,2,SYSUTCDATETIME(),@u);
INSERT dbo.StockValuationEntries(LedgerId,ValueChange) VALUES(SCOPE_IDENTITY(),6);
INSERT dbo.StockBalances(WarehouseId,ProductId,Quantity,InventoryValue) VALUES(@w,@p,2,6);
INSERT dbo.SalesReceipts(SaleNo,SaleDate,WarehouseId,EmployeeId,CustomerId,CreatedByUserId)
VALUES(N'probe-sale','20261018',@w,@e,@c,@u); SET @s=SCOPE_IDENTITY();
INSERT dbo.SalesReceiptLines(SaleId,[LineNo],ProductId,Qty,Unit,IsGift,UnitPrice,LineAmount,CreatedByUserId)
VALUES(@s,1,@p,1,N'EA',0,5,5,@u);
INSERT dbo.SecurityAuditEvents(ActorUserId,TargetType,TargetId,Operation,CorrelationId)
VALUES(@u,N'User',@u,N'Probe',NEWID());";

    [SqlServerFact]
    public async Task Fresh_and_reuse_have_equivalent_sales_and_value_contamination_boundaries()
    {
        foreach (var mode in new[] { SqlFixtureRunMode.Fresh, SqlFixtureRunMode.Reuse })
        {
            var fixture = ReusableSqlFixture.CreateApplication(mode);
            try
            {
                for (var iteration = 0; iteration < 2; iteration++)
                {
                    var scope = await fixture.BeginTestAsync("SalesValueContamination", async (owned, _) =>
                    {
                        using var connection = new SqlConnection(owned.ConnectionString);
                        foreach (var table in MutableTables)
                        {
                            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT_BIG(*) FROM dbo.[" + table + "];"));
                        }
                        await AssertTriggerAsync(connection);
                        Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.SalesReceiptNumberSequence;"));
                        await connection.ExecuteAsync(SeedSql);
                    });
                    try
                    {
                        using var connection = new SqlConnection(scope.ConnectionString);
                        foreach (var table in MutableTables)
                        {
                            Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT COUNT_BIG(*) FROM dbo.[" + table + "];"));
                        }
                        Assert.Equal(0, await connection.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM dbo.StockValuationEntries v LEFT JOIN dbo.StockLedger l ON l.LedgerId=v.LedgerId WHERE l.LedgerId IS NULL;"));
                    }
                    finally { await scope.DisposeAsync(); }
                }
                Assert.False(fixture.IsPoisoned);
            }
            finally { await fixture.DisposeAsync(); }
        }
    }

    [SqlServerFact]
    public Task Empty_ledger_reset_restores_append_only_trigger() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database);
        await profile.ResetAsync();
        using var connection = new SqlConnection(database.ConnectionString);
        await AssertTriggerAsync(connection);
        Assert.False(profile.IsPoisoned);
    });

    [SqlServerFact]
    public Task Reset_clears_all_mutable_rows_preserves_journal_and_owner() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database);
        using var connection = new SqlConnection(database.ConnectionString);
        var journal = await ReadJournalAsync(connection);
        var owner = await ReadOwnerAsync(connection);
        await connection.ExecuteAsync(SeedSql);
        foreach (var table in MutableTables)
        {
            Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT COUNT_BIG(*) FROM dbo.[" + table + "];"));
        }
        await profile.ResetAsync();
        foreach (var table in MutableTables)
        {
            Assert.Equal(0L, await connection.QuerySingleAsync<long>("SELECT COUNT_BIG(*) FROM dbo.[" + table + "];"));
        }
        Assert.Equal(journal, await ReadJournalAsync(connection));
        Assert.Equal(owner, await ReadOwnerAsync(connection));
        await AssertTriggerAsync(connection);
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1;"));
        Assert.Equal(0, await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1;"));
    });

    [SqlServerFact]
    public Task Repeated_reset_reseeds_actual_identities_and_sequence() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database);
        using var connection = new SqlConnection(database.ConnectionString);
        for (var iteration = 0; iteration < 3; iteration++)
        {
            await profile.ResetAsync(); // Includes never-populated and repeated empty reset.
            await profile.ResetAsync();
            await connection.ExecuteAsync(SeedSql);
            var identityValues = (await connection.QueryAsync<decimal>(@"
SELECT CONVERT(decimal(38,0), i.last_value) FROM sys.identity_columns i
JOIN sys.tables t ON t.object_id=i.object_id
WHERE t.is_ms_shipped=0 AND SCHEMA_NAME(t.schema_id)=N'dbo' AND t.name<>N'SchemaVersions'
ORDER BY i.object_id;")).ToArray();
            Assert.Equal(15, identityValues.Length);
            Assert.All(identityValues, value => Assert.Equal(1m, value));
            Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;"));
            Assert.Equal(2L, await connection.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.GoodsReceiptNumberSequence;"));
            Assert.Equal(1L, await connection.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.SalesReceiptNumberSequence;"));
            Assert.Equal(2L, await connection.QuerySingleAsync<long>("SELECT NEXT VALUE FOR dbo.SalesReceiptNumberSequence;"));
        }
    });

    [SqlServerFact]
    public Task Reset_restores_constraints_and_rejects_schema_drift() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database);
        using var connection = new SqlConnection(database.ConnectionString);
        await connection.ExecuteAsync(SeedSql);
        await profile.ResetAsync();
        var error = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(
            "INSERT dbo.UserRoles(UserId,RoleId) VALUES(2147483647,2147483647);"));
        Assert.Equal(547, error.Number);
        await connection.ExecuteAsync("ALTER TABLE dbo.Products ADD UnexpectedColumn int NULL;");
        await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
        Assert.True(profile.IsPoisoned);
        await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
    });

    [SqlServerFact]
    public async Task Unknown_schema_trigger_principal_and_disabled_constraints_prevent_reset()
    {
        // Fresh profile for each independent challenge, retaining each original allocation.
        var changes = new[]
        {
            "CREATE TRIGGER ddl_probe ON DATABASE FOR ALTER_TABLE AS RETURN;",
            "CREATE SCHEMA unexpected;",
            "CREATE TRIGGER dbo.TR_Unexpected ON dbo.Users AFTER INSERT AS RETURN;",
            "CREATE USER reset_probe WITHOUT LOGIN;",
            "GRANT SELECT ON dbo.Users TO public;",
            "ALTER TABLE dbo.UserRoles NOCHECK CONSTRAINT FK_UserRoles_Users;",
            "ALTER TABLE dbo.UserRoles NOCHECK CONSTRAINT FK_UserRoles_Users; ALTER TABLE dbo.UserRoles CHECK CONSTRAINT FK_UserRoles_Users;",
            "UPDATE dbo.SchemaVersions SET Applied=DATEADD(second,1,Applied);",
        };
        // Each independent change owns a fresh migrated allocation.
        foreach (var change in changes)
        {
            await WithDatabaseAsync(async owned =>
            {
                var profile = await RespawnResetProfile.CreateAsync(owned);
                using var connection = new SqlConnection(owned.ConnectionString);
                await connection.ExecuteAsync(SeedSql);
                await connection.ExecuteAsync(change);
                await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
                Assert.True(profile.IsPoisoned);
                Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            });
        }
    }

    [SqlServerFact]
    public async Task Initialization_rejects_unreviewed_schema_and_trigger_before_mutation()
    {
        foreach (var change in new[]
        {
            "CREATE TRIGGER dbo.TR_Unexpected ON dbo.Users AFTER DELETE AS RETURN;",
            "CREATE PROCEDURE dbo.UnexpectedProcedure AS SELECT 1;",
            "ALTER TABLE dbo.Products ADD CONSTRAINT CK_Unexpected CHECK(ProductId>0);",
            "GRANT SELECT ON dbo.Users TO public;",
            "CREATE SCHEMA unexpected;",
            "CREATE USER reset_probe WITHOUT LOGIN;",
            "ALTER TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger INSTEAD OF UPDATE,DELETE AS RETURN;",
            "DISABLE TRIGGER dbo.TR_StockLedger_AppendOnly ON dbo.StockLedger;",
            "ALTER TRIGGER dbo.TR_StockValuationEntries_AppendOnly ON dbo.StockValuationEntries INSTEAD OF UPDATE,DELETE AS RETURN;",
            "DISABLE TRIGGER dbo.TR_StockValuationEntries_AppendOnly ON dbo.StockValuationEntries;",
        })
        {
            await WithDatabaseAsync(async database =>
            {
                using var connection = new SqlConnection(database.ConnectionString);
                await connection.ExecuteAsync(SeedSql);
                await connection.ExecuteAsync(change);
                await Assert.ThrowsAsync<InvalidOperationException>(() => RespawnResetProfile.CreateAsync(database));
                Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT COUNT(*) FROM dbo.Users;"));
            });
        }
    }

    [SqlServerFact]
    public Task Ownership_change_prevents_reset_ddl() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database);
        using var connection = new SqlConnection(database.ConnectionString);
        var marker = await ReadOwnerAsync(connection);
        try
        {
            await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership', @value=N'changed';");
            await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
            Assert.True(profile.IsPoisoned);
            await AssertTriggerAsync(connection);
        }
        finally
        {
            await connection.ExecuteAsync("EXEC sys.sp_updateextendedproperty @name=N'MyDmsVn.TestOwnership', @value=@marker;", new { marker });
        }
    });

    [SqlServerFact]
    public Task Reset_failure_restores_trigger_and_poison_rejects_later_admission() => WithDatabaseAsync(async database =>
    {
        var fail = false;
        var profile = await RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: async (connection, token) =>
        {
            if (fail)
            {
                using var command = new SqlCommand("DELETE dbo.UserPermissions; THROW 51099, 'Expected reset fault.', 1;", connection);
                await command.ExecuteNonQueryAsync(token);
            }
        });
        using var verification = new SqlConnection(database.ConnectionString);
        await verification.ExecuteAsync(SeedSql);
        fail = true;
        await Assert.ThrowsAsync<SqlException>(() => profile.ResetAsync());
        Assert.True(profile.IsPoisoned);
        await AssertTriggerAsync(verification);
        await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
    });

    [SqlServerFact]
    public Task Trigger_restoration_failure_preserves_both_errors_and_poisons_profile() => WithDatabaseAsync(async database =>
    {
        var fail = false;
        var profile = await RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: async (connection, token) =>
        {
            if (fail)
            {
                using var command = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_StockValuationEntries_AppendOnly') AND is_disabled=1) BEGIN DROP TRIGGER dbo.TR_StockLedger_AppendOnly; THROW 51099, 'Expected reset fault.', 1; END;", connection);
                await command.ExecuteNonQueryAsync(token);
            }
        });
        fail = true;
        var error = await Assert.ThrowsAsync<AggregateException>(() => profile.ResetAsync());
        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Contains(error.InnerExceptions, e => e is SqlException sql && sql.Number == 51099);
        Assert.True(profile.IsPoisoned);
        // A damaged ledger trigger must not prevent restoration of its independent companion.
        using var observer = new SqlConnection(database.ConnectionString);
        await AssertValuationTriggerAsync(observer);
        await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
    });

    [SqlServerFact]
    public async Task Cancellation_between_each_trigger_disable_restores_both_protections()
    {
        foreach (var cancelAfter in new[] { 1, 2 })
        {
            await WithDatabaseAsync(async database =>
            {
                var observe = false;
                var disabled = 0;
                using var cancellation = new CancellationTokenSource();
                var profile = await RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: (_, token) =>
                {
                    if (observe && ++disabled == cancelAfter)
                    {
                        cancellation.Cancel();
                        token.ThrowIfCancellationRequested();
                    }
                    return Task.CompletedTask;
                });
                observe = true;
                await Assert.ThrowsAnyAsync<Exception>(() => profile.ResetAsync(cancellation.Token));
                Assert.Equal(cancelAfter, disabled);
                Assert.True(profile.IsPoisoned);
                using var observer = new SqlConnection(database.ConnectionString);
                await AssertTriggerAsync(observer);
                await Assert.ThrowsAsync<InvalidOperationException>(() => profile.VerifyAsync());
            });
        }
    }

    [SqlServerFact]
    public Task Companion_restoration_failure_does_not_leave_ledger_disabled() => WithDatabaseAsync(async database =>
    {
        var fail = false;
        var profile = await RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: async (connection, token) =>
        {
            if (fail)
            {
                using var command = new SqlCommand("IF EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_StockValuationEntries_AppendOnly') AND is_disabled=1) BEGIN DROP TRIGGER dbo.TR_StockValuationEntries_AppendOnly; THROW 51099, 'Expected reset fault.', 1; END;", connection);
                await command.ExecuteNonQueryAsync(token);
            }
        });
        fail = true;
        var error = await Assert.ThrowsAsync<AggregateException>(() => profile.ResetAsync());
        Assert.Contains(error.InnerExceptions, e => e is SqlException sql && sql.Number == 51099);
        using var observer = new SqlConnection(database.ConnectionString);
        Assert.Equal(0, await observer.QuerySingleAsync<int>(
            "SELECT is_disabled FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_StockLedger_AppendOnly');"));
        var denied = await Assert.ThrowsAsync<SqlException>(() => observer.ExecuteAsync("DELETE dbo.StockLedger;"));
        Assert.Equal(51000, denied.Number);
        Assert.True(profile.IsPoisoned);
    });

    [SqlServerFact]
    public Task Reset_timeout_terminates_owned_operation_before_returning() => WithDatabaseAsync(async database =>
    {
        var profile = await RespawnResetProfile.CreateAsync(database, commandTimeoutSeconds: 1);
        using var holder = new SqlConnection(database.ConnectionString);
        await holder.OpenAsync();
        using var transaction = holder.BeginTransaction();
        await holder.ExecuteAsync("SELECT COUNT(*) FROM dbo.Users WITH(TABLOCKX,HOLDLOCK);", transaction: transaction);
        try
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => profile.ResetAsync());
            Assert.Equal(-2, error.Number);
            Assert.True(profile.IsPoisoned);
            using var observer = new SqlConnection(database.ConnectionString);
            Assert.Equal(0, await observer.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.dm_exec_requests WHERE database_id=DB_ID()
AND session_id<>@@SPID AND session_id<>@holder;", new { holder = holder.ServerProcessId }));
            await AssertTriggerAsync(observer);
        }
        finally { transaction.Rollback(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => profile.ResetAsync());
    });

    [SqlServerFact]
    public Task Cancellation_closes_and_drains_the_actual_reset_connection() => WithDatabaseAsync(async database =>
    {
        var observe = false;
        var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var profile = await RespawnResetProfile.CreateAsync(database, afterTriggerDisabled: (connection, _) =>
        {
            if (observe) { session.TrySetResult(connection.ServerProcessId); }
            return Task.CompletedTask;
        });
        using var holder = new SqlConnection(database.ConnectionString);
        await holder.OpenAsync();
        using var transaction = holder.BeginTransaction();
        await holder.ExecuteAsync("SELECT COUNT(*) FROM dbo.Users WITH(TABLOCKX,HOLDLOCK);", transaction: transaction);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        observe = true;
        var reset = profile.ResetAsync(cancellation.Token);
        try
        {
            // Cancel only after the real Respawn delete is observed waiting on our lock.
            Assert.Same(session.Task, await Task.WhenAny(session.Task, Task.Delay(TimeSpan.FromSeconds(10))));
            var resetSession = await session.Task;
            using var observer = new SqlConnection(database.ConnectionString);
            // Keep this session open: SQL oracle auto-close/reopen could reuse the reset SPID
            // and make the observer count its own request after cancellation.
            await observer.OpenAsync();
            Assert.NotEqual(resetSession, observer.ServerProcessId);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            var blocked = false;
            while (DateTime.UtcNow < deadline && !reset.IsCompleted)
            {
                blocked = await observer.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@resetSession
AND blocking_session_id=@holder;", new { resetSession, holder = holder.ServerProcessId }, commandTimeout: 5) == 1;
                if (blocked) { break; }
                await Task.Delay(25);
            }
            Assert.True(blocked, "The reset must reach the held SQL lock before cancellation.");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<Exception>(() => reset);
            Assert.True(profile.IsPoisoned);
            Assert.Equal(0, await observer.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id=@resetSession;", new { resetSession }));
            await AssertTriggerAsync(observer);
        }
        finally
        {
            cancellation.Cancel();
            // Drain even when observation/assertion fails before cancellation was requested.
            await Record.ExceptionAsync(() => reset);
            transaction.Rollback();
        }
    });

    private static Task<string> ReadJournalAsync(SqlConnection connection) => connection.QuerySingleAsync<string>(
        "SELECT CONVERT(nvarchar(max),(SELECT Id,ScriptName,Applied FROM dbo.SchemaVersions ORDER BY Id FOR XML RAW, BINARY BASE64));");

    private static Task<string> ReadOwnerAsync(SqlConnection connection) => connection.QuerySingleAsync<string>(
        "SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership';");

    private static async Task AssertTriggerAsync(SqlConnection connection)
    {
        await AssertValuationTriggerAsync(connection);
        Assert.Equal(1, await connection.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_StockLedger_AppendOnly')
AND parent_id=OBJECT_ID(N'dbo.StockLedger') AND is_disabled=0 AND is_instead_of_trigger=1;"));
        var definition = await connection.QuerySingleAsync<string>("SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_StockLedger_AppendOnly'));" );
        Assert.Contains("THROW 51000", definition, StringComparison.Ordinal);
        var error = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync("DELETE dbo.StockLedger;"));
        Assert.Equal(51000, error.Number);
    }

    private static async Task AssertValuationTriggerAsync(SqlConnection connection)
    {
        Assert.Equal(1, await connection.QuerySingleAsync<int>(@"
SELECT COUNT(*) FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.TR_StockValuationEntries_AppendOnly')
AND parent_id=OBJECT_ID(N'dbo.StockValuationEntries') AND is_disabled=0 AND is_instead_of_trigger=1;"));
        foreach (var sql in new[] { "DELETE dbo.StockValuationEntries;", "UPDATE dbo.StockValuationEntries SET ValueChange=ValueChange;" })
        {
            var error = await Assert.ThrowsAsync<SqlException>(() => connection.ExecuteAsync(sql));
            Assert.Equal(51000, error.Number);
        }
    }

    private static async Task WithDatabaseAsync(Func<SqlTestDatabase, Task> scenario)
    {
        var database = await SqlTestDatabase.CreateAsync(
            Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            var migration = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            Assert.True(migration.Successful, migration.Error?.ToString());
            await scenario(database);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try
            {
                await database.DisposeAsync();
                using var master = new SqlConnection(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable));
                Assert.Equal(0, await master.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM sys.databases WHERE name=@name;", new { name = database.DatabaseName }));
            }
            catch (Exception cleanup) when (failure != null)
            {
                throw new AggregateException("Reset probe and owned cleanup failed.", failure, cleanup);
            }
        }
    }
}
