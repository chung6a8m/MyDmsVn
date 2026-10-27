using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
namespace MyDmsVn.Desktop.Tests;
// Synthetic transport fixture only; no SQL, package, Login or production persistence claim.
internal sealed class CatalogPresentationClient : IProductApiClient, IWarehouseApiClient, IEmployeeApiClient, ICustomerApiClient
{
    internal bool FailList { get; set; }
    internal bool FailSave { get; set; }
    internal TaskCompletionSource<bool>? ListBarrier { get; set; }
    private readonly Dictionary<int, bool> _active = Enumerable.Range(1, 30).ToDictionary(i => i, i => i != 7 && i != 13 && i != 27);
    private readonly Dictionary<int, Tuple<string, string, string>> _saved = new Dictionary<int, Tuple<string, string, string>>();
    private T Row<T>(int id, Func<int, string, string, string, bool, T> create)
    {
        var saved = _saved.TryGetValue(id, out var values) ? values : Tuple.Create("SP" + id.ToString("D3"), id == 1 ? "A4 paper 80gsm" : id == 2 ? "Giấy in chuyên dụng — tên sản phẩm Unicode dài để kiểm tra mật độ và khả năng chỉnh sửa trong inspector" : "Product " + id + " — văn phòng phẩm", "ream");
        return create(id, saved.Item1, saved.Item2, saved.Item3, _active[id]);
    }
    private async Task<ApiResponse<PagedResult<T>>> List<T>(CatalogListRequest request, Func<int, string, string, string, bool, T> create)
    {
        var barrier = ListBarrier;
        if (barrier != null) await barrier.Task.ConfigureAwait(false);
        if (FailList) return ApiResponse<PagedResult<T>>.Failure(new ApiError(ApiStatusCode.BadRequest, "Synthetic", "Synthetic catalog failure"));
        var ids = _active.Keys.OrderBy(i => i).Where(i => string.IsNullOrEmpty(request.Search) || ("SP" + i.ToString("D3")).IndexOf(request.Search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
        return ApiResponse<PagedResult<T>>.Success(new PagedResult<T>(ids.Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).Select(i => Row(i, create)).ToArray(), request.PageNumber, request.PageSize, ids.Length));
    }
    private Task<ApiResponse<T>> Save<T>(int id, string code, string name, string extra, Func<int, string, string, string, bool, T> create)
    {
        if (FailSave) return Task.FromResult(ApiResponse<T>.Failure(new ApiError(ApiStatusCode.BadRequest, "ValidationError", "Correct the fields.", new[] { new ApiErrorDetail("Validation.Required", "Code is required.", "code") })));
        _active[id] = true; _saved[id] = Tuple.Create(code, name, extra);
        return Task.FromResult(ApiResponse<T>.Success(Row(id, create)));
    }
    private Task<ApiResponse<UnitResponse>> Active(int id, bool active) { _active[id] = active; return Task.FromResult(ApiResponse<UnitResponse>.Success(UnitResponse.Value)); }
    Task<ApiResponse<PagedResult<ProductDto>>> IProductApiClient.ListAsync(CatalogListRequest request, CancellationToken token) => List<ProductDto>(request, (i,c,n,e,a) => new ProductDto(i,c,n,e,a));
    Task<ApiResponse<ProductDto>> IProductApiClient.GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<ProductDto>.Success(Row<ProductDto>(id, (i,c,n,e,a) => new ProductDto(i,c,n,e,a))));
    Task<ApiResponse<ProductDto>> IProductApiClient.CreateAsync(SaveProductRequest request, CancellationToken token) => Save<ProductDto>(31, request.Code, request.Name, request.Unit, (i,c,n,e,a) => new ProductDto(i,c,n,e,a));
    Task<ApiResponse<ProductDto>> IProductApiClient.UpdateAsync(int id, SaveProductRequest request, CancellationToken token) => Save<ProductDto>(id, request.Code, request.Name, request.Unit, (i,c,n,e,a) => new ProductDto(i,c,n,e,a));
    Task<ApiResponse<UnitResponse>> IProductApiClient.SetActiveAsync(int id, bool active, CancellationToken token) => Active(id, active);
    Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> IProductApiClient.LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();

