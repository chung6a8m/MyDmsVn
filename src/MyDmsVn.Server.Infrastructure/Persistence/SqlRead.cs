using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Server.Infrastructure.Persistence;

// Typed native result adapter for security procedures and startup diagnostics.
// Callers own connections/transactions and commands; no resource is opened here.
internal static class SqlRead
{
    internal static SqlCommand Command(IDbConnection connection, IDbTransaction? transaction, string sql, params SqlParameter[] parameters)
    {
        var command = (SqlCommand)connection.CreateCommand();
        command.Transaction = (SqlTransaction?)transaction;
        command.CommandText = sql;
        command.CommandTimeout = 30;
        command.Parameters.AddRange(parameters);
        return command;
    }

    internal static SqlParameter P(string name, SqlDbType type, object? value) => new SqlParameter(name, type) { Value = value ?? DBNull.Value };
    internal static SqlParameter Text(string name, string? value) => new SqlParameter(name, SqlDbType.NVarChar, value?.Length > 4000 ? -1 : 4000) { Value = (object?)value ?? DBNull.Value };

    internal static async Task<T[]> QueryAsync<T>(SqlCommand command, Func<SqlDataReader, T> map, CancellationToken token)
    {
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var rows = new List<T>();
        while (await reader.ReadAsync(token).ConfigureAwait(false)) rows.Add(map(reader));
        // A procedure may deliver THROW only after its first (possibly empty) result.
        await DrainAsync(reader, token).ConfigureAwait(false);
        return rows.ToArray();
    }

    internal static async Task<T> SingleAsync<T>(SqlCommand command, Func<SqlDataReader, T> map, CancellationToken token) =>
        (await QueryAsync(command, map, token).ConfigureAwait(false)).Single();

    internal static async Task<T?> SingleOrDefaultAsync<T>(SqlCommand command, Func<SqlDataReader, T> map, CancellationToken token) where T : class =>
        (await QueryAsync(command, map, token).ConfigureAwait(false)).SingleOrDefault();

    internal static async Task DrainAsync(SqlDataReader reader, CancellationToken token)
    {
        while (await reader.NextResultAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false)) { }
    }
}
