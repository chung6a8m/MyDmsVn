using System.Threading;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Persistence;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Catalog;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopCompositionTests
    {
        [Fact]
        public void Local_desktop_services_use_one_shared_weak_reference_messenger()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var first = provider.GetRequiredService<IMessenger>();
                var second = provider.GetRequiredService<IMessenger>();

                Assert.Same(first, second);
                Assert.IsType<WeakReferenceMessenger>(first);
            }
        }

        [Fact]
        public async Task Local_desktop_services_resolve_api_without_database_configuration()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IFoundationApiClient>();

                var response = await client.GetStatusAsync(
                    new FoundationStatusRequest("Desktop"),
                    CancellationToken.None);

                Assert.True(response.IsSuccess);
                Assert.True(response.Data!.IsReady);
                Assert.Equal("Local", response.Data.Runtime);
            }
        }

        [Fact]
        public async Task Local_api_returns_validation_failure_as_api_response()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IFoundationApiClient>();

                var response = await client.GetStatusAsync(
                    new FoundationStatusRequest(string.Empty),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("ValidationError", response.Error!.Code);
                Assert.Equal("clientName", response.Error.Details[0].Field);
            }
        }

        [Fact]
        public async Task Local_api_honors_pre_canceled_request()
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var client = provider.GetRequiredService<IFoundationApiClient>();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => client.GetStatusAsync(
                        new FoundationStatusRequest("Desktop"),
                        cancellation.Token));
            }
        }

        [Fact]
        public async Task Local_api_maps_handler_exceptions_and_reports_diagnostics()
        {
            var services = new ServiceCollection();
            var reporter = new CapturingExceptionReporter();
            services.AddSingleton<IApplicationExceptionReporter>(reporter);
            services.AddServerApplication();
            services.AddTransient<
                IRequestHandler<GetFoundationStatusQuery, ErrorOr<FoundationStatus>>,
                ThrowingFoundationHandler>();
            services.AddServerInfrastructure();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IFoundationApiClient>();

                var response = await client.GetStatusAsync(
                    new FoundationStatusRequest("Desktop"),
                    CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("InternalError", response.Error!.Code);
                Assert.Empty(response.Error.Details);
                Assert.IsType<InvalidOperationException>(reporter.Exception);
                Assert.Equal(typeof(GetFoundationStatusQuery), reporter.RequestType);
            }
        }

        [Fact]
        public async Task Local_catalog_clients_are_registered_and_map_application_errors_to_api_responses()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IProductQueryService>(new EmptyProductQueryService());
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();

            using (var provider = services.BuildServiceProvider())
            {
                Assert.NotNull(provider.GetRequiredService<IProductApiClient>());
                Assert.NotNull(provider.GetRequiredService<IWarehouseApiClient>());
                Assert.NotNull(provider.GetRequiredService<IEmployeeApiClient>());
                Assert.NotNull(provider.GetRequiredService<ICustomerApiClient>());

                var response = await provider.GetRequiredService<IProductApiClient>()
                    .GetAsync(41, CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal("Auth.Unauthorized", response.Error!.Code);
                Assert.Equal(ApiStatusCode.Unauthorized, response.Error.Status);
            }
        }

        [Fact]
        public async Task Local_catalog_validation_fields_are_relative_to_public_request_dtos()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IProductQueryService>(new EmptyProductQueryService());
            services.AddSingleton<IUnitOfWorkFactory>(new RejectingUnitOfWorkFactory());
            services.AddSingleton<IPermissionStore>(new AllowPermissionStore());
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddSingleton<ICurrentUserAccessor>(new ActiveCurrentUserAccessor());

            using (var provider = services.BuildServiceProvider())
            {
                var client = provider.GetRequiredService<IProductApiClient>();

                var list = await client.ListAsync(
                    new CatalogListRequest(new string('x', 257), 1, 25, false),
                    CancellationToken.None);
                var lookup = await client.LookupAsync(
                    new CatalogLookupRequest("invalid\0search", 25),
                    CancellationToken.None);
                var create = await client.CreateAsync(
                    new SaveProductRequest(string.Empty, "Product", "Unit"),
                    CancellationToken.None);
                var update = await client.UpdateAsync(
                    1,
                    new SaveProductRequest(string.Empty, "Product", "Unit"),
                    CancellationToken.None);

                Assert.Equal("search", Assert.Single(list.Error!.Details).Field);
                Assert.Equal("search", Assert.Single(lookup.Error!.Details).Field);
                Assert.Equal("code", Assert.Single(create.Error!.Details).Field);
                Assert.Equal("code", Assert.Single(update.Error!.Details).Field);
            }
        }

        [Fact]
        public async Task In_flight_catalog_mutation_keeps_authorized_actor_after_local_sign_out()
        {
            var unitOfWorkFactory = new PausingUnitOfWorkFactory();
            var services = new ServiceCollection();
            services.AddSingleton<IUnitOfWorkFactory>(unitOfWorkFactory);
            services.AddSingleton<IPermissionStore>(new AllowPermissionStore());
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.AddTransient<
                IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>,
                SuccessfulLoginHandler>();

            using (var provider = services.BuildServiceProvider())
            {
                var identity = provider.GetRequiredService<IIdentityApiClient>();
                var session = provider.GetRequiredService<IDesktopSession>();
                var catalog = provider.GetRequiredService<IProductApiClient>();
                var login = await identity.LoginAsync(
                    new LoginRequest("operator", "secret"),
                    CancellationToken.None);
                Assert.True(login.IsSuccess);

                var mutation = catalog.SetActiveAsync(41, false, CancellationToken.None);
                var createStarted = unitOfWorkFactory.CreateStarted;
                var completed = await Task.WhenAny(
                    createStarted,
                    Task.Delay(TimeSpan.FromSeconds(5)));
                if (completed != createStarted)
                {
                    unitOfWorkFactory.Resume();
                }

                Assert.Same(createStarted, completed);
                await createStarted;
                session.SignOut();
                unitOfWorkFactory.Resume();
                var response = await mutation;

                Assert.True(response.IsSuccess);
                Assert.Equal(42, unitOfWorkFactory.Repository.UpdatedByUserId);
            }
        }

        private sealed class ThrowingFoundationHandler
            : IRequestHandler<GetFoundationStatusQuery, ErrorOr<FoundationStatus>>
        {
            public Task<ErrorOr<FoundationStatus>> Handle(
                GetFoundationStatusQuery request,
                CancellationToken cancellationToken)
            {
                throw new InvalidOperationException("secret infrastructure detail");
            }
        }

        private sealed class CapturingExceptionReporter : IApplicationExceptionReporter
        {
            public Exception? Exception { get; private set; }

            public Type? RequestType { get; private set; }

            public void Report(Type requestType, Exception exception)
            {
                RequestType = requestType;
                Exception = exception;
            }
        }

        private sealed class EmptyProductQueryService : IProductQueryService
        {
            public Task<PagedResult<ProductDto>> ListAsync(CatalogListRequest request, CancellationToken cancellationToken) =>
                Task.FromResult(new PagedResult<ProductDto>(Array.Empty<ProductDto>(), request.PageNumber, request.PageSize, 0));

            public Task<ProductDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult<ProductDto?>(null);

            public Task<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>> LookupAsync(
                CatalogLookupRequest request,
                CancellationToken cancellationToken) =>
                Task.FromResult<System.Collections.Generic.IReadOnlyList<CatalogLookupDto>>(Array.Empty<CatalogLookupDto>());
        }

        private sealed class ActiveCurrentUserAccessor : ICurrentUserAccessor
        {
            public CurrentUser Current { get; } =
                CurrentUser.Authenticated(42, "operator", "Operator", isActive: true);
        }

        private sealed class AllowPermissionStore : IPermissionStore
        {
            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken) =>
                Task.FromResult(new PermissionSnapshot(true, false));
        }

        private sealed class RejectingUnitOfWorkFactory : IUnitOfWorkFactory
        {
            public IUnitOfWork Create() =>
                throw new InvalidOperationException("Validation must run before unit-of-work creation.");

            public Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken) =>
                Task.FromException<IUnitOfWork>(
                    new InvalidOperationException("Validation must run before unit-of-work creation."));
        }

        private sealed class SuccessfulLoginHandler
            : IRequestHandler<LoginCommand, ErrorOr<CurrentUserDto>>
        {
            public Task<ErrorOr<CurrentUserDto>> Handle(
                LoginCommand request,
                CancellationToken cancellationToken) =>
                Task.FromResult<ErrorOr<CurrentUserDto>>(
                    new CurrentUserDto(42, request.Username, "Operator"));
        }

        private sealed class PausingUnitOfWorkFactory : IUnitOfWorkFactory
        {
            private readonly TaskCompletionSource<bool> _createStarted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _resume =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public PausingUnitOfWorkFactory()
            {
                Repository = new AuditCatalogWriteRepository();
            }

            public AuditCatalogWriteRepository Repository { get; }

            public Task CreateStarted => _createStarted.Task;

            public IUnitOfWork Create() => throw new NotSupportedException();

            public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken)
            {
                _createStarted.TrySetResult(true);
                await _resume.Task.ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return new AuditUnitOfWork(Repository);
            }

            public void Resume() => _resume.TrySetResult(true);
        }

        private sealed class AuditUnitOfWork : IUnitOfWork
        {
            private readonly ICatalogWriteRepository _repository;

            public AuditUnitOfWork(ICatalogWriteRepository repository) =>
                _repository = repository;

            public UnitOfWorkState State { get; private set; } = UnitOfWorkState.Created;

            public void BeginTransaction() => State = UnitOfWorkState.ActiveTransaction;

            public TRepository Repository<TRepository>() where TRepository : class =>
                (TRepository)_repository;

            public void Commit() => State = UnitOfWorkState.Committed;

            public void Rollback() => State = UnitOfWorkState.RolledBack;

            public void Dispose() => State = UnitOfWorkState.Disposed;
        }

        private sealed class AuditCatalogWriteRepository : ICatalogWriteRepository
        {
            public int? UpdatedByUserId { get; private set; }

            public Task<bool> SetProductActiveAsync(
                int id,
                bool isActive,
                DateTime updatedAtUtc,
                int? updatedByUserId,
                CancellationToken cancellationToken)
            {
                UpdatedByUserId = updatedByUserId;
                return Task.FromResult(true);
            }

            public Task<int> InsertProductAsync(Product product, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<Product?> UpdateProductAsync(Product product, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<int> InsertWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<Warehouse?> UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<bool> SetWarehouseActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<int> InsertEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<Employee?> UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<bool> SetEmployeeActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<int> InsertCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<Customer?> UpdateCustomerAsync(Customer customer, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
            public Task<bool> SetCustomerActiveAsync(int id, bool isActive, DateTime updatedAtUtc, int? updatedByUserId, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }
    }
}
