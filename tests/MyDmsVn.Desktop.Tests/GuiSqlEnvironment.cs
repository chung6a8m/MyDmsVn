﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Identity;
using MyDmsVn.Server.Infrastructure.IntegrationTests;
using MyDmsVn.Server.Infrastructure.Operations;
using Newtonsoft.Json;
using Xunit;

namespace MyDmsVn.Desktop.Tests;

// Infrastructure for the existing GUI owner, never a SQL runner or product service.
internal sealed class GuiSqlEnvironment : IDisposable
{
    private readonly SqlTestDatabase _database;
    private readonly string _administration;
    private readonly List<Login> _logins = new List<Login>();
    private bool _uncertainLogin;
    private string? _uncertainLoginName;
    private object? _allocationIdentity;
    private bool _disposed;
    private string _runtime = "";
    private string _migration = "";
    private string _oracle = "";
    private readonly string _resourceFile;
    public string OwnerPassword { get; } = Guid.NewGuid().ToString("N") + "aA9!";
    public const string OwnerUsername = "gui.p5.owner";
    public string ActorPassword { get; } = Guid.NewGuid().ToString("N") + "aA9!";

    public async Task SeedNegativeActorsAsync()
    {
        using var connection = new SqlConnection(_migration);
        await connection.OpenAsync();
        using var command = new SqlCommand(
            "INSERT dbo.Users(Username,NormalizedUsername,DisplayName,Source,PasswordHash,PasswordSalt,PasswordAlgorithm,IsActive) VALUES " +
            "(N'gui.p5.disabled',N'GUI.P5.DISABLED',N'GUI P5 Disabled',N'Local',@hash,N'',N'BCrypt',0)," +
            "(N'gui.p5.deny',N'GUI.P5.DENY',N'GUI P5 Deny',N'Local',@hash,N'',N'BCrypt',1); " +
            "INSERT dbo.Roles(RoleName,NormalizedRoleName) VALUES(N'GUI P5 Writer',N'GUI P5 WRITER'); " +
            "DECLARE @role int=SCOPE_IDENTITY(),@user int=(SELECT UserId FROM dbo.Users WHERE NormalizedUsername=N'GUI.P5.DENY'); " +
            "INSERT dbo.UserRoles(UserId,RoleId) VALUES(@user,@role); " +
            "INSERT dbo.RolePermissions(RoleId,PermissionKey) VALUES(@role,N'Catalog.Products.Read'),(@role,N'Catalog.Products.Write');",
            connection) { CommandTimeout = 15 };
        command.Parameters.AddWithValue("@hash", new BcryptPasswordHasher(10).Hash(ActorPassword));
        await command.ExecuteNonQueryAsync();
    }

    public async Task DenyProductWriteAsync()
    {
        await ExecuteAsync(_migration,
            "INSERT dbo.UserPermissions(UserId,PermissionKey,Granted) SELECT UserId,N'Catalog.Products.Write',0 " +
            "FROM dbo.Users WHERE NormalizedUsername=N'GUI.P5.DENY';");
    }

