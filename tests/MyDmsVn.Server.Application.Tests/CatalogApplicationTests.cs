using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Catalog;
using Xunit;

namespace MyDmsVn.Server.Application.Tests
{
    public sealed class CatalogApplicationTests
    {
        [Fact]
        public async Task Create_product_commits_authenticated_user_audit_and_returns_contract_dto()
        {
            var repository = new FakeCatalogWriteRepository();
            var unitOfWork = new FakeUnitOfWork(repository);
            using var provider = CreateProvider(unitOfWork);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new CreateProductCommand(new SaveProductRequest("SP001", "Sản phẩm", "Cái")),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(41, result.Value.Id);
            Assert.Equal("SP001", result.Value.Code);
            Assert.Equal(42, repository.InsertedProduct!.CreatedByUserId);
            Assert.True(unitOfWork.BeganTransaction);
            Assert.True(unitOfWork.Committed);
        }

        [Fact]
        public async Task Product_validation_rejects_display_whitespace_before_opening_a_unit_of_work()
        {
            var repository = new FakeCatalogWriteRepository();
            var unitOfWork = new FakeUnitOfWork(repository);
            using var provider = CreateProvider(unitOfWork);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new CreateProductCommand(new SaveProductRequest("\u00a0", "Sản phẩm", "Cái")),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Contains(result.Errors, error =>
                error.Code == "Validation.Required" &&
                error.Metadata != null &&
                error.Metadata.TryGetValue("Field", out var field) &&
                Equals(field, "Request.Code"));
            Assert.False(unitOfWork.BeganTransaction);
        }

        [Fact]
        public async Task Duplicate_product_code_maps_to_deterministic_conflict_and_does_not_commit()
        {
            var repository = new FakeCatalogWriteRepository
            {
                InsertFailure = new CatalogWriteConflictException(CatalogWriteConflict.DuplicateCode),
            };
            var unitOfWork = new FakeUnitOfWork(repository);
            using var provider = CreateProvider(unitOfWork);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new CreateProductCommand(new SaveProductRequest("SP001", "Sản phẩm", "Cái")),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Product.DuplicateCode", result.FirstError.Code);
            Assert.False(unitOfWork.Committed);
        }

        [Fact]
        public async Task Product_list_query_preserves_deterministic_page_from_query_service()
        {
            var queryService = new FakeProductQueryService();
            using var provider = CreateProvider(new FakeUnitOfWork(new FakeCatalogWriteRepository()), queryService);
            var request = new CatalogListRequest("sp", 2, 25, includeInactive: true);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new ListProductsQuery(request),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Equal(26, result.Value.Items[0].Id);
            Assert.Equal(2, result.Value.PageNumber);
            Assert.Same(request, queryService.LastListRequest);
        }

        [Fact]
        public async Task Update_product_persists_update_audit_and_returns_not_found_when_missing()
        {
            var repository = new FakeCatalogWriteRepository { ProductUpdateFound = false };
            var unitOfWork = new FakeUnitOfWork(repository);
            using var provider = CreateProvider(unitOfWork);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new UpdateProductCommand(99, new SaveProductRequest("SP099", "Mới", "Hộp")),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Product.NotFound", result.FirstError.Code);
            Assert.Equal(42, repository.UpdatedProduct!.UpdatedByUserId);
            Assert.Equal(new DateTime(2026, 10, 9, 2, 3, 4, DateTimeKind.Utc), repository.UpdatedProduct.UpdatedAtUtc);
            Assert.False(unitOfWork.Committed);
        }

        [Fact]
        public async Task Set_product_active_commits_requested_state_with_update_audit()
        {
            var repository = new FakeCatalogWriteRepository();
            var unitOfWork = new FakeUnitOfWork(repository);
            using var provider = CreateProvider(unitOfWork);

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new SetProductActiveCommand(41, false),
                CancellationToken.None);

            Assert.False(result.IsError);
            Assert.False(repository.ProductActiveValue);
            Assert.Equal(42, repository.ProductActiveUserId);
            Assert.True(unitOfWork.Committed);
        }

        [Fact]
        public async Task Product_get_and_lookup_map_missing_and_active_only_query_contracts()
        {
            var queryService = new FakeProductQueryService();
            using var provider = CreateProvider(new FakeUnitOfWork(new FakeCatalogWriteRepository()), queryService);
            var sender = provider.GetRequiredService<MediatR.ISender>();

            var missing = await sender.Send(new GetProductByIdQuery(404), CancellationToken.None);
            var lookupRequest = new CatalogLookupRequest("sp", 10);
            var lookup = await sender.Send(new LookupProductsQuery(lookupRequest), CancellationToken.None);

            Assert.True(missing.IsError);
            Assert.Equal("Product.NotFound", missing.FirstError.Code);
            Assert.False(lookup.IsError);
            Assert.All(lookup.Value, item => Assert.True(item.IsActive));
            Assert.Same(lookupRequest, queryService.LastLookupRequest);
        }

