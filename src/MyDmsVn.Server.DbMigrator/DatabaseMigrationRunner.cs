using System;
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

    public DatabaseUpgradeResult Migrate(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        ValidateTargetDatabase(connectionString);

        using var migrationLock = DatabaseMigrationLock.Acquire(connectionString);
        ValidateSchemaStateBeforeMigration(connectionString, migrationLock.Connection);
        var connectionManager = new SqlConnectionManager(
            migrationLock.TransferConnectionOwnership);

        var assembly = typeof(DatabaseMigrationRunner).Assembly;
        var engine = DeployChanges.To
            .SqlDatabase(connectionManager)
            .WithScriptsEmbeddedInAssembly(
                assembly,
                resourceName => resourceName.Contains(".Scripts."))
            .JournalToSqlTable("dbo", "SchemaVersions")
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build();

        return engine.PerformUpgrade();
    }

    private static void ValidateSchemaStateBeforeMigration(
        string connectionString,
        SqlConnection connection)
    {
        var compatibility = new SqlSchemaCompatibilityChecker(
                connectionString,
                requireCurrentPermissionRelease: false)
            .CheckConnectionAsync(connection, System.Threading.CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        switch (compatibility.Status)
        {
            case SchemaCompatibilityStatus.Empty:
            case SchemaCompatibilityStatus.TooOld:
            case SchemaCompatibilityStatus.Compatible:
                return;
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
            case SchemaCompatibilityStatus.PermissionSynchronizationRequired:
                throw new DatabaseMigrationPreflightException(
                    "Migration.PermissionSynchronizationRequired",
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_connectionOwnershipTransferred)
        {
            _disposed = true;
            return;
        }

        try
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "EXEC sys.sp_releaseapplock @Resource = @Resource, @LockOwner = N'Session';";
            command.Parameters.AddWithValue("@Resource", DatabaseMigrationRunner.MigrationLockResource);
            command.ExecuteNonQuery();
        }
        finally
        {
            _disposed = true;
            _connection.Dispose();
        }
    }
}