    Task<ApiResponse<PagedResult<WarehouseDto>>> IWarehouseApiClient.ListAsync(CatalogListRequest request, CancellationToken token) => List<WarehouseDto>(request, (i,c,n,e,a) => new WarehouseDto(i,c,n,e,a));
    Task<ApiResponse<WarehouseDto>> IWarehouseApiClient.GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<WarehouseDto>.Success(Row<WarehouseDto>(id, (i,c,n,e,a) => new WarehouseDto(i,c,n,e,a))));
    Task<ApiResponse<WarehouseDto>> IWarehouseApiClient.CreateAsync(SaveWarehouseRequest request, CancellationToken token) => Save<WarehouseDto>(31, request.Code, request.Name, request.Address ?? string.Empty, (i,c,n,e,a) => new WarehouseDto(i,c,n,e,a));
    Task<ApiResponse<WarehouseDto>> IWarehouseApiClient.UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken token) => Save<WarehouseDto>(id, request.Code, request.Name, request.Address ?? string.Empty, (i,c,n,e,a) => new WarehouseDto(i,c,n,e,a));
    Task<ApiResponse<UnitResponse>> IWarehouseApiClient.SetActiveAsync(int id, bool active, CancellationToken token) => Active(id, active);
    Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> IWarehouseApiClient.LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();

    Task<ApiResponse<PagedResult<EmployeeDto>>> IEmployeeApiClient.ListAsync(CatalogListRequest request, CancellationToken token) => List<EmployeeDto>(request, (i,c,n,e,a) => new EmployeeDto(i,c,n,e,12,a));
    Task<ApiResponse<EmployeeDto>> IEmployeeApiClient.GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<EmployeeDto>.Success(Row<EmployeeDto>(id, (i,c,n,e,a) => new EmployeeDto(i,c,n,e,12,a))));
    Task<ApiResponse<EmployeeDto>> IEmployeeApiClient.CreateAsync(SaveEmployeeRequest request, CancellationToken token) => Save<EmployeeDto>(31, request.Code, request.Name, request.Phone ?? string.Empty, (i,c,n,e,a) => new EmployeeDto(i,c,n,e,12,a));
    Task<ApiResponse<EmployeeDto>> IEmployeeApiClient.UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken token) => Save<EmployeeDto>(id, request.Code, request.Name, request.Phone ?? string.Empty, (i,c,n,e,a) => new EmployeeDto(i,c,n,e,12,a));
    Task<ApiResponse<UnitResponse>> IEmployeeApiClient.SetActiveAsync(int id, bool active, CancellationToken token) => Active(id, active);
    Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> IEmployeeApiClient.LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();

    Task<ApiResponse<PagedResult<CustomerDto>>> ICustomerApiClient.ListAsync(CatalogListRequest request, CancellationToken token) => List<CustomerDto>(request, (i,c,n,e,a) => new CustomerDto(i,c,n,e,"0123","TAX",a));
    Task<ApiResponse<CustomerDto>> ICustomerApiClient.GetAsync(int id, CancellationToken token) => Task.FromResult(ApiResponse<CustomerDto>.Success(Row<CustomerDto>(id, (i,c,n,e,a) => new CustomerDto(i,c,n,e,"0123","TAX",a))));
    Task<ApiResponse<CustomerDto>> ICustomerApiClient.CreateAsync(SaveCustomerRequest request, CancellationToken token) => Save<CustomerDto>(31, request.Code, request.Name, request.Address ?? string.Empty, (i,c,n,e,a) => new CustomerDto(i,c,n,e,"0123","TAX",a));
    Task<ApiResponse<CustomerDto>> ICustomerApiClient.UpdateAsync(int id, SaveCustomerRequest request, CancellationToken token) => Save<CustomerDto>(id, request.Code, request.Name, request.Address ?? string.Empty, (i,c,n,e,a) => new CustomerDto(i,c,n,e,"0123","TAX",a));
    Task<ApiResponse<UnitResponse>> ICustomerApiClient.SetActiveAsync(int id, bool active, CancellationToken token) => Active(id, active);
    Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> ICustomerApiClient.LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
}
