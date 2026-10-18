using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using LinqToDB;
using LinqToDB.Async;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests.Pilot;
internal sealed class PilotConnections : IDbConnectionFactory, IDisposable, IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private SqlConnection? _last;
    private readonly string _connectionString;
    private readonly IDisposable _all;
    private readonly List<IDisposable> _listeners = new();
    public List<(string Sql, string Parameters)> Commands { get; } = new();
    public PilotConnections(string connectionString) { _connectionString = connectionString; _all = DiagnosticListener.AllListeners.Subscribe(this); }
    public void OnNext(DiagnosticListener listener) { if (listener.Name == "SqlClientDiagnosticListener") _listeners.Add(listener.Subscribe(this)); }
    public void OnNext(KeyValuePair<string, object?> item)
    {
        if (!item.Key.EndsWith("WriteCommandBefore", StringComparison.Ordinal)) return;
        var command = item.Value?.GetType().GetProperty("Command")?.GetValue(item.Value) as SqlCommand;
        if (command is null || !ReferenceEquals(command.Connection, _last)) return;
        Commands.Add((command.CommandText, string.Join(",", command.Parameters.Cast<SqlParameter>().Select(p => p.SqlDbType + "(" + p.Size + ")"))));
    }
    public void OnCompleted() { }
    public void OnError(Exception error) { throw new InvalidOperationException("Pilot command observation failed.", error); }
    public void Dispose() { _all.Dispose(); foreach (var listener in _listeners) listener.Dispose(); }
    public long RoundTrips => (long)(_last!.RetrieveStatistics()["ServerRoundtrips"]!);
    public IDbConnection OpenConnection() => OpenConnectionAsync(CancellationToken.None).GetAwaiter().GetResult();
    public async Task<IDbConnection> OpenConnectionAsync(CancellationToken token)
    {
        _last = new SqlConnection(_connectionString);
        try { await _last.OpenAsync(token); _last.StatisticsEnabled = true; return _last; }
        catch { _last.Dispose(); throw; }
    }
}

