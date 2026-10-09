using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal abstract class SqlCatalogQueryServiceBase
{
    private readonly IDbConnectionFactory _connectionFactory;

    protected SqlCatalogQueryServiceBase(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    protected async Task<PagedResult<TDto>> ListAsync<TRow, TDto>(
        string table,
        string keyColumn,
        string selectColumns,
        CatalogListRequest request,
        Func<TRow, TDto> map,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        const string filter = "(@IncludeInactive = 1 OR IsActive = 1) AND (@Search IS NULL OR CHARINDEX(@Search, Code) > 0 OR CHARINDEX(@Search, Name) > 0)";
        var command = new CommandDefinition(
            "SELECT COUNT_BIG(*) FROM " + table + " WHERE " + filter + "; " +
            "SELECT " + selectColumns + " FROM " + table + " WHERE " + filter +
            " ORDER BY Code, " + keyColumn + " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            new
            {
                Search = NormalizeSearch(request.Search),
                request.IncludeInactive,
                Offset = (long)(request.PageNumber - 1) * request.PageSize,
                request.PageSize,
            },
            cancellationToken: cancellationToken);
        using var results = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var total = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        var rows = await results.ReadAsync<TRow>().ConfigureAwait(false);
        return new PagedResult<TDto>(rows.Select(map), request.PageNumber, request.PageSize, total);
    }

    protected async Task<TDto?> GetAsync<TRow, TDto>(
        string table,
        string keyColumn,
        string selectColumns,
        int id,
        Func<TRow, TDto> map,
        CancellationToken cancellationToken)
        where TRow : class
        where TDto : class
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            "SELECT " + selectColumns + " FROM " + table + " WHERE " + keyColumn + " = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TRow>(command).ConfigureAwait(false);
        return row == null ? null : map(row);
    }

    protected async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(
        string table,
        string keyColumn,
        CatalogLookupRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(
            "SELECT TOP (@Limit) " + keyColumn + " AS Id, Code, Name, IsActive FROM " + table +
            " WHERE IsActive = 1 AND (@Search IS NULL OR CHARINDEX(@Search, Code) > 0 OR CHARINDEX(@Search, Name) > 0)" +
            " ORDER BY Code, " + keyColumn + ";",
            new { Search = NormalizeSearch(request.Search), request.Limit },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupRow>(command).ConfigureAwait(false);
        return rows.Select(row => new CatalogLookupDto(row.Id, row.Code, row.Name, row.IsActive)).ToArray();
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search!.Trim();

    private sealed class LookupRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}

internal sealed class SqlWarehouseQueryService : SqlCatalogQueryServiceBase, IWarehouseQueryService
{
    public SqlWarehouseQueryService(IDbConnectionFactory factory) : base(factory) { }
    public Task<PagedResult<WarehouseDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        ListAsync<WarehouseRow, WarehouseDto>("dbo.Warehouses", "WarehouseId", "WarehouseId AS Id, Code, Name, Address, IsActive", request, ToDto, token);
    public Task<WarehouseDto?> GetByIdAsync(int id, CancellationToken token) =>
        GetAsync<WarehouseRow, WarehouseDto>("dbo.Warehouses", "WarehouseId", "WarehouseId AS Id, Code, Name, Address, IsActive", id, ToDto, token);
    public Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
        LookupAsync("dbo.Warehouses", "WarehouseId", request, token);
    private static WarehouseDto ToDto(WarehouseRow row) => new WarehouseDto(row.Id, row.Code, row.Name, row.Address, row.IsActive);
    private sealed class WarehouseRow { public int Id { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string? Address { get; set; } public bool IsActive { get; set; } }
}

internal sealed class SqlEmployeeQueryService : SqlCatalogQueryServiceBase, IEmployeeQueryService
{
    public SqlEmployeeQueryService(IDbConnectionFactory factory) : base(factory) { }
    public Task<PagedResult<EmployeeDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        ListAsync<EmployeeRow, EmployeeDto>("dbo.Employees", "EmployeeId", "EmployeeId AS Id, Code, Name, Phone, UserId, IsActive", request, ToDto, token);
    public Task<EmployeeDto?> GetByIdAsync(int id, CancellationToken token) =>
        GetAsync<EmployeeRow, EmployeeDto>("dbo.Employees", "EmployeeId", "EmployeeId AS Id, Code, Name, Phone, UserId, IsActive", id, ToDto, token);
    public Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
        LookupAsync("dbo.Employees", "EmployeeId", request, token);
    private static EmployeeDto ToDto(EmployeeRow row) => new EmployeeDto(row.Id, row.Code, row.Name, row.Phone, row.UserId, row.IsActive);
    private sealed class EmployeeRow { public int Id { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string? Phone { get; set; } public int? UserId { get; set; } public bool IsActive { get; set; } }
}

internal sealed class SqlCustomerQueryService : SqlCatalogQueryServiceBase, ICustomerQueryService
{
    public SqlCustomerQueryService(IDbConnectionFactory factory) : base(factory) { }
    public Task<PagedResult<CustomerDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        ListAsync<CustomerRow, CustomerDto>("dbo.Customers", "CustomerId", "CustomerId AS Id, Code, Name, Address, Phone, TaxCode, IsActive", request, ToDto, token);
    public Task<CustomerDto?> GetByIdAsync(int id, CancellationToken token) =>
        GetAsync<CustomerRow, CustomerDto>("dbo.Customers", "CustomerId", "CustomerId AS Id, Code, Name, Address, Phone, TaxCode, IsActive", id, ToDto, token);
    public Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token) =>
        LookupAsync("dbo.Customers", "CustomerId", request, token);
    private static CustomerDto ToDto(CustomerRow row) => new CustomerDto(row.Id, row.Code, row.Name, row.Address, row.Phone, row.TaxCode, row.IsActive);
    private sealed class CustomerRow { public int Id { get; set; } public string Code { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string? Address { get; set; } public string? Phone { get; set; } public string? TaxCode { get; set; } public bool IsActive { get; set; } }
}
