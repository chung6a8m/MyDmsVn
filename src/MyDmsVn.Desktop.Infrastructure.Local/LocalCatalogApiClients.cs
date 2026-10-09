using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Catalog;

namespace MyDmsVn.Desktop.Infrastructure.Local
{
    internal abstract class LocalCatalogApiClientBase
    {
        protected LocalCatalogApiClientBase(ISender sender) =>
            Sender = sender ?? throw new ArgumentNullException(nameof(sender));

        protected ISender Sender { get; }

        protected async Task<ApiResponse<T>> SendAsync<T>(ApplicationRequest<T> request, CancellationToken cancellationToken)
        {
            var result = await Sender.Send(request, cancellationToken).ConfigureAwait(false);
            return ApiResponseMapper.Map(result, "Request");
        }
    }

    internal sealed class LocalProductApiClient : LocalCatalogApiClientBase, IProductApiClient
    {
        public LocalProductApiClient(ISender sender) : base(sender) { }
        public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => SendAsync(new ListProductsQuery(request), token);
        public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken token) => SendAsync(new GetProductByIdQuery(id), token);
        public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken token) => SendAsync(new CreateProductCommand(request), token);
        public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken token) => SendAsync(new UpdateProductCommand(id, request), token);
        public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken token) => SendAsync(new SetProductActiveCommand(id, isActive), token);
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => SendAsync(new LookupProductsQuery(request), token);
    }

    internal sealed class LocalWarehouseApiClient : LocalCatalogApiClientBase, IWarehouseApiClient
    {
        public LocalWarehouseApiClient(ISender sender) : base(sender) { }
        public Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => SendAsync(new ListWarehousesQuery(request), token);
        public Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken token) => SendAsync(new GetWarehouseByIdQuery(id), token);
        public Task<ApiResponse<WarehouseDto>> CreateAsync(SaveWarehouseRequest request, CancellationToken token) => SendAsync(new CreateWarehouseCommand(request), token);
        public Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken token) => SendAsync(new UpdateWarehouseCommand(id, request), token);
        public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken token) => SendAsync(new SetWarehouseActiveCommand(id, isActive), token);
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => SendAsync(new LookupWarehousesQuery(request), token);
    }

    internal sealed class LocalEmployeeApiClient : LocalCatalogApiClientBase, IEmployeeApiClient
    {
        public LocalEmployeeApiClient(ISender sender) : base(sender) { }
        public Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => SendAsync(new ListEmployeesQuery(request), token);
        public Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken token) => SendAsync(new GetEmployeeByIdQuery(id), token);
        public Task<ApiResponse<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken token) => SendAsync(new CreateEmployeeCommand(request), token);
        public Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken token) => SendAsync(new UpdateEmployeeCommand(id, request), token);
        public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken token) => SendAsync(new SetEmployeeActiveCommand(id, isActive), token);
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => SendAsync(new LookupEmployeesQuery(request), token);
    }

    internal sealed class LocalCustomerApiClient : LocalCatalogApiClientBase, ICustomerApiClient
    {
        public LocalCustomerApiClient(ISender sender) : base(sender) { }
        public Task<ApiResponse<PagedResult<CustomerDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => SendAsync(new ListCustomersQuery(request), token);
        public Task<ApiResponse<CustomerDto>> GetAsync(int id, CancellationToken token) => SendAsync(new GetCustomerByIdQuery(id), token);
        public Task<ApiResponse<CustomerDto>> CreateAsync(SaveCustomerRequest request, CancellationToken token) => SendAsync(new CreateCustomerCommand(request), token);
        public Task<ApiResponse<CustomerDto>> UpdateAsync(int id, SaveCustomerRequest request, CancellationToken token) => SendAsync(new UpdateCustomerCommand(id, request), token);
        public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken token) => SendAsync(new SetCustomerActiveCommand(id, isActive), token);
        public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => SendAsync(new LookupCustomersQuery(request), token);
    }
}
