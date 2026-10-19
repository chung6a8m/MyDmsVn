using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure.Persistence;

namespace MyDmsVn.Server.Infrastructure.Catalog;

internal sealed class SqlProductQueryService(ILinqToDbSessionFactory sessions) : IProductQueryService
{
    public Task<PagedResult<ProductDto>> ListAsync(CatalogListRequest request, CancellationToken token) =>
        SqlCatalogPageReader.ReadAsync(sessions, "dbo.Products", "ProductId", "ProductId AS Id, Code, Name, Unit, IsActive",
            request, row => new ProductDto(row.GetInt32(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetBoolean(4)), token);

    public async Task<ProductDto?> GetByIdAsync(int id, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        return await db.GetTable<Product>().Where(e => e.Id == id)
            .Select(e => new ProductDto(e.Id, e.Code, e.Name, e.Unit, e.IsActive))
            .SingleOrDefaultAsync(token).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(CatalogLookupRequest request, CancellationToken token)
    {
        using var db = await sessions.OpenOwnedAsync(token).ConfigureAwait(false);
        var search = SqlCatalogPageReader.NormalizeSearch(request.Search);
        return await db.GetTable<Product>()
            .Where(e => e.IsActive && (search == null || e.Code.Contains(search) || e.Name.Contains(search)))
            .OrderBy(e => e.Code).ThenBy(e => e.Id).Take(request.Limit)
            .Select(e => new CatalogLookupDto(e.Id, e.Code, e.Name, e.IsActive))
            .ToArrayAsync(token).ConfigureAwait(false);
    }
}