// Temporary three-slice pilot. Never registered by production composition; remove in T06.
internal sealed class ProductPilot
{
    private readonly ISqlExecutionContext _context;
    private readonly bool _candidate;
    private readonly Catalog.SqlCatalogWriteRepository _oldWrites;
    private readonly Catalog.SqlProductQueryService _oldReads;
    private readonly LinqToDbSessionFactory _sessions;
    public ProductPilot(ISqlExecutionContext context, IDbConnectionFactory connections, bool candidate)
    {
        _context = context; _candidate = candidate;
        _sessions = new(connections, new LinqToDbMappingSchemaProvider(new ILinqToDbMapping[] { new ProductMapping() }));
        // After T03 the production side is LINQ too; retain the independent temporary pilot/native oracles until T06.
        _oldWrites = new(context, _sessions); _oldReads = new(_sessions);
    }
    public async Task<int> Insert(Product p)
    {
        if (!_candidate) return await _oldWrites.InsertProductAsync(p, CancellationToken.None);
        try
        {
            using var db = _sessions.Borrow(_context, true);
            return await db.GetTable<Product>().Value(x => x.Code, p.Code).Value(x => x.Name, p.Name).Value(x => x.Unit, p.Unit)
                .Value(x => x.IsActive, p.IsActive).Value(x => x.CreatedByUserId, p.CreatedByUserId).InsertWithInt32IdentityAsync(CancellationToken.None) ?? throw new InvalidOperationException("Product insert returned no identity.");
        }
        catch (SqlException e) when (e.Number is 2601 or 2627) { throw new MyDmsVn.Server.Application.Catalog.CatalogWriteConflictException(MyDmsVn.Server.Application.Catalog.CatalogWriteConflict.DuplicateCode); }
    }
    public async Task<Product?> Update(Product p)
    {
        if (!_candidate) return await _oldWrites.UpdateProductAsync(p, CancellationToken.None);
        try
        {
            using var db = _sessions.Borrow(_context, true);
            var changed = await db.GetTable<Product>().Where(x => x.Id == p.Id).Set(x => x.Code, p.Code).Set(x => x.Name, p.Name).Set(x => x.Unit, p.Unit)
                .Set(x => x.UpdatedAtUtc, p.UpdatedAtUtc).Set(x => x.UpdatedByUserId, p.UpdatedByUserId).UpdateAsync(CancellationToken.None);
            return changed == 0 ? null : await db.GetTable<Product>().SingleAsync(x => x.Id == p.Id, CancellationToken.None);
        }
        catch (SqlException e) when (e.Number is 2601 or 2627) { throw new MyDmsVn.Server.Application.Catalog.CatalogWriteConflictException(MyDmsVn.Server.Application.Catalog.CatalogWriteConflict.DuplicateCode); }
    }
    public async Task<bool> SetActive(int id, bool active)
    {
        if (!_candidate) return await _oldWrites.SetProductActiveAsync(id, active, new DateTime(2026, 10, 18), 43, CancellationToken.None);
        using var db = _sessions.Borrow(_context, true);
        return await db.GetTable<Product>().Where(x => x.Id == id).Set(x => x.IsActive, active).Set(x => x.UpdatedAtUtc, new DateTime(2026, 10, 18)).Set(x => x.UpdatedByUserId, 43).UpdateAsync(CancellationToken.None) == 1;
    }
    public async Task<ProductDto?> Get(int id)
    {
        if (!_candidate) return await _oldReads.GetByIdAsync(id, CancellationToken.None);
        using var db = await _sessions.OpenOwnedAsync(CancellationToken.None);
        var p = await db.GetTable<Product>().SingleOrDefaultAsync(x => x.Id == id, CancellationToken.None);
        return p is null ? null : new ProductDto(p.Id, p.Code, p.Name, p.Unit, p.IsActive);
    }
    public async Task<IReadOnlyList<CatalogLookupDto>> Lookup(string? search)
    {
        if (!_candidate) return await _oldReads.LookupAsync(new CatalogLookupRequest(search, 3), CancellationToken.None);
        using var db = await _sessions.OpenOwnedAsync(CancellationToken.None); var normalized = Normalize(search);
        var rows = await db.GetTable<Product>().Where(x => x.IsActive && (normalized == null || x.Code.Contains(normalized) || x.Name.Contains(normalized)))
            .OrderBy(x => x.Code).ThenBy(x => x.Id).Take(3).ToListAsync(CancellationToken.None);
        return rows.Select(x => new CatalogLookupDto(x.Id, x.Code, x.Name, x.IsActive)).ToArray();
    }
    public async Task<PagedResult<ProductDto>> List(CatalogListRequest request)
    {
        if (!_candidate) return await _oldReads.ListAsync(request, CancellationToken.None);
        // Native escape: one COUNT_BIG/page batch, bigint offset, unchanged CHARINDEX/collation semantics.
        using var db = await _sessions.OpenOwnedAsync(CancellationToken.None);
        const string filter = "(@active=1 OR IsActive=1) AND (@s IS NULL OR CHARINDEX(@s,Code)>0 OR CHARINDEX(@s,Name)>0)";
        using var command = PilotSql.Command((SqlConnection)db.OpenDbConnection(), null,
            "SELECT COUNT_BIG(*) FROM dbo.Products WHERE " + filter + "; SELECT ProductId,Code,Name,Unit,IsActive FROM dbo.Products WHERE " + filter + " ORDER BY Code,ProductId OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY;",
            new SqlParameter("@s", SqlDbType.NVarChar, 4000) { Value = (object?)Normalize(request.Search) ?? DBNull.Value },
            PilotSql.P("@active", SqlDbType.Bit, request.IncludeInactive), PilotSql.P("@offset", SqlDbType.BigInt, (long)(request.PageNumber - 1) * request.PageSize), PilotSql.P("@size", SqlDbType.Int, request.PageSize));
        using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync(); var count = reader.GetInt64(0); await reader.NextResultAsync(); var rows = new List<ProductDto>();
        while (await reader.ReadAsync()) rows.Add(new ProductDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
        return new PagedResult<ProductDto>(rows, request.PageNumber, request.PageSize, count);
    }
    private static string? Normalize(string? text) => string.IsNullOrWhiteSpace(text) ? null : text!.Trim();
    private sealed class ProductMapping : ILinqToDbMapping
    {
        public void Configure(LinqToDB.Mapping.FluentMappingBuilder b)
        {
            var p = b.Entity<Product>().HasSchemaName("dbo").HasTableName("Products");
            p.Property(x => x.Id).HasColumnName("ProductId").IsPrimaryKey().IsIdentity();
            p.Property(x => x.Code).HasLength(32).HasDataType(LinqToDB.DataType.NVarChar); p.Property(x => x.Name).HasLength(256).HasDataType(LinqToDB.DataType.NVarChar); p.Property(x => x.Unit).HasLength(32).HasDataType(LinqToDB.DataType.NVarChar);
            p.Property(x => x.CreatedAtUtc).HasDataType(LinqToDB.DataType.DateTime2); p.Property(x => x.UpdatedAtUtc).HasDataType(LinqToDB.DataType.DateTime2);
        }
    }
}
