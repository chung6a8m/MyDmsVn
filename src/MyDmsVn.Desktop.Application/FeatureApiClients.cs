using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public interface IProductApiClient
    {
        Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
    }

    public interface IWarehouseApiClient
    {
        Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> CreateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
    }

    public interface IEmployeeApiClient
    {
        Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
    }

    public interface ICustomerApiClient
    {
        Task<ApiResponse<PagedResult<CustomerDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> UpdateAsync(int id, SaveCustomerRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
    }

    public interface IGoodsReceiptApiClient
    {
        Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(long id, SaveGoodsReceiptRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(long id, CancellationToken cancellationToken);
        Task<ApiResponse<GoodsReceiptDto>> GetAsync(long id, CancellationToken cancellationToken);
        Task<ApiResponse<PagedResult<GoodsReceiptDto>>> ListAsync(GoodsReceiptListRequest request, CancellationToken cancellationToken);
    }

    public interface IInventoryApiClient
    {
        Task<ApiResponse<PagedResult<StockBalanceRow>>> GetStockBalancesAsync(StockBalanceQuery request, CancellationToken cancellationToken);
        Task<ApiResponse<PagedResult<StockCardRow>>> GetStockCardAsync(StockCardQuery request, CancellationToken cancellationToken);
    }

    public interface IIdentityApiClient
    {
        Task<ApiResponse<CurrentUserDto>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<CurrentUserDto>> GetCurrentUserAsync(CancellationToken cancellationToken);
    }
}
