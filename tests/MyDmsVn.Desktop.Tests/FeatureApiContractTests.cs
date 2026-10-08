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
    }
}
