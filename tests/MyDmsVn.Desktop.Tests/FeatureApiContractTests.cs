using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class FeatureApiContractTests
    {
        [Fact]
        public void Feature_clients_are_async_cancellable_and_return_only_api_responses()
        {
            var clientTypes = new[]
            {
                typeof(IProductApiClient),
                typeof(IWarehouseApiClient),
                typeof(IEmployeeApiClient),
                typeof(ICustomerApiClient),
                typeof(IGoodsReceiptApiClient),
                typeof(IInventoryApiClient),
                typeof(IIdentityApiClient),
            };

            foreach (var method in clientTypes.SelectMany(type => type.GetMethods()))
            {
                Assert.Equal(typeof(CancellationToken), method.GetParameters().Last().ParameterType);
                Assert.True(method.ReturnType.IsGenericType);
                Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
                var taskResult = method.ReturnType.GetGenericArguments()[0];
                Assert.True(taskResult.IsGenericType);
                Assert.Equal(typeof(ApiResponse<>), taskResult.GetGenericTypeDefinition());
            }
        }

        [Fact]
        public void Goods_receipt_update_and_post_clients_require_versioned_request_dtos()
        {
            var updateParameter = typeof(IGoodsReceiptApiClient)
                .GetMethod(nameof(IGoodsReceiptApiClient.UpdateDraftAsync))!
                .GetParameters()
                .First();
            var postParameter = typeof(IGoodsReceiptApiClient)
                .GetMethod(nameof(IGoodsReceiptApiClient.PostAsync))!
                .GetParameters()
                .First();

            Assert.Equal(
                "MyDmsVn.Contracts.UpdateGoodsReceiptDraftRequest",
                updateParameter.ParameterType.FullName);
            Assert.Equal(
                "MyDmsVn.Contracts.PostGoodsReceiptRequest",
                postParameter.ParameterType.FullName);
        }

        [Fact]
        public void Inventory_client_is_query_only_with_no_balance_or_ledger_mutation_api()
        {
            var methods = typeof(IInventoryApiClient).GetMethods();

            Assert.Equal(
                new[] { "GetStockBalancesAsync", "GetStockCardAsync" },
                methods.Select(method => method.Name).OrderBy(name => name).ToArray());
            Assert.All(
                methods,
                method => Assert.StartsWith("Get", method.Name, StringComparison.Ordinal));
        }
    }
}
