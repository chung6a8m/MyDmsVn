using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Testing.Sql;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class NativeSqlOracleTests
{
    [Fact]
    public void Native_oracle_maps_nullable_binary_tuple_and_poco_values_without_product_mappings()
    {
        var table = new DataTable(); table.Columns.Add("Id", typeof(short)); table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Payload", typeof(byte[])); table.Columns.Add("Revision", typeof(long));
        table.Rows.Add((short)7, DBNull.Value, new byte[] { 0, 128, 255 }, 9L);
        using var reader = table.CreateDataReader(); Assert.True(reader.Read());
        Assert.Equal(7, SqlOracleExtensions.Read<int>(reader));
        var tuple = SqlOracleExtensions.Read<(int, string?, byte[], long)>(reader);
        Assert.Equal(7, tuple.Item1); Assert.Null(tuple.Item2); Assert.Equal(new byte[] { 0, 128, 255 }, tuple.Item3); Assert.Equal(9L, tuple.Item4);
        var row = SqlOracleExtensions.Read<Row>(reader); Assert.Equal(7, row.Id); Assert.Null(row.Name); Assert.Equal(tuple.Item3, row.Payload); Assert.Equal(9, row.Revision);
    }

    [SqlServerFact]
    public async Task Native_oracle_preserves_auto_open_current_transaction_types_cardinality_and_deferred_errors()
    {
        var database = await SqlTestDatabase.CreateAsync(Environment.GetEnvironmentVariable(SqlTestDatabase.ConnectionStringEnvironmentVariable)!);
        try
        {
            using var connection = new SqlConnection(database.ConnectionString);
            Assert.Equal(7, await connection.QuerySingleAsync<int>("SELECT CONVERT(smallint,7)")); Assert.Equal(ConnectionState.Closed, connection.State);
            await connection.OpenAsync(); using var transaction = connection.BeginTransaction();
            Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: transaction));
            var stamp = new DateTime(2026, 10, 19, 1, 2, 3).AddTicks(1234567);
            var parameters = new[] { new SqlParameter("@stamp", SqlDbType.DateTime2) { Value = stamp }, new SqlParameter("@bytes", SqlDbType.VarBinary, 3) { Value = new byte[] { 0, 128, 255 } } };
            var result = await connection.QuerySingleAsync<(DateTime, byte[])>("SELECT @stamp,@bytes", parameters, transaction);
            Assert.Equal(stamp, result.Item1); Assert.Equal(new byte[] { 0, 128, 255 }, result.Item2);
            Assert.Null(await connection.QuerySingleOrDefaultAsync<int?>("SELECT CONVERT(int,NULL)", transaction: transaction));
            Assert.Null(await connection.QuerySingleOrDefaultAsync<Row>("SELECT 1 AS Id WHERE 1=0", transaction: transaction));
            await Assert.ThrowsAsync<InvalidOperationException>(() => connection.QuerySingleAsync<int>("SELECT 1 UNION ALL SELECT 2", transaction: transaction));
            foreach (var select in new[] { "SELECT 1;", "SELECT 1 WHERE 1=0;" })
            {
                var error = await Assert.ThrowsAsync<SqlException>(() => connection.QuerySingleAsync<int>(select + " THROW 51221, 'Expected late oracle error.',1;", transaction: transaction));
                Assert.Equal(51221, error.Number);
            }
            Assert.Equal(ConnectionState.Open, connection.State); Assert.Equal(1, await connection.QuerySingleAsync<int>("SELECT @@TRANCOUNT", transaction: transaction));
            transaction.Rollback();
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connection.ExecuteAsync(new SqlTestCommand("SELECT 1", cancellationToken: cancel.Token)));
            Assert.Equal(ConnectionState.Open, connection.State);
        }
        finally { await database.DisposeAsync(); }
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public long Revision { get; set; }
    }
}
