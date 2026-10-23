using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using MyDmsVn.Contracts;
using MyDmsVn.Contracts.Security;

namespace MyDmsVn.Desktop.Application
{
    public interface ISalesReceiptApiClient
    {
        // Bind once to the VM owner. Reject a different actual captured session
        // before admitting any operation, independently of cancellation.
        ISalesReceiptApiClient ForSession(long expectedSessionVersion);
        Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<SalesReceiptDto>> GetAsync(long saleId, CancellationToken cancellationToken);
        Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken cancellationToken);
    }

    public interface IProductApiClient
    {
        Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
    }

    public interface IWarehouseApiClient
    {
        Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> CreateAsync(SaveWarehouseRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
    }

    public interface IEmployeeApiClient
    {
        Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
    }

    public interface ICustomerApiClient
    {
        Task<ApiResponse<PagedResult<CustomerDto>>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> GetAsync(int id, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<CustomerDto>> UpdateAsync(int id, SaveCustomerRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken);
    }

    public interface IGoodsReceiptApiClient
    {
        Task<ApiResponse<GoodsReceiptDto>> CreateDraftAsync(SaveGoodsReceiptRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<GoodsReceiptDto>> UpdateDraftAsync(UpdateGoodsReceiptDraftRequest request, CancellationToken cancellationToken);
        Task<ApiResponse<PostGoodsReceiptResponse>> PostAsync(PostGoodsReceiptRequest request, CancellationToken cancellationToken);
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

    public interface IPermissionApiClient
    {
        Task<ApiResponse<PermissionDecisionDto>> CheckAsync(
            string permissionKey,
            CancellationToken cancellationToken);
    }

    public interface IPermissionCatalogApiClient
    {
        Task<ApiResponse<PermissionCatalogDto>> GetCatalogAsync(CancellationToken cancellationToken);
    }

    public interface ISecurityAdministrationApiClient
    {
        Task<ApiResponse<IReadOnlyList<SecurityUserDto>>> ListUsersAsync(CancellationToken cancellationToken);
        Task<ApiResponse<IReadOnlyList<SecurityRoleDto>>> ListRolesAsync(CancellationToken cancellationToken);
        Task<ApiResponse<SecurityAdministrationResultDto>> MutateAsync(
            SecurityAdministrationMutationRequest request,
            CancellationToken cancellationToken);
        Task<ApiResponse<PermissionAssignmentImportPreviewDto>> PreviewPermissionImportAsync(
            PermissionImportTargetTypeDto targetType,
            int targetId,
            string json,
            CancellationToken cancellationToken);
        Task<ApiResponse<PermissionAssignmentImportResultDto>> ApplyPermissionImportAsync(
            PermissionImportTargetTypeDto targetType,
            int targetId,
            PermissionAssignmentImportRequest request,
            CancellationToken cancellationToken);
    }
}
