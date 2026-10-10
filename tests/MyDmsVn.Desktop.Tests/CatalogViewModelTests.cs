using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class CatalogViewModelTests
    {
        [Fact]
        public async Task Product_create_update_and_set_active_publish_one_typed_message_after_each_success()
        {
            var client = new FakeProductClient
            {
                CreateResponse = ApiResponse<ProductDto>.Success(Product(10, "P10")),
                GetResponse = ApiResponse<ProductDto>.Success(Product(10, "P10")),
                UpdateResponse = ApiResponse<ProductDto>.Success(Product(10, "P10U")),
                SetActiveResponse = ApiResponse<UnitResponse>.Success(UnitResponse.Value),
            };
            var messenger = new WeakReferenceMessenger();
            var messages = new List<CatalogChangedMessage>();
            var recipient = new object();
            messenger.Register<CatalogChangedMessage>(recipient, (_, message) => messages.Add(message));
            using (var viewModel = CreateProduct(client, new ControllableDelay(), messenger))
            {
                viewModel.BeginCreate();
                viewModel.Code = "P10";
                viewModel.Name = "Product 10";
                viewModel.Unit = "pcs";
                await viewModel.SaveAsync(CancellationToken.None);

                await viewModel.SelectAsync(10, CancellationToken.None);
                viewModel.Code = "P10U";
                await viewModel.SaveAsync(CancellationToken.None);
                await viewModel.SetActiveAsync(10, false, CancellationToken.None);

                Assert.Equal(3, messages.Count);
                Assert.All(messages, message => Assert.Equal(CatalogKind.Product, message.CatalogKind));
                Assert.Equal(
                    new[]
                    {
                        CatalogChangeOperation.Created,
                        CatalogChangeOperation.Updated,
                        CatalogChangeOperation.ActiveStatusChanged,
                    },
                    messages.Select(message => message.Operation));
                Assert.All(messages, message => Assert.Equal(10, message.EntityId));
                Assert.Equal(3, client.ListRequests.Count);
            }
        }

        [Fact]
        public async Task Failed_validation_conflict_exception_and_cancellation_publish_no_messages()
        {
            var validation = new ApiError(
                ApiStatusCode.BadRequest,
                "ValidationError",
                "Correct the fields.",
                new[] { new ApiErrorDetail("Validation.Required", "Code is required.", "code") });
            var client = new FakeProductClient
            {
                CreateResponse = ApiResponse<ProductDto>.Failure(validation),
                SetActiveResponse = ApiResponse<UnitResponse>.Failure(
                    new ApiError(ApiStatusCode.Conflict, "Product.Conflict", "Conflict.")),
            };
            var messenger = new WeakReferenceMessenger();
            var messages = new List<CatalogChangedMessage>();
            var recipient = new object();
            messenger.Register<CatalogChangedMessage>(recipient, (_, message) => messages.Add(message));
            using (var viewModel = CreateProduct(client, new ControllableDelay(), messenger))
            {
                viewModel.BeginCreate();
                await viewModel.SaveAsync(CancellationToken.None);
                Assert.Equal("Code is required.", Assert.Single(viewModel.GetErrors("code")));

                await viewModel.SetActiveAsync(1, false, CancellationToken.None);
                client.CreateException = new InvalidOperationException("failure");
                await viewModel.SaveAsync(CancellationToken.None);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    await viewModel.SaveAsync(cancellation.Token);
                }

                Assert.Empty(messages);
            }
        }

        [Fact]
        public async Task Every_catalog_create_publishes_its_own_kind_and_entity_id()
        {
            var messenger = new WeakReferenceMessenger();
            var messages = new List<CatalogChangedMessage>();
            var recipient = new object();
            messenger.Register<CatalogChangedMessage>(recipient, (_, message) => messages.Add(message));
            var dispatcher = new ImmediateUiDispatcher();
            var notifications = new DesktopNotificationCenter();
            var delay = new SystemAsyncDelay();

            using (var warehouse = new WarehouseCatalogViewModel(
                new SuccessfulWarehouseClient(), messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            using (var employee = new EmployeeCatalogViewModel(
                new SuccessfulEmployeeClient(), messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            using (var customer = new CustomerCatalogViewModel(
                new SuccessfulCustomerClient(), messenger, dispatcher, notifications, delay, TimeSpan.Zero))
            {
                warehouse.BeginCreate();
                warehouse.Code = "W11";
                warehouse.Name = "Warehouse";
                employee.BeginCreate();
                employee.Code = "E12";
                employee.Name = "Employee";
                customer.BeginCreate();
                customer.Code = "C13";
                customer.Name = "Customer";

                await warehouse.SaveAsync(CancellationToken.None);
                await employee.SaveAsync(CancellationToken.None);
                await customer.SaveAsync(CancellationToken.None);
            }

            Assert.Equal(
                new[]
                {
                    (CatalogKind.Warehouse, 11),
                    (CatalogKind.Employee, 12),
                    (CatalogKind.Customer, 13),
                },
                messages.Select(message => (message.CatalogKind, message.EntityId)));
            Assert.All(messages, message => Assert.Equal(CatalogChangeOperation.Created, message.Operation));
        }

        [Fact]
        public async Task Rapid_search_uses_300ms_debounce_issues_one_query_and_resets_page()
        {
            var client = new FakeProductClient();
            var delay = new ControllableDelay();
            using (var viewModel = CreateProduct(client, delay))
            {
                await viewModel.MoveToPageAsync(3, CancellationToken.None);
                client.ListRequests.Clear();

                var first = viewModel.SetSearch("s");
                var second = viewModel.SetSearch("sp");
                Assert.Empty(client.ListRequests);

                delay.ReleaseLatest();
                await Task.WhenAll(first, second);

                var request = Assert.Single(client.ListRequests);
                Assert.Equal("sp", request.Search);
                Assert.Equal(1, request.PageNumber);
                Assert.Equal(TimeSpan.FromMilliseconds(300), delay.LatestInterval);
            }
        }

        [Fact]
        public async Task Refresh_is_immediate_and_preserves_the_current_page()
        {
            var client = new FakeProductClient();
            using (var viewModel = CreateProduct(client, new ControllableDelay()))
            {
                await viewModel.MoveToPageAsync(2, CancellationToken.None);
                client.ListRequests.Clear();

                await viewModel.RefreshAsync(CancellationToken.None);

                var request = Assert.Single(client.ListRequests);
                Assert.Equal(2, request.PageNumber);
            }
        }

        [Fact]
        public async Task Late_old_list_response_cannot_overwrite_newer_items()
        {
            var client = new FakeProductClient { ControlListResponses = true };
            using (var viewModel = CreateProduct(client, new ControllableDelay()))
            {
                var oldLoad = viewModel.RefreshAsync(CancellationToken.None);
                var newLoad = viewModel.RefreshAsync(CancellationToken.None);
                client.CompleteList(1, Product(2, "NEW"));
                await newLoad;
                client.CompleteList(0, Product(1, "OLD"));
                await oldLoad;

                Assert.Equal(2, Assert.Single(viewModel.Items).Id);
            }
        }

        [Fact]
        public async Task List_failure_sets_error_and_disposal_blocks_late_updates()
        {
            var failureClient = new FakeProductClient
            {
                ListResponse = ApiResponse<PagedResult<ProductDto>>.Failure(
                    new ApiError(ApiStatusCode.Forbidden, "Auth.Forbidden", "Access denied.")),
            };
            using (var failed = CreateProduct(failureClient, new ControllableDelay()))
            {
                await failed.RefreshAsync(CancellationToken.None);
                Assert.Equal("Access denied.", failed.ErrorMessage);
                Assert.Empty(failed.Items);
            }

            var lateClient = new FakeProductClient { ControlListResponses = true };
            var disposed = CreateProduct(lateClient, new ControllableDelay());
            var pending = disposed.RefreshAsync(CancellationToken.None);
            disposed.Dispose();
            lateClient.CompleteList(0, Product(3, "LATE"));
            await pending;
            Assert.Empty(disposed.Items);
        }

        private static ProductCatalogViewModel CreateProduct(
            IProductApiClient client,
            IAsyncDelay delay,
            IMessenger? messenger = null)
        {
            return new ProductCatalogViewModel(
                client,
                messenger ?? new WeakReferenceMessenger(),
                new ImmediateUiDispatcher(),
                new DesktopNotificationCenter(),
                delay);
        }

        private static ProductDto Product(int id, string code)
        {
            return new ProductDto(id, code, code + " name", "pcs", true);
        }

        private sealed class ControllableDelay : IAsyncDelay
        {
            private readonly List<TaskCompletionSource<bool>> _pending =
                new List<TaskCompletionSource<bool>>();

            public TimeSpan LatestInterval { get; private set; }

            public Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
            {
                LatestInterval = interval;
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                cancellationToken.Register(() => completion.TrySetCanceled());
                _pending.Add(completion);
                return completion.Task;
            }

            public void ReleaseLatest() => _pending[_pending.Count - 1].TrySetResult(true);
        }

        private sealed class FakeProductClient : IProductApiClient
        {
            private readonly List<TaskCompletionSource<ApiResponse<PagedResult<ProductDto>>>>
                _controlledLists = new List<TaskCompletionSource<ApiResponse<PagedResult<ProductDto>>>>();

            public List<CatalogListRequest> ListRequests { get; } = new List<CatalogListRequest>();

            public bool ControlListResponses { get; set; }

            public ApiResponse<PagedResult<ProductDto>> ListResponse { get; set; } =
                ApiResponse<PagedResult<ProductDto>>.Success(
                    new PagedResult<ProductDto>(Array.Empty<ProductDto>(), 1, 25, 0));

            public ApiResponse<ProductDto> GetResponse { get; set; } =
                ApiResponse<ProductDto>.Success(Product(1, "P1"));

            public ApiResponse<ProductDto> CreateResponse { get; set; } =
                ApiResponse<ProductDto>.Success(Product(1, "P1"));

            public ApiResponse<ProductDto> UpdateResponse { get; set; } =
                ApiResponse<ProductDto>.Success(Product(1, "P1"));

            public ApiResponse<UnitResponse> SetActiveResponse { get; set; } =
                ApiResponse<UnitResponse>.Success(UnitResponse.Value);

            public Exception? CreateException { get; set; }

            public Task<ApiResponse<PagedResult<ProductDto>>> ListAsync(
                CatalogListRequest request,
                CancellationToken cancellationToken)
            {
                ListRequests.Add(request);
                if (!ControlListResponses)
                {
                    return Task.FromResult(ListResponse);
                }

                var response = new TaskCompletionSource<ApiResponse<PagedResult<ProductDto>>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _controlledLists.Add(response);
                return response.Task;
            }

            public void CompleteList(int index, params ProductDto[] items)
            {
                _controlledLists[index].TrySetResult(
                    ApiResponse<PagedResult<ProductDto>>.Success(
                        new PagedResult<ProductDto>(items, 1, 25, items.Length)));
            }

            public Task<ApiResponse<ProductDto>> GetAsync(int id, CancellationToken cancellationToken) =>
                Task.FromResult(GetResponse);
            public Task<ApiResponse<ProductDto>> CreateAsync(SaveProductRequest request, CancellationToken cancellationToken) =>
                CreateException == null
                    ? Task.FromResult(CreateResponse)
                    : Task.FromException<ApiResponse<ProductDto>>(CreateException);
            public Task<ApiResponse<ProductDto>> UpdateAsync(int id, SaveProductRequest request, CancellationToken cancellationToken) =>
                Task.FromResult(UpdateResponse);
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken) =>
                Task.FromResult(SetActiveResponse);
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException();
        }

        private sealed class SuccessfulWarehouseClient : IWarehouseApiClient
        {
            public Task<ApiResponse<PagedResult<WarehouseDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => Task.FromResult(ApiResponse<PagedResult<WarehouseDto>>.Success(new PagedResult<WarehouseDto>(Array.Empty<WarehouseDto>(), request.PageNumber, request.PageSize, 0)));
            public Task<ApiResponse<WarehouseDto>> GetAsync(int id, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<WarehouseDto>> CreateAsync(SaveWarehouseRequest request, CancellationToken token) => Task.FromResult(ApiResponse<WarehouseDto>.Success(new WarehouseDto(11, request.Code, request.Name, request.Address, true)));
            public Task<ApiResponse<WarehouseDto>> UpdateAsync(int id, SaveWarehouseRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class SuccessfulEmployeeClient : IEmployeeApiClient
        {
            public Task<ApiResponse<PagedResult<EmployeeDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => Task.FromResult(ApiResponse<PagedResult<EmployeeDto>>.Success(new PagedResult<EmployeeDto>(Array.Empty<EmployeeDto>(), request.PageNumber, request.PageSize, 0)));
            public Task<ApiResponse<EmployeeDto>> GetAsync(int id, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<EmployeeDto>> CreateAsync(SaveEmployeeRequest request, CancellationToken token) => Task.FromResult(ApiResponse<EmployeeDto>.Success(new EmployeeDto(12, request.Code, request.Name, request.Phone, request.UserId, true)));
            public Task<ApiResponse<EmployeeDto>> UpdateAsync(int id, SaveEmployeeRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }

        private sealed class SuccessfulCustomerClient : ICustomerApiClient
        {
            public Task<ApiResponse<PagedResult<CustomerDto>>> ListAsync(CatalogListRequest request, CancellationToken token) => Task.FromResult(ApiResponse<PagedResult<CustomerDto>>.Success(new PagedResult<CustomerDto>(Array.Empty<CustomerDto>(), request.PageNumber, request.PageSize, 0)));
            public Task<ApiResponse<CustomerDto>> GetAsync(int id, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<CustomerDto>> CreateAsync(SaveCustomerRequest request, CancellationToken token) => Task.FromResult(ApiResponse<CustomerDto>.Success(new CustomerDto(13, request.Code, request.Name, request.Address, request.Phone, request.TaxCode, true)));
            public Task<ApiResponse<CustomerDto>> UpdateAsync(int id, SaveCustomerRequest request, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<UnitResponse>> SetActiveAsync(int id, bool active, CancellationToken token) => throw new NotSupportedException();
            public Task<ApiResponse<IReadOnlyList<CatalogLookupDto>>> LookupAsync(CatalogLookupRequest request, CancellationToken token) => throw new NotSupportedException();
        }
    }
}