        [Fact]
        public void Catalog_requests_declare_exact_read_and_write_permissions()
        {
            Assert.Equal(PermissionKeys.CatalogProductsWrite,
                ((IAuthorizedRequest)new CreateProductCommand(new SaveProductRequest("P", "P", "P"))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogWarehousesWrite,
                ((IAuthorizedRequest)new CreateWarehouseCommand(new SaveWarehouseRequest("W", "W", null))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogEmployeesWrite,
                ((IAuthorizedRequest)new CreateEmployeeCommand(new SaveEmployeeRequest("E", "E", null, null))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogCustomersWrite,
                ((IAuthorizedRequest)new CreateCustomerCommand(new SaveCustomerRequest("C", "C", null, null, null))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogProductsRead,
                ((IAuthorizedRequest)new LookupProductsQuery(new CatalogLookupRequest(null, 10))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogWarehousesRead,
                ((IAuthorizedRequest)new LookupWarehousesQuery(new CatalogLookupRequest(null, 10))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogEmployeesRead,
                ((IAuthorizedRequest)new LookupEmployeesQuery(new CatalogLookupRequest(null, 10))).PermissionKey);
            Assert.Equal(PermissionKeys.CatalogCustomersRead,
                ((IAuthorizedRequest)new LookupCustomersQuery(new CatalogLookupRequest(null, 10))).PermissionKey);
        }

        [Fact]
        public async Task Duplicate_employee_user_link_maps_to_specific_conflict()
        {
            var repository = new FakeCatalogWriteRepository
            {
                InsertFailure = new CatalogWriteConflictException(CatalogWriteConflict.EmployeeUserAlreadyLinked),
            };
            using var provider = CreateProvider(new FakeUnitOfWork(repository));

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new CreateEmployeeCommand(new SaveEmployeeRequest("NV01", "Nhân viên", null, 12)),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal("Employee.UserAlreadyLinked", result.FirstError.Code);
        }

        [Fact]
        public async Task Denied_catalog_mutation_never_opens_a_unit_of_work()
        {
            var unitOfWork = new FakeUnitOfWork(new FakeCatalogWriteRepository());
            var services = new ServiceCollection();
            services.AddSingleton<IUnitOfWorkFactory>(new FakeUnitOfWorkFactory(unitOfWork));
            services.AddSingleton<ICurrentUserAccessor>(
                new StubCurrentUserAccessor(CurrentUser.Authenticated(42, "operator", "Operator", true)));
            services.AddSingleton<IPermissionStore>(new DenyPermissionStore());
            services.AddSingleton<IProductQueryService>(new FakeProductQueryService());
            services.AddServerApplication();
            using var provider = services.BuildServiceProvider();

            var result = await provider.GetRequiredService<MediatR.ISender>().Send(
                new CreateProductCommand(new SaveProductRequest("SP001", "Sản phẩm", "Cái")),
                CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(ErrorOr.ErrorType.Forbidden, result.FirstError.Type);
            Assert.False(unitOfWork.BeganTransaction);
        }

        private static ServiceProvider CreateProvider(
            FakeUnitOfWork unitOfWork,
            IProductQueryService? productQueryService = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IUnitOfWorkFactory>(new FakeUnitOfWorkFactory(unitOfWork));
            services.AddSingleton<ICurrentUserAccessor>(
                new StubCurrentUserAccessor(
                    CurrentUser.Authenticated(42, "operator", "Operator", isActive: true)));
            services.AddSingleton<IPermissionStore>(new AllowPermissionStore());
            services.AddSingleton<IUtcClock>(new FixedUtcClock());
            services.AddSingleton(productQueryService ?? new FakeProductQueryService());

            services.AddServerApplication();
            return services.BuildServiceProvider();
        }

        private sealed class FixedUtcClock : IUtcClock
        {
            public DateTime UtcNow { get; } =
                new DateTime(2026, 10, 9, 2, 3, 4, DateTimeKind.Utc);
        }

        private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
        {
            public StubCurrentUserAccessor(CurrentUser current) => Current = current;

            public CurrentUser Current { get; }
        }

        private sealed class AllowPermissionStore : IPermissionStore
        {
            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new PermissionSnapshot(true, false));
            }
        }

        private sealed class DenyPermissionStore : IPermissionStore
        {
            public Task<PermissionSnapshot> GetSnapshotAsync(int userId, string permissionKey, CancellationToken cancellationToken) =>
                Task.FromResult(new PermissionSnapshot(false, true));
        }

        private sealed class FakeUnitOfWorkFactory : IUnitOfWorkFactory
        {
            private readonly FakeUnitOfWork _unitOfWork;

            public FakeUnitOfWorkFactory(FakeUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

            public IUnitOfWork Create() => _unitOfWork;

            public Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) =>
                Task.FromResult<IUnitOfWork>(_unitOfWork);
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            private readonly ICatalogWriteRepository _repository;

            public FakeUnitOfWork(ICatalogWriteRepository repository) => _repository = repository;

            public UnitOfWorkState State { get; private set; } = UnitOfWorkState.Created;
            public bool BeganTransaction { get; private set; }
            public bool Committed { get; private set; }

            public void BeginTransaction()
            {
                BeganTransaction = true;
                State = UnitOfWorkState.ActiveTransaction;
            }

            public TRepository Repository<TRepository>() where TRepository : class =>
                (TRepository)_repository;

            public void Commit()
            {
                Committed = true;
                State = UnitOfWorkState.Committed;
            }

            public void Rollback() => State = UnitOfWorkState.RolledBack;

            public void Dispose() => State = UnitOfWorkState.Disposed;
        }

        private sealed class FakeCatalogWriteRepository : ICatalogWriteRepository
        {
            public Product? InsertedProduct { get; private set; }
            public Product? UpdatedProduct { get; private set; }
            public Exception? InsertFailure { get; set; }
            public bool ProductUpdateFound { get; set; } = true;
            public bool ProductActiveFound { get; set; } = true;
            public bool ProductActiveValue { get; private set; }
            public int? ProductActiveUserId { get; private set; }

            public Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken)
            {
                InsertedProduct = product;
                if (InsertFailure != null)
                {
                    throw InsertFailure;
                }

                return Task.FromResult(41);
            }

            public Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
                InsertOrThrow(warehouse, 1);

            public Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
                InsertOrThrow(employee, 1);

            public Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
                InsertOrThrow(customer, 1);

            public Task<bool> UpdateProductAsync(Product product, CancellationToken cancellationToken)
            {
                UpdatedProduct = product;
                return Task.FromResult(ProductUpdateFound);
            }

            public Task<bool> SetProductActiveAsync(
                int id,
                bool isActive,
                DateTime updatedAtUtc,
                int? updatedByUserId,
                CancellationToken cancellationToken)
            {
                ProductActiveValue = isActive;
                ProductActiveUserId = updatedByUserId;
                return Task.FromResult(ProductActiveFound);
            }

            public Task<bool> UpdateWarehouseAsync(Warehouse entity, CancellationToken token) => Task.FromResult(true);
            public Task<bool> SetWarehouseActiveAsync(int id, bool active, DateTime at, int? by, CancellationToken token) => Task.FromResult(true);
            public Task<bool> UpdateEmployeeAsync(Employee entity, CancellationToken token) => Task.FromResult(true);
            public Task<bool> SetEmployeeActiveAsync(int id, bool active, DateTime at, int? by, CancellationToken token) => Task.FromResult(true);
            public Task<bool> UpdateCustomerAsync(Customer entity, CancellationToken token) => Task.FromResult(true);
            public Task<bool> SetCustomerActiveAsync(int id, bool active, DateTime at, int? by, CancellationToken token) => Task.FromResult(true);

            private Task<int> InsertOrThrow(object entity, int id)
            {
                if (InsertFailure != null)
                {
                    throw InsertFailure;
                }

                return Task.FromResult(id);
            }
        }

        private sealed class FakeProductQueryService : IProductQueryService
        {
            public CatalogListRequest? LastListRequest { get; private set; }
            public CatalogLookupRequest? LastLookupRequest { get; private set; }

            public Task<PagedResult<ProductDto>> ListAsync(
                CatalogListRequest request,
                CancellationToken cancellationToken)
            {
                LastListRequest = request;
                return Task.FromResult(
                    new PagedResult<ProductDto>(
                        new[] { new ProductDto(26, "SP026", "Sản phẩm", "Cái", true) },
                        2,
                        25,
                        30));
            }

            public Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult<ProductDto?>(null);

            public Task<IReadOnlyList<CatalogLookupDto>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken)
            {
                LastLookupRequest = request;
                return Task.FromResult<IReadOnlyList<CatalogLookupDto>>(
                    new[] { new CatalogLookupDto(1, "SP001", "Sản phẩm", true) });
            }
        }
    }
}
