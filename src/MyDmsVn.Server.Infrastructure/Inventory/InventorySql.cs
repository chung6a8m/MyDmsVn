using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Inventory;

// Native escape for the existing multi-result and reconciliation batches. The caller
// owns the factory connection/transaction and materializes every result before disposal.
internal static class InventorySql
{
    internal static SqlCommand Command(IDbConnection connection, IDbTransaction? transaction, string sql)
    {
        var command = (SqlCommand)connection.CreateCommand();
        command.Transaction = (SqlTransaction?)transaction;
        command.CommandText = sql;
        command.CommandTimeout = 30;
        return command;
    }

    internal static void Add(SqlCommand command, string name, SqlDbType type, object? value) =>
        command.Parameters.Add(name, type).Value = value ?? DBNull.Value;

    internal static void AddText(SqlCommand command, string name, string? value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, value?.Length > 4000 ? -1 : 4000).Value = (object?)value ?? DBNull.Value;

    internal static async Task<long> ReadCountAsync(SqlDataReader reader, CancellationToken token)
    {
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Inventory count result is missing.");
        var count = reader.GetInt64(0);
        await NextResultAsync(reader, token).ConfigureAwait(false);
        return count;
    }

    internal static async Task NextResultAsync(SqlDataReader reader, CancellationToken token)
    {
        if (!await reader.NextResultAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Inventory result set is missing.");
    }

    internal static async Task<T[]> ReadRowsAsync<T>(SqlDataReader reader, Func<SqlDataReader, T> map, CancellationToken token)
    {
        var rows = new List<T>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(map(reader));
        return rows.ToArray();
    }
}