    public async Task AssertDeniedAndNoMutationAsync()
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Users u JOIN dbo.UserRoles ur ON ur.UserId=u.UserId " +
            "JOIN dbo.RolePermissions rp ON rp.RoleId=ur.RoleId JOIN dbo.UserPermissions up ON up.UserId=u.UserId " +
            "AND up.PermissionKey=rp.PermissionKey WHERE u.NormalizedUsername=N'GUI.P5.DENY' " +
            "AND rp.PermissionKey=N'Catalog.Products.Write' AND up.Granted=0;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Users WHERE NormalizedUsername=N'GUI.P5.DISABLED' AND IsActive=0;"));
        foreach (var table in new[] { "Products", "GoodsReceipts", "GoodsReceiptLines", "StockLedger", "StockBalances" })
            Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo." + table + ";"));
    }

    public async Task AssertNoInventoryAsync()
    {
        foreach (var table in new[] { "GoodsReceipts", "GoodsReceiptLines", "StockLedger", "StockBalances" })
            Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo." + table + ";"));
    }

    public Task<string> DescribeSqlEngineAsync() => ScalarAsync<string>(_administration,
        "SELECT N'SQL_ENGINE=' + CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) + N';EDITION=' + CONVERT(nvarchar(128),SERVERPROPERTY('Edition')); ");

    public async Task AssertChangedLookupsAsync()
    {
        await AssertSavedDraftWithoutPostingAsync();
        foreach (var entry in new[] { new[] { "Products", "GUI-PROD", "UIA Product Changed" }, new[] { "Warehouses", "GUI-WH", "UIA Warehouse Changed" }, new[] { "Employees", "GUI-EMP", "UIA Employee Changed" } })
            Assert.Equal(1, await ScalarAsync<int>(_oracle,
                "SELECT COUNT(*) FROM dbo." + entry[0] + " WHERE Code=@code AND Name=@name AND IsActive=0;",
                new SqlParameter("@code", entry[1]), new SqlParameter("@name", entry[2])));
    }

    public async Task AssertConcurrentWinnerAsync()
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Status=N'Draft' AND Note=N'GUI concurrent winner' AND PostedAtUtc IS NULL;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE Quantity=3.125 AND UnitCost=4;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances;"));
    }

    private const string PostTimeoutTrigger = "CREATE TRIGGER dbo.GuiP5_PostTimeout ON dbo.GoodsReceipts AFTER UPDATE AS " +
        "BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted i JOIN deleted d ON d.ReceiptId=i.ReceiptId " +
        "WHERE i.Status=N'Posted' AND d.Status=N'Draft') WAITFOR DELAY '00:00:45'; END;";
    private int _postTimeoutTriggerId;

    public async Task InstallOwnedPostTimeoutAsync()
    {
        await VerifySeparationAsync();
        await ExecuteAsync(_migration, PostTimeoutTrigger);
        _postTimeoutTriggerId = await ScalarAsync<int>(_migration, "SELECT OBJECT_ID(N'dbo.GuiP5_PostTimeout',N'TR');");
        Assert.True(_postTimeoutTriggerId > 0);
    }

    public async Task<bool> OwnedRuntimeIsWaitingInsideSqlAsync()
    {
        return await ScalarAsync<int>(_administration,
            "SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id " +
            "WHERE r.database_id=DB_ID(@database) AND s.login_name=@login AND r.wait_type=N'WAITFOR';",
            new SqlParameter("@database", _database.DatabaseName),
            new SqlParameter("@login", new SqlConnectionStringBuilder(_runtime).UserID)) > 0;
    }

    public async Task AssertTimedOutDraftAndRemoveFaultAsync()
    {
        await AssertSavedDraftWithoutPostingAsync();
        using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await _database.VerifyOwnershipAsync(connection, CancellationToken.None);
        Assert.Equal(_postTimeoutTriggerId, await ScalarAsync<int>(_migration, "SELECT OBJECT_ID(N'dbo.GuiP5_PostTimeout',N'TR');"));
        Assert.Equal(PostTimeoutTrigger, await ScalarAsync<string>(_migration, "SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.GuiP5_PostTimeout',N'TR'));"));
        await ExecuteAsync(_migration, "DROP TRIGGER dbo.GuiP5_PostTimeout;");
    }

    public async Task AssertSavedDraftWithoutPostingAsync()
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.GoodsReceipts WHERE Status=N'Draft' AND Note=N'GUI edited' AND PostedAtUtc IS NULL;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines WHERE Quantity=3.125 AND UnitCost=4;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances;"));
    }

    private GuiSqlEnvironment(SqlTestDatabase database, string administration, string repository)
    {
        _database = database;
        _administration = administration;
        _resourceFile = Path.Combine(repository, "TestResults", "gui-p5-t03", "resources-" + Guid.NewGuid().ToString("N") + ".json");
    }

    public static Task<GuiSqlEnvironment> CreateAsync(string package)
    {
        GuiTestSafety.ThrowIfPoisoned();
        return CreateCoreAsync(package);
    }

    // Keep admission before the async state machine's SQL dependency/configuration work.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<GuiSqlEnvironment> CreateCoreAsync(string package)
    {
        var configured = Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured)) throw new InvalidOperationException("BLOCKED: designated non-production master connection is missing.");
        var builder = new SqlConnectionStringBuilder(configured)
        {
            Pooling = false, ConnectTimeout = 15, ConnectRetryCount = 0,
        };
        var repository = RepositoryRoot();
        var database = await SqlTestDatabase.CreateGuiAsync(builder.ConnectionString);
        var fixture = new GuiSqlEnvironment(database, builder.ConnectionString, repository);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fixture._resourceFile)!);
            await fixture.CaptureAllocationAsync();
            fixture.Record("Allocated");
            await fixture.ProvisionAsync(package, repository);
            return fixture;
        }
        catch (Exception initial)
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException("GUI setup and cleanup failed; inspect retained local resource evidence.", initial, cleanup); }
            ExceptionDispatchInfo.Capture(initial).Throw();
            throw;
        }
    }

    private async Task ProvisionAsync(string package, string repository)
    {
        var migration = await CreateLoginAsync("migration");
        var runtime = await CreateLoginAsync("runtime");
        var oracle = await CreateLoginAsync("oracle");
        foreach (var login in _logins)
            await ExecuteAsync(_database.ConnectionString, "CREATE USER [" + login.Name + "] FOR LOGIN [" + login.Name + "];");
        await ExecuteAsync(_database.ConnectionString, "ALTER ROLE [db_owner] ADD MEMBER [" + migration.Name + "];");
        _migration = Connection(migration);
        _runtime = Connection(runtime);
        _oracle = Connection(oracle);

        var migrator = Path.Combine(package, "admin-migrator-net48-win-x86", "MyDmsVn.Server.DbMigrator.exe");
        var start = new ProcessStartInfo(migrator) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(migrator)! };
        PackagedGuiAcceptanceTests.ClearSqlEnvironment(start);
        start.EnvironmentVariables["MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING"] = _migration;
        var result = await GuiOwnedProcess.RunAsync(start, TimeSpan.FromSeconds(90));
        Assert.True(result.ExitCode == 0, "Packaged migration failed; inspect local diagnostics.");

        var grants = File.ReadAllText(Path.Combine(repository, "deployment", "sql", "Provision-MyDmsVnPrincipals.sql"))
            .Replace("$(RuntimeUser)", runtime.Name).Replace("$(MigrationUser)", migration.Name);
        await ExecuteAsync(_database.ConnectionString, grants);
        await ExecuteAsync(_database.ConnectionString, "GRANT SELECT ON SCHEMA::dbo TO [" + oracle.Name + "];");

        // Invoke the shipped administrative bootstrap engine, with an in-memory secret.
        // Desktop still authenticates normally against the resulting protected binding.
        var assembly = Assembly.LoadFrom(migrator);
        var type = assembly.GetType("MyDmsVn.Server.DbMigrator.SecurityProvisioning.SuperuserBootstrapEngine", true)!;
        var engine = Activator.CreateInstance(type, new BcryptPasswordHasher(10))!;
        await (Task<int>)type.GetMethod("BootstrapAsync")!.Invoke(engine,
            new object[] { _migration, OwnerUsername, "GUI P5 Owner", OwnerPassword, DateTime.UtcNow, CancellationToken.None })!;
        await VerifySeparationAsync();
        Assert.Equal(BcryptPasswordHasher.AlgorithmName, await ScalarAsync<string>(_oracle,
            "SELECT PasswordAlgorithm FROM dbo.Users WHERE NormalizedUsername=N'GUI.P5.OWNER';"));
        Assert.True(new BcryptPasswordHasher(10).Verify(OwnerPassword, await ScalarAsync<string>(_oracle,
            "SELECT PasswordHash FROM dbo.Users WHERE NormalizedUsername=N'GUI.P5.OWNER';")), "Administrative bootstrap credentials did not verify.");
        Record("ProvisionedAndValidated");
    }

    private async Task<Login> CreateLoginAsync(string purpose)
    {
        var login = new Login("GuiP5_" + purpose + "_" + Guid.NewGuid().ToString("N"));
        // Generated name/password use only a closed character set, never caller SQL text.
        _uncertainLogin = true;
        _uncertainLoginName = login.Name;
        Record("PrincipalCreateAttempted");
        using (var connection = new SqlConnection(_administration))
        {
            await connection.OpenAsync();
            using var command = new SqlCommand("CREATE LOGIN [" + login.Name + "] WITH PASSWORD=N'" + login.Password +
                "', CHECK_POLICY=ON; SELECT principal_id,sid,create_date FROM sys.server_principals WHERE name=@name;", connection) { CommandTimeout = 15 };
            command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = login.Name;
            using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "Created principal identity could not be captured.");
            login.Id = reader.GetInt32(0);
            login.Sid = (byte[])reader.GetValue(1);
            login.Created = reader.GetDateTime(2);
        }
        _logins.Add(login);
        _uncertainLogin = false;
        _uncertainLoginName = null;
        Record("PrincipalIdentityCaptured");
        return login;
    }

    private string Connection(Login login) => new SqlConnectionStringBuilder(_administration)
    {
        InitialCatalog = _database.DatabaseName, IntegratedSecurity = false,
        UserID = login.Name, Password = login.Password, Pooling = false,
        ApplicationName = "MyDmsVn.GuiP5." + login.Name,
    }.ConnectionString;

    public async Task VerifySeparationAsync()
    {
        Assert.Equal(3, _logins.Select(login => login.Id).Distinct().Count());
        await new SqlRuntimePrincipalValidator().ValidateAsync(_runtime, CancellationToken.None);
        Assert.Equal(1, await ScalarAsync<int>(_runtime, "SELECT IS_ROLEMEMBER(N'MyDmsVnRuntimeRole');"));
        Assert.Equal(0, await ScalarAsync<int>(_runtime, "SELECT ISNULL(IS_ROLEMEMBER(N'db_owner'),0);") );
        Assert.Equal(0, await ScalarAsync<int>(_runtime,
            "SELECT COUNT(*) FROM sys.objects o INNER JOIN sys.schemas s ON o.schema_id=s.schema_id " +
            "WHERE o.is_ms_shipped=0 AND HAS_PERMS_BY_NAME(QUOTENAME(s.name)+N'.'+QUOTENAME(o.name),N'OBJECT',N'EXECUTE')=1 " +
            "AND NOT(s.name=N'dbo' AND o.name IN(N'ApplySecurityAdministration',N'ReadSecurityPermissionImportTarget',N'ApplySecurityPermissionImport'));"));
        Assert.Equal(9, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SchemaVersions;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.SystemSecuritySettings WHERE BoundNormalizedUsername=N'GUI.P5.OWNER';"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle,
            "SELECT HAS_PERMS_BY_NAME(N'dbo.Products',N'OBJECT',N'INSERT')+HAS_PERMS_BY_NAME(N'dbo.Products',N'OBJECT',N'UPDATE')+HAS_PERMS_BY_NAME(DB_NAME(),N'DATABASE',N'ALTER');"));
        foreach (var login in _logins) await VerifyLoginAsync(login);
        using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await _database.VerifyOwnershipAsync(connection, CancellationToken.None);
    }

    public void ConfigureRuntime(ProcessStartInfo start)
    {
        PackagedGuiAcceptanceTests.ClearSqlEnvironment(start);
        start.EnvironmentVariables["MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING"] = _runtime;
        start.EnvironmentVariables["Security__SuperuserUsername"] = OwnerUsername;
        start.EnvironmentVariables["MYDMSVN_GUI_P5_OWNER_PASSWORD"] = OwnerPassword;
        start.EnvironmentVariables["MYDMSVN_GUI_P5_EXPECTED_SCALE"] = Environment.GetEnvironmentVariable("MYDMSVN_GUI_P5_EXPECTED_SCALE") ?? "100";
    }

    public async Task AssertCatalogProductAsync()
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Products WHERE Code=N'GUI-PROD' AND Name=N'UIA Product Updated' AND Unit=N'pcs' AND IsActive=1;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Warehouses WHERE Code=N'GUI-WH' AND Name=N'UIA Warehouse Updated' AND Address=N'GUI address' AND IsActive=1;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Employees WHERE Code=N'GUI-EMP' AND Name=N'UIA Employee Updated' AND Phone=N'0900123456' AND UserId IS NULL AND IsActive=1;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.Customers WHERE Code=N'GUI-CUST' AND Name=N'UIA Customer Updated' AND Address=N'GUI address' AND Phone=N'0900123456' AND TaxCode=N'P5-TAX' AND IsActive=1;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
    }

    public async Task AssertPostedReceiptAsync()
    {
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceipts;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.GoodsReceiptLines;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockBalances;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.GoodsReceipts r JOIN dbo.Warehouses w ON w.WarehouseId=r.WarehouseId " +
            "JOIN dbo.Employees e ON e.EmployeeId=r.EmployeeId JOIN dbo.Users u ON u.UserId=r.PostedByUserId " +
            "WHERE r.Status=N'Posted' AND r.Note=N'GUI edited' AND w.Code=N'GUI-WH' AND e.Code=N'GUI-EMP' " +
            "AND u.NormalizedUsername=N'GUI.P5.OWNER' AND r.PostedAtUtc IS NOT NULL;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.GoodsReceiptLines l JOIN dbo.Products p ON p.ProductId=l.ProductId " +
            "WHERE p.Code=N'GUI-PROD' AND l.Quantity=3.125 AND l.UnitCost=4;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle, "SELECT COUNT(*) FROM dbo.StockLedger;"));
        Assert.Equal(1, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM dbo.StockLedger s JOIN dbo.GoodsReceiptLines l ON l.ReceiptLineId=s.DocumentLineId " +
            "JOIN dbo.GoodsReceipts r ON r.ReceiptId=l.ReceiptId WHERE s.DocumentType=N'GoodsReceipt' " +
            "AND s.DocumentId=r.ReceiptId AND s.ProductId=l.ProductId AND s.WarehouseId=r.WarehouseId " +
            "AND s.QuantityChange=3.125 AND s.PostedByUserId=r.PostedByUserId;"));
        Assert.Equal(3.125m, await ScalarAsync<decimal>(_oracle, "SELECT SUM(Quantity) FROM dbo.StockBalances;"));
        Assert.Equal(0, await ScalarAsync<int>(_oracle,
            "SELECT COUNT(*) FROM (SELECT WarehouseId,ProductId,SUM(QuantityChange) Quantity FROM dbo.StockLedger " +
            "GROUP BY WarehouseId,ProductId) l FULL JOIN dbo.StockBalances b " +
            "ON b.WarehouseId=l.WarehouseId AND b.ProductId=l.ProductId WHERE ISNULL(l.Quantity,0)<>ISNULL(b.Quantity,0);"));
    }

    public async Task DisposeAsync()
    {
        if (_disposed) return;
        if (GuiTestSafety.IsPoisoned || _uncertainLogin)
        {
            var failure = new InvalidOperationException("GUI allocation retained: process or principal ownership is uncertain.");
            Quarantine(failure, "QuarantinedOwnershipOrProcessCleanupUncertain");
            throw failure;
        }
        try
        {
            // Freeze only exactly verified owned logins; no new SQL sessions can then race cleanup.
            foreach (var login in _logins)
            {
                await VerifyLoginAsync(login);
                await ExecuteAsync(_administration, "ALTER LOGIN [" + login.Name + "] DISABLE;");
            }
            var clock = Stopwatch.StartNew();
            while (await ScalarAsync<int>(_administration,
                "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID AND " +
                "(database_id=DB_ID(@db) OR " + (_logins.Count == 0 ? "1=0" :
                    "original_login_name IN (" + string.Join(",", _logins.Select(l => "N'" + l.Name + "'")) + ")") + ");",
                new SqlParameter("@db", _database.DatabaseName)) != 0)
            {
                if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException("Owned SQL sessions did not drain.");
                await Task.Delay(50);
            }
            await _database.DisposeAsync();
            foreach (var login in _logins)
            {
                await VerifyLoginAsync(login);
                await ExecuteAsync(_administration, "DROP LOGIN [" + login.Name + "];");
                Assert.Equal(0, await ScalarAsync<int>(_administration,
                    "SELECT COUNT(*) FROM sys.server_principals WHERE name=@name OR sid=@sid;",
                    new SqlParameter("@name", login.Name), new SqlParameter("@sid", login.Sid)));
            }
            _disposed = true;
            Record("DatabaseAndAllPrincipalsDroppedAfterVerifiedDrain");
        }
        catch (Exception failure)
        {
            Quarantine(failure, "QuarantinedCleanupFailed");
            throw;
        }
    }

    private void Quarantine(Exception failure, string state)
    {
        // Stop admission even if writing local quarantine evidence also fails.
        GuiTestSafety.Poison(failure);
        try { Record(state); }
        catch (Exception recording)
        {
            throw new AggregateException("GUI cleanup is uncertain and quarantine evidence could not be written.", failure, recording);
        }
    }

    private async Task VerifyLoginAsync(Login login)
    {
        using var connection = new SqlConnection(_administration);
        await connection.OpenAsync();
        using var command = new SqlCommand(
            "SELECT principal_id,sid,create_date," +
            "(SELECT COUNT(*) FROM sys.server_role_members WHERE member_principal_id=p.principal_id)," +
            "(SELECT COUNT(*) FROM sys.server_permissions WHERE grantee_principal_id=p.principal_id AND permission_name<>N'CONNECT SQL') " +
            "FROM sys.server_principals p WHERE name=@name AND type=N'S';", connection) { CommandTimeout = 15 };
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = login.Name;
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Owned principal missing; refusing cleanup.");
        Assert.Equal(login.Id, reader.GetInt32(0));
        Assert.Equal(login.Sid, (byte[])reader.GetValue(1));
        Assert.Equal(login.Created, reader.GetDateTime(2));
        Assert.Equal(0, reader.GetInt32(3));
        Assert.Equal(0, reader.GetInt32(4));
    }

    public static async Task RunAsync(string package, Func<GuiSqlEnvironment, Task> body)
    {
        var fixture = await CreateAsync(package);
        Exception? failure = null;
        try { await body(fixture); }
        catch (Exception error) { failure = error; }
        try { await fixture.DisposeAsync(); }
        catch (Exception cleanup)
        {
            if (failure != null) throw new AggregateException("GUI operation and cleanup both failed.", failure, cleanup);
            throw;
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose() => DisposeAsync().GetAwaiter().GetResult();

    private async Task CaptureAllocationAsync()
    {
        using var connection = new SqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await _database.VerifyOwnershipAsync(connection, CancellationToken.None);
        using var command = new SqlCommand(
            "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),database_id,create_date," +
            "(SELECT CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'MyDmsVn.TestOwnership') " +
            "FROM sys.databases WHERE database_id=DB_ID();", connection) { CommandTimeout = 15 };
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        _allocationIdentity = new { Instance = reader.GetString(0), DatabaseId = reader.GetInt32(1), Created = reader.GetDateTime(2), Marker = reader.GetString(3) };
    }

    private void Record(string state) => File.WriteAllText(_resourceFile, JsonConvert.SerializeObject(new
    {
        State = state, Database = _database.DatabaseName, AllocationIdentity = _allocationIdentity,
        UncertainPrincipal = _uncertainLoginName,
        Principals = _logins.Select(login => new { Name = login.Name, login.Id, Sid = Convert.ToBase64String(login.Sid), login.Created }),
    }, Formatting.Indented)); // Local ignored identity evidence only, never a password or connection string.

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MyDmsVn.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }

    internal static async Task<T> ScalarAsync<T>(string connectionString, string sql, params SqlParameter[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 15 };
        command.Parameters.AddRange(parameters);
        return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T));
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await command.ExecuteNonQueryAsync();
    }

    private sealed class Login
    {
        public Login(string name) { Name = name; }
        public string Name { get; }
        public string Password { get; } = Guid.NewGuid().ToString("N") + "aA9!";
        public int Id { get; set; }
        public byte[] Sid { get; set; } = Array.Empty<byte>();
        public DateTime Created { get; set; }
    }
}
