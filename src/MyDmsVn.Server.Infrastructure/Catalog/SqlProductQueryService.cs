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

internal sealed class SqlProductQueryService : IProductQueryService
{
    private const string Filter =
        "(@IncludeInactive = 1 OR IsActive = 1) " +
        "AND (@Search IS NULL OR CHARINDEX(@Search, Code) > 0 OR CHARINDEX(@Search, Name) > 0)";

    private readonly IDbConnectionFactory _connectionFactory;

    public SqlProductQueryService(IDbConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory;

    public async Task<PagedResult<ProductDto>> ListAsync(
        CatalogListRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var parameters = new
        {
            Search = NormalizeSearch(request.Search),
            request.IncludeInactive,
            Offset = (long)(request.PageNumber - 1) * request.PageSize,
            request.PageSize,
        };
        var command = new CommandDefinition(
            "SELECT COUNT_BIG(*) FROM dbo.Products WHERE " + Filter + "; " +
            "SELECT ProductId AS Id, Code, Name, Unit, IsActive FROM dbo.Products WHERE " + Filter + " " +
            "ORDER BY Code, ProductId OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            parameters,
            cancellationToken: cancellationToken);
        using var results = await connection.QueryMultipleAsync(command).ConfigureAwait(false);
        var total = await results.ReadSingleAsync<long>().ConfigureAwait(false);
        var rows = (await results.ReadAsync<ProductRow>().ConfigureAwait(false)).ToArray();
        return new PagedResult<ProductDto>(
            rows.Select(ToDto),
            request.PageNumber,
            request.PageSize,
            total);
    }

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var command = new CommandDefinition(
            "SELECT ProductId AS Id, Code, Name, Unit, IsActive FROM dbo.Products WHERE ProductId = @Id;",
            new { Id = id },
            cancellationToken: cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProductRow>(command).ConfigureAwait(false);
        return row == null ? null : ToDto(row);
    }

    public async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(
        CatalogLookupRequest request,
        CancellationToken cancellationToken)
    {
        using var connection = await _connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        var command = new CommandDefinition(
            "SELECT TOP (@Limit) ProductId AS Id, Code, Name, IsActive " +
            "FROM dbo.Products WHERE IsActive = 1 " +
            "AND (@Search IS NULL OR CHARINDEX(@Search, Code) > 0 OR CHARINDEX(@Search, Name) > 0) " +
            "ORDER BY Code, ProductId;",
            new { Search = NormalizeSearch(request.Search), request.Limit },
            cancellationToken: cancellationToken);
        var rows = await connection.QueryAsync<LookupRow>(command).ConfigureAwait(false);
        return rows.Select(row => new CatalogLookupDto(row.Id, row.Code, row.Name, row.IsActive)).ToArray();
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search!.Trim();

    private static ProductDto ToDto(ProductRow row) =>
        new ProductDto(row.Id, row.Code, row.Name, row.Unit, row.IsActive);

    private sealed class ProductRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class LookupRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
