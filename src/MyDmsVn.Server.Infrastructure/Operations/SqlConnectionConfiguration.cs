using System;
using System.IO;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Operations;

public enum SqlConnectionConfigurationPurpose
{
    Runtime,
    Migration,
}

public enum SqlConnectionConfigurationSource
{
    Environment,
    EnvironmentFile,
    InstallationFile,
}

public sealed class SqlConnectionConfiguration
{
    public SqlConnectionConfiguration(
        string connectionString,
        SqlConnectionConfigurationSource source)
    {
        ConnectionString = connectionString;
        Source = source;
    }

    public string ConnectionString { get; }

    public SqlConnectionConfigurationSource Source { get; }
}

public sealed class OperationalConfigurationException : Exception
{
    public OperationalConfigurationException(string code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
        SafeMessage = safeMessage;
    }

    public string Code { get; }

    public string SafeMessage { get; }
}

public sealed class SqlConnectionConfigurationLoader
{
    private static readonly string[] SystemDatabaseNames =
    {
        "master",
        "model",
        "msdb",
        "tempdb",
    };

    private readonly Func<string, string?> _readEnvironment;
    private readonly Func<string, string> _readFile;
    private readonly Func<SqlConnectionConfigurationPurpose, string> _getInstallationFilePath;

    public const string RuntimeConnectionStringEnvironmentVariable =
        "MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING";

    public const string RuntimeConnectionStringFileEnvironmentVariable =
        "MYDMSVN_RUNTIME_SQLSERVER_CONNECTION_STRING_FILE";

    public const string MigrationConnectionStringEnvironmentVariable =
        "MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING";

    public const string MigrationConnectionStringFileEnvironmentVariable =
        "MYDMSVN_MIGRATION_SQLSERVER_CONNECTION_STRING_FILE";

    public SqlConnectionConfigurationLoader()
        : this(
            Environment.GetEnvironmentVariable,
            File.ReadAllText,
            GetDefaultInstallationFilePath)
    {
    }

    public SqlConnectionConfigurationLoader(
        Func<string, string?> readEnvironment,
        Func<string, string> readFile,
        Func<SqlConnectionConfigurationPurpose, string> getInstallationFilePath)
    {
        _readEnvironment = readEnvironment ?? throw new ArgumentNullException(nameof(readEnvironment));
        _readFile = readFile ?? throw new ArgumentNullException(nameof(readFile));
        _getInstallationFilePath = getInstallationFilePath ??
            throw new ArgumentNullException(nameof(getInstallationFilePath));
    }

    public SqlConnectionConfiguration Load(SqlConnectionConfigurationPurpose purpose)
    {
        var valueVariable = purpose == SqlConnectionConfigurationPurpose.Runtime
            ? RuntimeConnectionStringEnvironmentVariable
            : MigrationConnectionStringEnvironmentVariable;
        var fileVariable = purpose == SqlConnectionConfigurationPurpose.Runtime
            ? RuntimeConnectionStringFileEnvironmentVariable
            : MigrationConnectionStringFileEnvironmentVariable;

        var environmentValue = _readEnvironment(valueVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return Create(environmentValue!, SqlConnectionConfigurationSource.Environment);
        }

        var environmentFile = _readEnvironment(fileVariable);
        if (!string.IsNullOrWhiteSpace(environmentFile))
        {
            return LoadFile(environmentFile!, SqlConnectionConfigurationSource.EnvironmentFile);
        }

        var installationFile = _getInstallationFilePath(purpose);
        try
        {
            return LoadFile(installationFile, SqlConnectionConfigurationSource.InstallationFile);
        }
        catch (OperationalConfigurationException exception)
            when (string.Equals(
                exception.Code,
                "Startup.DatabaseConfigurationNotFound",
                StringComparison.Ordinal))
        {
            throw MissingConfiguration();
        }
    }

    private SqlConnectionConfiguration LoadFile(
        string path,
        SqlConnectionConfigurationSource source)
    {
        try
        {
            return Create(_readFile(path), source);
        }
        catch (OperationalConfigurationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is FileNotFoundException ||
            exception is DirectoryNotFoundException)
        {
            throw new OperationalConfigurationException(
                "Startup.DatabaseConfigurationNotFound",
                "Không tìm thấy file cấu hình cơ sở dữ liệu. Hãy liên hệ quản trị viên triển khai.");
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is System.Security.SecurityException ||
            exception is ArgumentException ||
            exception is NotSupportedException)
        {
            throw new OperationalConfigurationException(
                "Startup.DatabaseConfigurationUnreadable",
                "Không thể đọc cấu hình cơ sở dữ liệu. Hãy liên hệ quản trị viên triển khai.");
        }
    }

    private static SqlConnectionConfiguration Create(
        string connectionString,
        SqlConnectionConfigurationSource source)
    {
        try
        {
            var trimmed = connectionString?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw InvalidConfiguration();
            }

            var builder = new SqlConnectionStringBuilder(trimmed);
            var databaseName = builder.InitialCatalog?.Trim();
            if (string.IsNullOrWhiteSpace(databaseName) ||
                Array.Exists(
                    SystemDatabaseNames,
                    systemDatabase => string.Equals(
                        systemDatabase,
                        databaseName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                throw InvalidConfiguration();
            }

            return new SqlConnectionConfiguration(trimmed!, source);
        }
        catch (OperationalConfigurationException)
        {
            throw;
        }
        catch (ArgumentException)
        {
            throw InvalidConfiguration();
        }
    }

    private static OperationalConfigurationException MissingConfiguration()
    {
        return new OperationalConfigurationException(
            "Startup.DatabaseConfigurationMissing",
            "Thiếu cấu hình cơ sở dữ liệu. Hãy liên hệ quản trị viên triển khai.");
    }

    private static OperationalConfigurationException InvalidConfiguration()
    {
        return new OperationalConfigurationException(
            "Startup.DatabaseConfigurationInvalid",
            "Cấu hình cơ sở dữ liệu không hợp lệ hoặc trỏ tới cơ sở dữ liệu hệ thống.");
    }

    private static string GetDefaultInstallationFilePath(
        SqlConnectionConfigurationPurpose purpose)
    {
        var fileName = purpose == SqlConnectionConfigurationPurpose.Runtime
            ? "runtime.connection"
            : "migration.connection";
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MyDmsVn",
            fileName);
    }
}
