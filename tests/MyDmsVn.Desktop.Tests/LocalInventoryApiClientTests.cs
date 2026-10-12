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

namespace MyDmsVn.Desktop.Tests;

public sealed class LocalInventoryApiClientTests
{
    [Fact]
    public async Task Balance_and_stock_card_calls_dispatch_matching_queries_and_preserve_requests()
    {
        var handler = new RecordingHandler();
        using var provider = CreateProvider(handler, allow: true);
        var client = provider.GetRequiredService<IInventoryApiClient>();
        var balancesRequest = new StockBalanceQuery(2, 7, "P-007", 2, 25);
        var cardRequest = new StockCardQuery(2, 7, null, null, 3, 50);

        var balances = await client.GetStockBalancesAsync(balancesRequest, CancellationToken.None);
        var card = await client.GetStockCardAsync(cardRequest, CancellationToken.None);

        Assert.True(balances.IsSuccess);
        Assert.True(card.IsSuccess);
        Assert.Same(balancesRequest, handler.BalanceRequest);
        Assert.Same(cardRequest, handler.CardRequest);
        Assert.Equal(
            new[] { typeof(GetStockBalancesQuery), typeof(GetStockCardQuery) },
            handler.RequestTypes);
    }

    [Fact]
    public async Task Inventory_permissions_and_validation_map_to_transport_neutral_errors()
    {
        var deniedHandler = new RecordingHandler();
        using (var deniedProvider = CreateProvider(deniedHandler, allow: false))
        {
            var denied = await deniedProvider.GetRequiredService<IInventoryApiClient>()
                .GetStockBalancesAsync(
                    new StockBalanceQuery(null, null, null, 1, 25),
                    CancellationToken.None);

            Assert.False(denied.IsSuccess);
            Assert.Equal(ApiStatusCode.Forbidden, denied.Error!.Status);
            Assert.Equal("Auth.Forbidden", denied.Error.Code);
            Assert.Empty(deniedHandler.RequestTypes);
        }

        var validationHandler = new RecordingHandler();
        using (var validationProvider = CreateProvider(validationHandler, allow: true))
        {
            var invalid = await validationProvider.GetRequiredService<IInventoryApiClient>()
                .GetStockCardAsync(
                    new StockCardQuery(0, 0, null, null, 1, 25),
                    CancellationToken.None);

            Assert.False(invalid.IsSuccess);
            Assert.Equal(ApiStatusCode.BadRequest, invalid.Error!.Status);
            Assert.Contains(invalid.Error.Details, detail => detail.Field == "warehouseId");
            Assert.Contains(invalid.Error.Details, detail => detail.Field == "productId");
            Assert.Empty(validationHandler.RequestTypes);
        }
    }

    private static ServiceProvider CreateProvider(RecordingHandler handler, bool allow)
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        services.AddLocalDesktopAdapter();
        services.Replace(ServiceDescriptor.Singleton<ICurrentUserAccessor>(new CurrentUserAccessor()));
        services.Replace(ServiceDescriptor.Singleton<IPermissionStore>(new PermissionStore(allow)));
        services.AddSingleton<
            IRequestHandler<GetStockBalancesQuery, ErrorOr<PagedResult<StockBalanceRow>>>>(handler);
        services.AddSingleton<
            IRequestHandler<GetStockCardQuery, ErrorOr<PagedResult<StockCardRow>>>>(handler);
        return services.BuildServiceProvider();
    }

    private sealed class RecordingHandler :
        IRequestHandler<GetStockBalancesQuery, ErrorOr<PagedResult<StockBalanceRow>>>,
        IRequestHandler<GetStockCardQuery, ErrorOr<PagedResult<StockCardRow>>>
    {
        public List<Type> RequestTypes { get; } = new List<Type>();
        public StockBalanceQuery? BalanceRequest { get; private set; }
        public StockCardQuery? CardRequest { get; private set; }

        public Task<ErrorOr<PagedResult<StockBalanceRow>>> Handle(
            GetStockBalancesQuery request,
            CancellationToken cancellationToken)
        {
            RequestTypes.Add(request.GetType());
            BalanceRequest = request.Request;
            return Task.FromResult<ErrorOr<PagedResult<StockBalanceRow>>>(
                new PagedResult<StockBalanceRow>(Array.Empty<StockBalanceRow>(), 1, 25, 0));
        }

        public Task<ErrorOr<PagedResult<StockCardRow>>> Handle(
            GetStockCardQuery request,
            CancellationToken cancellationToken)
        {
            RequestTypes.Add(request.GetType());
            CardRequest = request.Request;
            return Task.FromResult<ErrorOr<PagedResult<StockCardRow>>>(
                new PagedResult<StockCardRow>(Array.Empty<StockCardRow>(), 1, 25, 0));
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
