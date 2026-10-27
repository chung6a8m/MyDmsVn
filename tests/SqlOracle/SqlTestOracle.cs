using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace MyDmsVn.Testing.Sql;

// Test-only raw-SQL oracle. No product DAL, ORM mappings, SQL generation, list expansion,
// ambient transaction, retry or connection creation. Supports only the inventoried test shapes.
internal readonly struct SqlTestCommand
{
    internal SqlTestCommand(string commandText, object? parameters = null, IDbTransaction? transaction = null,
        int? commandTimeout = null, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        Text = commandText; Parameters = parameters; Transaction = transaction; Timeout = commandTimeout ?? 30;
        Type = commandType ?? CommandType.Text; Token = cancellationToken;
    }
    internal string Text { get; }
    internal object? Parameters { get; }
    internal IDbTransaction? Transaction { get; }
    internal int Timeout { get; }
    internal CommandType Type { get; }
    internal CancellationToken Token { get; }
}

internal static class SqlOracleExtensions
{
    internal static Task<int> ExecuteAsync(this IDbConnection connection, string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null) =>
        connection.ExecuteAsync(new SqlTestCommand(sql, param, transaction, commandTimeout, commandType));

    internal static Task<int> ExecuteAsync(this IDbConnection connection, SqlTestCommand request) =>
        UseAsync(connection, request.Token, async () =>
        {
            // A small number of seed calls execute a parameter-object batch; retain their order.
            if (request.Parameters is IEnumerable batch && request.Parameters is not string && request.Parameters is not byte[] && request.Parameters is not SqlParameter[])
            {
                var count = 0;
                foreach (var item in batch)
                {
                    using var command = Build(connection, new SqlTestCommand(request.Text, item, request.Transaction, request.Timeout, request.Type, request.Token));
                    count += await command.ExecuteNonQueryAsync(request.Token).ConfigureAwait(false);
                }
                return count;
            }
            using var single = Build(connection, request);
            return await single.ExecuteNonQueryAsync(request.Token).ConfigureAwait(false);
        });

