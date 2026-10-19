using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Server.DbMigrator;
using MyDmsVn.Server.Infrastructure.IntegrationTests;

namespace MyDmsVn.Testing.Sql;

internal static class NativeSql
{
    public static SqlParameter P(string name, SqlDbType type, object? value) => new(name, type) { Value = value ?? DBNull.Value };
    public static SqlCommand Command(SqlConnection connection, SqlTransaction? transaction, string sql, params SqlParameter[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = 30 };
        command.Parameters.AddRange(parameters); return command;
    }
    public static async Task<T> Scalar<T>(SqlConnection connection, SqlTransaction? transaction, string sql, params SqlParameter[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        return (T)(await command.ExecuteScalarAsync(CancellationToken.None))!;
    }
    public static async Task Execute(SqlConnection connection, SqlTransaction? transaction, string sql, params SqlParameter[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }
    public static async Task<int[]> Ids(SqlConnection connection, string sql, params SqlParameter[] parameters)
    {
        using var command = Command(connection, null, sql, parameters);
        using var reader = await command.ExecuteReaderAsync(); var ids = new List<int>();
        while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0)); return ids.ToArray();
    }
    public static async Task Owned(Func<SqlTestDatabase, Task> body)
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        Exception? failure = null;
        try
        {
            var result = new DatabaseMigrationRunner().Migrate(database.ConnectionString);
            if (!result.Successful) throw new InvalidOperationException("Pilot database migration failed.", result.Error);
            await body(database);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            try { await database.DisposeAsync(); }
            catch (Exception cleanup) when (failure is not null) { throw new AggregateException(failure, cleanup); }
        }
    }
}
