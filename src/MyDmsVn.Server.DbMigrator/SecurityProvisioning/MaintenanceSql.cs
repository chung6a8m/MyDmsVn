using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

// Administrator-only adapters. The engines retain connection, transaction and authority ownership.
internal static class MaintenanceSql
{
    internal static SqlCommand Command(SqlConnection connection, SqlTransaction transaction, string sql, params SqlParameter[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 30 };
        command.Parameters.AddRange(parameters);
        return command;
    }

    internal static SqlParameter P(string name, SqlDbType type, object? value) => new SqlParameter(name, type) { Value = value ?? DBNull.Value };
    internal static SqlParameter Text(string name, string? value) => new SqlParameter(name, SqlDbType.NVarChar, value?.Length > 4000 ? -1 : 4000) { Value = (object?)value ?? DBNull.Value };

    internal static async Task<T> SingleAsync<T>(SqlCommand command, CancellationToken token)
    {
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var found = await reader.ReadAsync(token).ConfigureAwait(false);
        var value = found ? (T)reader.GetValue(0) : default!;
        var multiple = found && await reader.ReadAsync(token).ConfigureAwait(false);
        while (await reader.NextResultAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false)) { }
        if (!found || multiple) throw new InvalidOperationException("Maintenance SQL must return exactly one row.");
        return value;
    }
}
