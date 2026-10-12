using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Operations;

public sealed class DesktopStartupException : Exception
{
    public DesktopStartupException(string code, string safeMessage)
        : base(safeMessage)
    {
        Code = code;
        SafeMessage = safeMessage;
    }

    public string Code { get; }

    public string SafeMessage { get; }
}

public sealed class DesktopDatabaseStartupCoordinator
{
    private readonly SqlConnectionConfigurationLoader _configurationLoader;
    private readonly Func<string, ISchemaCompatibilityChecker> _createChecker;

    public DesktopDatabaseStartupCoordinator()
        : this(
            new SqlConnectionConfigurationLoader(),
            connectionString => new SqlSchemaCompatibilityChecker(connectionString))
    {
    }

    public DesktopDatabaseStartupCoordinator(
        SqlConnectionConfigurationLoader configurationLoader,
        Func<string, ISchemaCompatibilityChecker> createChecker)
    {
        _configurationLoader = configurationLoader ??
            throw new ArgumentNullException(nameof(configurationLoader));
        _createChecker = createChecker ?? throw new ArgumentNullException(nameof(createChecker));
    }

    public async Task<SqlConnectionConfiguration> PrepareAsync(
        CancellationToken cancellationToken)
    {
        var configuration = _configurationLoader.Load(
            SqlConnectionConfigurationPurpose.Runtime);

        SchemaCompatibilityResult compatibility;
        try
        {
            compatibility = await _createChecker(configuration.ConnectionString)
                .CheckAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SqlException)
        {
            throw new DesktopStartupException(
                "Startup.DatabaseUnavailable",
                "Không thể kết nối cơ sở dữ liệu để kiểm tra phiên bản. Kiểm tra mạng hoặc liên hệ quản trị viên.");
        }

        if (!compatibility.IsCompatible)
        {
            throw new DesktopStartupException(
                compatibility.Code,
                compatibility.SafeMessage);
        }

        return configuration;
    }
}
