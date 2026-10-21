using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Sales;
using MyDmsVn.Server.Domain.Catalog;

namespace MyDmsVn.Server.Infrastructure.Sales;

internal static class SalesReferenceLoader
{
    public static async Task<SalesReceiptReferenceSnapshot> LoadAsync(DataConnection db, int customerId, int warehouseId, int employeeId,
        IReadOnlyCollection<int> productIds, CancellationToken token, bool orderedProductLocks = true)
    {
        // Keep references stable through this save/read transaction, in the common reference order.
        var warehouse = await db.GetTable<Warehouse>().TableHint("HOLDLOCK").SingleOrDefaultAsync(row => row.Id == warehouseId, token).ConfigureAwait(false);
        var employee = await db.GetTable<Employee>().TableHint("HOLDLOCK").SingleOrDefaultAsync(row => row.Id == employeeId, token).ConfigureAwait(false);
        var customer = await db.GetTable<Customer>().TableHint("HOLDLOCK").SingleOrDefaultAsync(row => row.Id == customerId, token).ConfigureAwait(false);
        var products = new Dictionary<int, SalesProductReference>();
        var ids = productIds.Distinct().OrderBy(id => id).ToArray();
        if (orderedProductLocks)
        {
            foreach (var id in ids)
            {
                var product = await db.GetTable<Product>().TableHint("HOLDLOCK").SingleOrDefaultAsync(row => row.Id == id, token).ConfigureAwait(false);
                products.Add(id, Reference(product));
            }
        }
        else
        {
            // Detail is already SERIALIZABLE; load its bounded Product set in one query.
            var rows = await db.GetTable<Product>().Where(row => ids.Contains(row.Id)).ToArrayAsync(token).ConfigureAwait(false);
            var byId = rows.ToDictionary(row => row.Id);
            foreach (var id in ids) products.Add(id, Reference(byId.TryGetValue(id, out var product) ? product : null));
        }
        return new SalesReceiptReferenceSnapshot(
            new SalesCatalogReference(State(customer?.IsActive), customer == null ? null : customer.Code + " - " + customer.Name),
            new SalesCatalogReference(State(warehouse?.IsActive), warehouse == null ? null : warehouse.Code + " - " + warehouse.Name),
            new SalesCatalogReference(State(employee?.IsActive), employee == null ? null : employee.Code + " - " + employee.Name), products);
    }
    private static SalesProductReference Reference(Product? product) =>
        new SalesProductReference(State(product?.IsActive), product == null ? null : product.Code + " - " + product.Name, product?.Unit);
    private static CatalogReferenceState State(bool? active) => active.HasValue ? active.Value ? CatalogReferenceState.Active : CatalogReferenceState.Inactive : CatalogReferenceState.Missing;
}
