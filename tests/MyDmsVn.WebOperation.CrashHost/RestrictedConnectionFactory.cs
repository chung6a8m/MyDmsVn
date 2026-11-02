using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.WebOperation.CrashHost;

// Same runtime principal boundary as WebAuthHttpSqlTests.Scenario.RestrictedFactory.
// The supervisor owns all arrangement/oracle work; this process only impersonates WebHttpProbe.
internal sealed class RestrictedConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    internal RestrictedConnectionFactory(string connectionString) { _connectionString = connectionString; }
    public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandTimeout = 30;
            command.CommandText = "EXECUTE AS USER=N'WebHttpProbe';";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }
}