    internal static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection connection, string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null) =>
        connection.QueryAsync<T>(new SqlTestCommand(sql, param, transaction, commandTimeout, commandType));

    internal static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection connection, SqlTestCommand request) =>
        UseAsync<IEnumerable<T>>(connection, request.Token, async () =>
        {
            using var command = Build(connection, request);
            using var reader = await command.ExecuteReaderAsync(request.Token).ConfigureAwait(false);
            var rows = new List<T>();
            while (await reader.ReadAsync(request.Token).ConfigureAwait(false)) rows.Add(Read<T>(reader));
            // Preserve deferred THROW, including empty first results; never return a false success.
            while (await reader.NextResultAsync(request.Token).ConfigureAwait(false))
                while (await reader.ReadAsync(request.Token).ConfigureAwait(false)) { }
            return rows;
        });

    internal static Task<T> QuerySingleAsync<T>(this IDbConnection connection, string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null) =>
        connection.QuerySingleAsync<T>(new SqlTestCommand(sql, param, transaction, commandTimeout, commandType));
    internal static async Task<T> QuerySingleAsync<T>(this IDbConnection connection, SqlTestCommand request) =>
        (await connection.QueryAsync<T>(request).ConfigureAwait(false)).Single();

    internal static Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null) =>
        connection.QuerySingleOrDefaultAsync<T>(new SqlTestCommand(sql, param, transaction, commandTimeout, commandType));
    internal static async Task<T?> QuerySingleOrDefaultAsync<T>(this IDbConnection connection, SqlTestCommand request) =>
        (await connection.QueryAsync<T>(request).ConfigureAwait(false)).SingleOrDefault();

    internal static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, string sql, object? param = null,
        IDbTransaction? transaction = null, int? commandTimeout = null, CommandType? commandType = null) =>
        connection.QuerySingleAsync<T>(sql, param, transaction, commandTimeout, commandType);
    internal static Task<T> ExecuteScalarAsync<T>(this IDbConnection connection, SqlTestCommand request) => connection.QuerySingleAsync<T>(request);

    private static async Task<T> UseAsync<T>(IDbConnection connection, CancellationToken token, Func<Task<T>> body)
    {
        token.ThrowIfCancellationRequested();
        var ownsOpen = connection.State == ConnectionState.Closed;
        if (ownsOpen)
        {
            if (connection is not DbConnection db) throw new InvalidOperationException("SQL test oracle needs a DbConnection to open asynchronously.");
            await db.OpenAsync(token).ConfigureAwait(false);
        }
        try { return await body().ConfigureAwait(false); }
        finally { if (ownsOpen) connection.Close(); }
    }

    internal static SqlCommand Build(IDbConnection connection, SqlTestCommand request)
    {
        var command = (SqlCommand)connection.CreateCommand();
        command.CommandText = request.Text; command.CommandTimeout = request.Timeout;
        command.CommandType = request.Type; command.Transaction = (SqlTransaction?)request.Transaction;
        if (request.Parameters is SqlParameter[] explicitParameters) command.Parameters.AddRange(explicitParameters);
        else if (request.Parameters != null)
            foreach (var property in request.Parameters.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var value = property.GetValue(request.Parameters);
                if (type.IsEnum) { type = Enum.GetUnderlyingType(type); value = value == null ? null : Convert.ChangeType(value, type, CultureInfo.InvariantCulture); }
                var parameter = new SqlParameter("@" + property.Name, TypeOf(type)) { Value = value ?? DBNull.Value };
                if (type == typeof(string)) parameter.Size = value is string text && text.Length > 4000 ? -1 : 4000;
                command.Parameters.Add(parameter);
            }
        return command;
    }

    private static SqlDbType TypeOf(Type type)
    {
        if (type == typeof(string)) return SqlDbType.NVarChar;
        if (type == typeof(int)) return SqlDbType.Int;
        if (type == typeof(long)) return SqlDbType.BigInt;
        if (type == typeof(bool)) return SqlDbType.Bit;
        if (type == typeof(byte[])) return SqlDbType.VarBinary;
        if (type == typeof(DateTime)) return SqlDbType.DateTime;
        if (type == typeof(decimal)) return SqlDbType.Decimal;
        if (type == typeof(Guid)) return SqlDbType.UniqueIdentifier;
        if (type == typeof(short)) return SqlDbType.SmallInt;
        if (type == typeof(byte)) return SqlDbType.TinyInt;
        throw new NotSupportedException("Uninventoried SQL oracle parameter type: " + type.FullName);
    }

    internal static T Read<T>(IDataRecord reader)
    {
        var type = typeof(T); var scalar = Nullable.GetUnderlyingType(type) ?? type;
        if (scalar.IsPrimitive || scalar == typeof(string) || scalar == typeof(decimal) || scalar == typeof(DateTime) || scalar == typeof(Guid) || scalar == typeof(byte[]))
            return (T)ConvertValue(reader.GetValue(0), type)!;
        if (type.IsValueType && type.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
        {
            var types = type.GetGenericArguments();
            if (types.Length > 7) throw new NotSupportedException("Nested oracle tuples are not inventoried.");
            return (T)Activator.CreateInstance(type, types.Select((t, index) => ConvertValue(reader.GetValue(index), t)).ToArray())!;
        }
        var row = Activator.CreateInstance(type, nonPublic: true)!;
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanWrite).ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
            if (properties.TryGetValue(reader.GetName(index), out var property))
                property.SetValue(row, ConvertValue(reader.GetValue(index), property.PropertyType));
        return (T)row;
    }

    private static object? ConvertValue(object value, Type type)
    {
        if (value == DBNull.Value) return type.IsValueType && Nullable.GetUnderlyingType(type) == null ? Activator.CreateInstance(type) : null;
        var target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsInstanceOfType(value)) return value;
        return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
