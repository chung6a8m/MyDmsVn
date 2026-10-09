using System.Threading;
using System;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Infrastructure;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class DesktopCompositionTests
    {
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
    }
}
