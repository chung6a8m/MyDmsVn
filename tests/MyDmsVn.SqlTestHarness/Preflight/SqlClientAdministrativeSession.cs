using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.SqlTestHarness.Approval;

namespace MyDmsVn.SqlTestHarness.Preflight;

public sealed class SqlClientAdministrativeSession : ISqlAdministrativeSession
{
    private readonly SqlConnection _connection;

    public SqlClientAdministrativeSession(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public Task OpenAsync(CancellationToken cancellationToken) =>
        _connection.OpenAsync(cancellationToken);

    public async Task<string?> ReadEffectiveDatabaseAsync(CancellationToken cancellationToken)
    {
        using var command = CreateCommand("SELECT CONVERT(nvarchar(128), DB_NAME());");
        return Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    public async Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')),
    CONVERT(nvarchar(128), SERVERPROPERTY('MachineName')),
    CONVERT(nvarchar(128), SERVERPROPERTY('InstanceName')),
    CASE
        WHEN CONNECTIONPROPERTY('local_net_address') IS NULL THEN NULL
        ELSE CONVERT(nvarchar(32), CONNECTIONPROPERTY('net_transport')) + ':' +
             CONVERT(nvarchar(128), CONNECTIONPROPERTY('local_net_address')) + ',' +
             CONVERT(nvarchar(10), CONNECTIONPROPERTY('local_tcp_port'))
    END;";
        using var command = CreateCommand(sql);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
            reader.IsDBNull(0) ||
            reader.IsDBNull(1))
        {
            return null;
        }

        return new SqlInstanceIdentity(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    public async Task<int?> ReadMajorVersionAsync(CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            "SELECT TRY_CONVERT(int, SERVERPROPERTY('ProductMajorVersion')); ");
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null || value == DBNull.Value
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public async Task<bool?> HasServerPermissionAsync(
        string permissionName,
        CancellationToken cancellationToken)
    {
        using var command = CreateCommand(
            "SELECT HAS_PERMS_BY_NAME(NULL, NULL, @permissionName);");
        command.Parameters.Add(new SqlParameter("@permissionName", SqlDbType.NVarChar, 128)
        {
            Value = permissionName,
        });
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null || value == DBNull.Value
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
    }

    public async Task ExecuteAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        if (sql is null)
        {
            throw new ArgumentNullException(nameof(sql));
        }

        using var command = CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => _connection.Dispose();

    private SqlCommand CreateCommand(string sql) =>
        new SqlCommand(sql, _connection)
        {
            CommandType = CommandType.Text,
        };
}
