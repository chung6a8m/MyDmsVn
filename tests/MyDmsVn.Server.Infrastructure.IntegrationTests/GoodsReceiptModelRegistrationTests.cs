using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Server.Infrastructure.Persistence;
using Xunit;

namespace MyDmsVn.Server.Infrastructure.IntegrationTests;

public sealed class GoodsReceiptModelRegistrationTests
{
    [Fact]
    public void Sql_persistence_registers_goods_receipt_models_and_mapping()
    {
        var domainAssembly = typeof(MyDmsVn.Server.Domain.AssemblyMarker).Assembly;
        var receiptType = domainAssembly.GetType("MyDmsVn.Server.Domain.Inventory.GoodsReceipt");
        var lineType = domainAssembly.GetType("MyDmsVn.Server.Domain.Inventory.GoodsReceiptLine");
        var services = new ServiceCollection();

        services.AddSqlPersistence("Server=invalid;Database=unused;Integrated Security=true");
        using var provider = services.BuildServiceProvider();
        var mappingTypes = provider
            .GetServices<IRepoDbMapping>()
            .Select(mapping => mapping.GetType().FullName)
            .ToArray();

        Assert.NotNull(receiptType);
        Assert.NotNull(lineType);
        Assert.Contains("MyDmsVn.Server.Infrastructure.Inventory.InventoryRepoDbMapping", mappingTypes);
    }
}
