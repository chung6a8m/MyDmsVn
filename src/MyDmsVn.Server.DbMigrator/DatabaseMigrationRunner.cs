using System;
using System.Linq;
using DbUp;
using DbUp.Engine;
using DbUp.SqlServer;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Operations;

namespace MyDmsVn.Server.DbMigrator;

public sealed class DatabaseMigrationRunner
{
    internal const string MigrationLockResource = "MyDmsVn.SchemaMigration";

    private static readonly string[] SystemDatabaseNames =
    {
        "master",
        "model",
        "msdb",
        "tempdb",
    };

    public DatabaseUpgradeResult Migrate(string connectionString) => MigrateCore(connectionString, false);

    public DatabaseUpgradeResult MigrateWeb(string connectionString) => MigrateCore(connectionString, true);

    private DatabaseUpgradeResult MigrateCore(string connectionString, bool web)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        ValidateTargetDatabase(connectionString);

        using var migrationLock = DatabaseMigrationLock.Acquire(connectionString);
        if (web) ValidateWebSchemaStateBeforeMigration(connectionString, migrationLock.Connection);
        else ValidateSchemaStateBeforeMigration(connectionString, migrationLock.Connection);
        var connectionManager = new MigrationConnectionManager(
            new SqlConnectionManager(migrationLock.TransferConnectionOwnership), migrationLock.ReleaseSessionLock);

