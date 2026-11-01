using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
namespace MyDmsVn.Server.Infrastructure.Operations;
// Explicit web boundary. The desktop manifest and exact10 fingerprint remain frozen.
public sealed class SqlWebSchema11CompatibilityChecker : ISchemaCompatibilityChecker
{
    public static readonly IReadOnlyList<string> Scripts = SchemaCompatibilityPolicy.CurrentScripts.Concat(new[] { "011_WebAuthSessions.sql" }).ToArray();
    private readonly string _connectionString;
    public SqlWebSchema11CompatibilityChecker(string connectionString)
    {
        _connectionString = connectionString;
    }
    public async Task<SchemaCompatibilityResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var c = new SqlConnection(_connectionString);
        await c.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await CheckConnectionAsync(c, cancellationToken).ConfigureAwait(false);
    }
    public static async Task<SchemaCompatibilityResult> CheckConnectionAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandText = "SELECT OBJECT_ID(N'dbo.SchemaVersions', N'U');";
        var journal = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (journal == null || journal == DBNull.Value)
            return await new SqlSchemaCompatibilityChecker(connection.ConnectionString).CheckConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        command.CommandText = "SELECT ScriptName FROM dbo.SchemaVersions ORDER BY Id;";
        var scripts = new List<string>();
        using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString(0);
                const string marker = ".Scripts.";
                var index = name.LastIndexOf(marker, StringComparison.Ordinal);
                scripts.Add(index < 0 ? name.Trim() : name.Substring(index + marker.Length).Trim());
            }
        }
        if (scripts.Distinct(StringComparer.Ordinal).Count() != scripts.Count || !scripts.Take(Scripts.Count).SequenceEqual(Scripts.Take(scripts.Count), StringComparer.Ordinal))
            return Result(SchemaCompatibilityStatus.UnknownHistory);
        if (scripts.Count > Scripts.Count)
            return Result(SchemaCompatibilityStatus.TooNew);
        if (scripts.Count < 11)
        {
            var desktop = await new SqlSchemaCompatibilityChecker(connection.ConnectionString).CheckConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            return desktop.IsCompatible ? Result(SchemaCompatibilityStatus.TooOld) : desktop;
        }
        return string.Equals(await SqlSchemaDefinitionFingerprint.ReadAsync(connection, cancellationToken).ConfigureAwait(false), SqlSchemaDefinitionFingerprint.Schema11, StringComparison.Ordinal)
            ? Result(SchemaCompatibilityStatus.Compatible) : Result(SchemaCompatibilityStatus.Partial);
    }
    private static SchemaCompatibilityResult Result(SchemaCompatibilityStatus status) => new SchemaCompatibilityResult(status, "Schema." + status,
        status == SchemaCompatibilityStatus.Compatible ? "Web schema11 is compatible." : "Web schema11 admission refused: " + status + ".");
}
