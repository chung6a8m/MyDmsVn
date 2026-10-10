using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Desktop.Infrastructure.Local;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Desktop.Tests
{
    public sealed class LocalGoodsReceiptApiClientTests
    {
        [Fact]
        public async Task Create_update_list_and_get_dispatch_the_matching_application_requests()
        {
            var handler = new RecordingHandler();
            using (var provider = CreateProvider(handler, allow: true))
            {
                var client = provider.GetRequiredService<IGoodsReceiptApiClient>();
                var save = ValidSave();

                var create = await client.CreateDraftAsync(save, CancellationToken.None);
                var update = await client.UpdateDraftAsync(
                    new UpdateGoodsReceiptDraftRequest(
                        41,
                        VersionToken(),
                        save.ReceiptDate,
                        save.WarehouseId,
                        save.EmployeeId,
                        save.Note,
                        save.Lines),
                    CancellationToken.None);
                var listRequest = new GoodsReceiptListRequest(1, 25, null, null, "GR");
                var list = await client.ListAsync(listRequest, CancellationToken.None);
                var get = await client.GetAsync(41, CancellationToken.None);

                Assert.True(create.IsSuccess);
                Assert.True(update.IsSuccess);
                Assert.True(list.IsSuccess);
                Assert.True(get.IsSuccess);
                Assert.Equal(
                    new[]
                    {
                        typeof(CreateGoodsReceiptDraftCommand),
                        typeof(UpdateGoodsReceiptDraftCommand),
                        typeof(ListGoodsReceiptsQuery),
                        typeof(GetGoodsReceiptByIdQuery),
                    },
                    handler.RequestTypes);
                Assert.Same(listRequest, handler.ListRequest);
            }
        }

        [Fact]
        public async Task Application_not_found_and_permission_denial_keep_typed_api_mapping()
        {
            var missingHandler = new RecordingHandler { ReturnMissingGet = true };
            using (var missingProvider = CreateProvider(missingHandler, allow: true))
            {
                var missing = await missingProvider.GetRequiredService<IGoodsReceiptApiClient>()
                    .GetAsync(404, CancellationToken.None);

                Assert.False(missing.IsSuccess);
                Assert.Equal(ApiStatusCode.NotFound, missing.Error!.Status);
                Assert.Equal("GoodsReceipt.NotFound", missing.Error.Code);
            }

            var deniedHandler = new RecordingHandler();
            using (var deniedProvider = CreateProvider(deniedHandler, allow: false))
            {
                var denied = await deniedProvider.GetRequiredService<IGoodsReceiptApiClient>()
                    .ListAsync(
                        new GoodsReceiptListRequest(1, 25, null, null),
                        CancellationToken.None);

                Assert.False(denied.IsSuccess);
                Assert.Equal(ApiStatusCode.Forbidden, denied.Error!.Status);
                Assert.Equal("Auth.Forbidden", denied.Error.Code);
                Assert.Empty(deniedHandler.RequestTypes);
            }
        }

        [Fact]
        public async Task Post_is_explicitly_unavailable_without_dispatch_before_p5_3()
        {
            var handler = new RecordingHandler();
            using (var provider = CreateProvider(handler, allow: true))
            {
                var response = await provider.GetRequiredService<IGoodsReceiptApiClient>()
                    .PostAsync(
                        new PostGoodsReceiptRequest(41, VersionToken()),
                        CancellationToken.None);

                Assert.False(response.IsSuccess);
                Assert.Equal(ApiStatusCode.InternalServerError, response.Error!.Status);
                Assert.Equal("GoodsReceipt.PostNotAvailable", response.Error.Code);
                Assert.Empty(handler.RequestTypes);
            }
        }

        private static ServiceProvider CreateProvider(RecordingHandler handler, bool allow)
        {
            var services = new ServiceCollection();
            services.AddServerApplication();
            services.AddLocalDesktopAdapter();
            services.Replace(ServiceDescriptor.Singleton<ICurrentUserAccessor>(
                new CurrentUserAccessor()));
            services.Replace(ServiceDescriptor.Singleton<IPermissionStore>(
                new PermissionStore(allow)));
            services.AddSingleton<
                IRequestHandler<CreateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>>(handler);
            services.AddSingleton<
                IRequestHandler<UpdateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>>(handler);
            services.AddSingleton<
                IRequestHandler<ListGoodsReceiptsQuery, ErrorOr<PagedResult<GoodsReceiptDto>>>>(handler);
            services.AddSingleton<
                IRequestHandler<GetGoodsReceiptByIdQuery, ErrorOr<GoodsReceiptDto>>>(handler);
            return services.BuildServiceProvider();
        }

        private static SaveGoodsReceiptRequest ValidSave() =>
            new SaveGoodsReceiptRequest(
                new DateTime(2026, 10, 10),
                3,
                5,
                "note",
                new[] { new SaveGoodsReceiptLineRequest(7, 2m, 4m) });

        private static string VersionToken() =>
            Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        private static GoodsReceiptDto Receipt() =>
            new GoodsReceiptDto(
                41,
                "GR0000000041",
                new DateTime(2026, 10, 10),
                3,
                5,
                "Draft",
                "note",
                null,
                VersionToken(),
                new[] { new GoodsReceiptLineDto(91, 1, 7, 2m, 4m) });

        private sealed class RecordingHandler :
            IRequestHandler<CreateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>,
            IRequestHandler<UpdateGoodsReceiptDraftCommand, ErrorOr<GoodsReceiptDto>>,
            IRequestHandler<ListGoodsReceiptsQuery, ErrorOr<PagedResult<GoodsReceiptDto>>>,
            IRequestHandler<GetGoodsReceiptByIdQuery, ErrorOr<GoodsReceiptDto>>
        {
            public List<Type> RequestTypes { get; } = new List<Type>();
            public GoodsReceiptListRequest? ListRequest { get; private set; }
            public bool ReturnMissingGet { get; set; }

            public Task<ErrorOr<GoodsReceiptDto>> Handle(
                CreateGoodsReceiptDraftCommand request,
                CancellationToken cancellationToken)
            {
                RequestTypes.Add(request.GetType());
                return Task.FromResult<ErrorOr<GoodsReceiptDto>>(Receipt());
            }

            public Task<ErrorOr<GoodsReceiptDto>> Handle(
                UpdateGoodsReceiptDraftCommand request,
                CancellationToken cancellationToken)
            {
                RequestTypes.Add(request.GetType());
                return Task.FromResult<ErrorOr<GoodsReceiptDto>>(Receipt());
            }

            public Task<ErrorOr<PagedResult<GoodsReceiptDto>>> Handle(
                ListGoodsReceiptsQuery request,
                CancellationToken cancellationToken)
            {
                RequestTypes.Add(request.GetType());
                ListRequest = request.Request;
                return Task.FromResult<ErrorOr<PagedResult<GoodsReceiptDto>>>(
                    new PagedResult<GoodsReceiptDto>(new[] { Receipt() }, 1, 25, 1));
            }

            public Task<ErrorOr<GoodsReceiptDto>> Handle(
                GetGoodsReceiptByIdQuery request,
                CancellationToken cancellationToken)
            {
                RequestTypes.Add(request.GetType());
                return Task.FromResult<ErrorOr<GoodsReceiptDto>>(
                    ReturnMissingGet
                        ? Error.NotFound("GoodsReceipt.NotFound", "The goods receipt was not found.")
                        : Receipt());
            }
        }

        private sealed class CurrentUserAccessor : ICurrentUserAccessor
        {
            public CurrentUser Current { get; } =
                CurrentUser.Authenticated(42, "operator", "Operator", isActive: true);
        }

        private sealed class PermissionStore : IPermissionStore
        {
            private readonly bool _allow;

            public PermissionStore(bool allow) => _allow = allow;

            public Task<PermissionSnapshot> GetSnapshotAsync(
                int userId,
                string permissionKey,
                CancellationToken cancellationToken) =>
                Task.FromResult(new PermissionSnapshot(_allow, hasRoleGrant: _allow));
        }
    }
}