        var assembly = typeof(DatabaseMigrationRunner).Assembly;
        var engine = DeployChanges.To
            .SqlDatabase(connectionManager)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resourceName => resourceName.Contains(".Scripts.") && (web ? SqlWebSchemaCompatibilityChecker.Scripts : SchemaCompatibilityPolicy.CurrentScripts).Any(script => resourceName.EndsWith(".Scripts." + script, StringComparison.Ordinal)))
            .JournalToSqlTable("dbo", "SchemaVersions")
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        var result = engine.PerformUpgrade();
        if (web && result.Successful)
        {
            var verified = new SqlWebSchemaCompatibilityChecker(connectionString).CheckAsync(System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            if (!verified.IsCompatible) throw new DatabaseMigrationPreflightException("Migration.WebVerificationFailed", verified.SafeMessage);
        }
        return result;
    }

    private static void ValidateWebSchemaStateBeforeMigration(string connectionString, SqlConnection connection)
    {
        var state = SqlWebSchemaCompatibilityChecker.CheckConnectionAsync(connection, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
        if (state.IsCompatible) return;
        if (state.Status == SchemaCompatibilityStatus.Empty)
        {
            ValidateSchemaStateBeforeMigration(connectionString, connection);
            return;
        }
        if (state.Status == SchemaCompatibilityStatus.TooOld)
        {
            var source11 = SqlWebSchema11CompatibilityChecker.CheckConnectionAsync(connection, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            if (source11.IsCompatible)
            {
                Schema11WebMaintenanceValidator.Validate(connection);
                return;
            }
            // Frozen exact10 checker is independent of the web manifest. Never infer 9->11.
            var source = new SqlSchemaCompatibilityChecker(connectionString).CheckConnectionAsync(connection, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            if (source.IsCompatible)
            {
                Schema10WebMaintenanceValidator.Validate(connection);
                return;
            }
            throw new DatabaseMigrationPreflightException("Migration.WebUnsupportedSource", "Web migration requires a separately admitted exact schema10 source; schema9 cannot upgrade directly.");
        }
        throw new DatabaseMigrationPreflightException("Migration.Web" + state.Status, state.SafeMessage);
    }

    private static void ValidateSchemaStateBeforeMigration(
        string connectionString,
        SqlConnection connection)
    {
        var compatibility = new SqlSchemaCompatibilityChecker(connectionString)
            .CheckConnectionAsync(connection, System.Threading.CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        switch (compatibility.Status)
        {
            case SchemaCompatibilityStatus.Empty:
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "EXEC sys.sp_set_session_context @key=N'MyDmsVn.FreshInstall', @value=1, @read_only=1;";
                    command.ExecuteNonQuery();
                }
                return;
            case SchemaCompatibilityStatus.Compatible:
                return;
            case SchemaCompatibilityStatus.TooOld:
                var source9 = SqlSchemaCompatibilityChecker.CheckExactSchema9Async(connection,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (source9.IsCompatible)
                {
                    Schema9MaintenanceValidator.Validate(connection);
                    return;
                }
                throw new DatabaseMigrationPreflightException(
                    "Migration.UnsupportedExistingDatabase",
                    "Cơ sở dữ liệu hiện có không được hỗ trợ sau khi thay security baseline. Hãy reset rõ ràng một database disposable trước khi cài đặt mới.");
            case SchemaCompatibilityStatus.TooNew:
                throw new DatabaseMigrationPreflightException(
                    "Migration.SchemaTooNew",
                    compatibility.SafeMessage);
            case SchemaCompatibilityStatus.UnknownHistory:
                throw new DatabaseMigrationPreflightException(
                    "Migration.UnknownHistory",
                    compatibility.SafeMessage);
            case SchemaCompatibilityStatus.Partial:
                throw new DatabaseMigrationPreflightException(
                    "Migration.PartialSchema",
                    compatibility.SafeMessage);
            default:
                throw new DatabaseMigrationPreflightException(
                    "Migration.SchemaUnsupported",
                    "Trạng thái schema không được hỗ trợ. Migration đã dừng trước khi thay đổi dữ liệu.");
        }
    }

    private static void ValidateTargetDatabase(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog?.Trim();
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException(
                "The migration connection string must specify an application database.",
                nameof(connectionString));
        }

        if (Array.Exists(
                SystemDatabaseNames,
                systemDatabase => string.Equals(systemDatabase, databaseName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                $"Migrations cannot target the SQL Server system database '{databaseName}'.",
                nameof(connectionString));
        }
    }
}

public sealed class DatabaseMigrationPreflightException : Exception
{
    public DatabaseMigrationPreflightException(string code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class DatabaseMigrationConcurrencyException : Exception
{
    public DatabaseMigrationConcurrencyException()
        : base("Một tiến trình migration khác đang chạy. Hãy chờ tiến trình đó hoàn tất rồi thử lại.")
    {
    }

    public string Code => "Migration.AlreadyRunning";
}

public sealed class DatabaseMigrationAuthorizationException : Exception
{
    public DatabaseMigrationAuthorizationException()
        : base("Tài khoản migration không có quyền CONTROL trên cơ sở dữ liệu đích. Không có thay đổi nào được thực hiện.")
    {
    }

    public string Code => "Migration.InsufficientPrivileges";
}

public static class DatabaseMigrationPrincipalValidator
{
    public static void Validate(SqlConnection connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        if (connection.State != System.Data.ConnectionState.Open)
        {
            throw new ArgumentException("The SQL connection must be open.", nameof(connection));
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT ISNULL(HAS_PERMS_BY_NAME(DB_NAME(), N'DATABASE', N'CONTROL'), 0);";
        if (Convert.ToInt32(command.ExecuteScalar()) != 1)
        {
            throw new DatabaseMigrationAuthorizationException();
        }
    }
}

internal sealed class DatabaseMigrationLock : IDisposable
{
    private readonly SqlConnection _connection;
    private bool _disposed;
    private bool _connectionOwnershipTransferred;
    private bool _sessionLockReleased;

    private DatabaseMigrationLock(SqlConnection connection)
    {
        _connection = connection;
    }

    public SqlConnection Connection => _connection;

    public static DatabaseMigrationLock Acquire(string connectionString)
    {
        var dedicatedConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        }.ConnectionString;
        var connection = new SqlConnection(dedicatedConnectionString);
        try
        {
            connection.Open();
            DatabaseMigrationPrincipalValidator.Validate(connection);
            using var command = connection.CreateCommand();
            command.CommandText =
                "DECLARE @Result int; " +
                "EXEC @Result = sys.sp_getapplock " +
                "@Resource = @Resource, @LockMode = N'Exclusive', " +
                "@LockOwner = N'Session', @LockTimeout = 0; " +
                "SELECT @Result;";
            command.Parameters.AddWithValue("@Resource", DatabaseMigrationRunner.MigrationLockResource);
            var result = Convert.ToInt32(command.ExecuteScalar());
            if (result < 0)
            {
                throw new DatabaseMigrationConcurrencyException();
            }

            return new DatabaseMigrationLock(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public SqlConnection TransferConnectionOwnership()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DatabaseMigrationLock));
        }

        if (_connectionOwnershipTransferred)
        {
            throw new InvalidOperationException(
                "The migration SQL connection has already been transferred to DbUp.");
        }

        _connectionOwnershipTransferred = true;
        return _connection;
    }

    internal void ReleaseSessionLock()
    {
        if (_sessionLockReleased || _connection.State != System.Data.ConnectionState.Open) return;
        using var command = _connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_releaseapplock @Resource=@Resource,@LockOwner=N'Session'; SELECT @result;";
        command.Parameters.AddWithValue("@Resource", DatabaseMigrationRunner.MigrationLockResource);
        if (Convert.ToInt32(command.ExecuteScalar()) < 0)
            throw new InvalidOperationException("The migration session lock could not be released.");
        _sessionLockReleased = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        try
        {
            // DbUp normally releases/disposes through MigrationConnectionManager. Retain ownership
            // cleanup if initialization fails after the connection factory handed it to DbUp.
            ReleaseSessionLock();
        }
        finally
        {
            _disposed = true;
            _connection.Dispose();
        }
    }
}
