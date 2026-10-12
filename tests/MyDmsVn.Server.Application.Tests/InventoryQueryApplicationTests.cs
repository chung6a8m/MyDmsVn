using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Security;
using Xunit;

namespace MyDmsVn.Server.Application.Tests;

public sealed class InventoryQueryApplicationTests
{
    [Fact]
    public void Inventory_queries_use_distinct_read_permissions()
    {
        Assert.Equal(
            PermissionKeys.InventoryBalancesRead,
            new GetStockBalancesQuery(new StockBalanceQuery(null, null, null, 1, 25)).PermissionKey);
        Assert.Equal(
            PermissionKeys.InventoryStockCardRead,
            new GetStockCardQuery(new StockCardQuery(2, 7, null, null, 1, 25)).PermissionKey);
    }

    [Fact]
    public async Task Validators_reject_invalid_filters_paging_search_and_date_ranges()
    {
        var services = new ServiceCollection();
        services.AddServerApplication();
        using var provider = services.BuildServiceProvider();
        var balances = provider.GetRequiredService<IValidator<GetStockBalancesQuery>>();
        var balanceResult = await balances.ValidateAsync(
            new GetStockBalancesQuery(new StockBalanceQuery(0, -1, "bad\0search", 0, 201)));

        Assert.Contains(balanceResult.Errors, failure => failure.PropertyName == "Request.WarehouseId");
        Assert.Contains(balanceResult.Errors, failure => failure.PropertyName == "Request.ProductId");
        Assert.Contains(balanceResult.Errors, failure => failure.PropertyName == "Request.Search");
        Assert.Contains(balanceResult.Errors, failure => failure.PropertyName == "Request.PageNumber");
        Assert.Contains(balanceResult.Errors, failure => failure.PropertyName == "Request.PageSize");

        var card = provider.GetRequiredService<IValidator<GetStockCardQuery>>();
        var cardResult = await card.ValidateAsync(
            new GetStockCardQuery(
                new StockCardQuery(
                    0,
                    -1,
                    new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                    0,
                    201)));

        Assert.Contains(cardResult.Errors, failure => failure.PropertyName == "Request.WarehouseId");
        Assert.Contains(cardResult.Errors, failure => failure.PropertyName == "Request.ProductId");
        Assert.Contains(cardResult.Errors, failure => failure.PropertyName == "Request.ToUtc");
        Assert.Contains(cardResult.Errors, failure => failure.PropertyName == "Request.PageNumber");
        Assert.Contains(cardResult.Errors, failure => failure.PropertyName == "Request.PageSize");
    }

    [Fact]
    public async Task Handlers_forward_valid_requests_to_the_query_service()
    {
        var service = new RecordingInventoryQueryService();
        var services = new ServiceCollection();
        services.AddSingleton<IInventoryQueryService>(service);
        services.AddServerApplication();
        using var provider = services.BuildServiceProvider();
        var balanceRequest = new StockBalanceQuery(2, 7, "P-007", 2, 25);
        var cardRequest = new StockCardQuery(2, 7, null, null, 3, 50);

        var balances = await provider
            .GetRequiredService<IRequestHandler<
                GetStockBalancesQuery,
                ErrorOr<PagedResult<StockBalanceRow>>>>()
            .Handle(new GetStockBalancesQuery(balanceRequest), CancellationToken.None);
        var card = await provider
            .GetRequiredService<IRequestHandler<
                GetStockCardQuery,
                ErrorOr<PagedResult<StockCardRow>>>>()
            .Handle(new GetStockCardQuery(cardRequest), CancellationToken.None);

        Assert.Same(balanceRequest, service.BalanceRequest);
        Assert.Same(cardRequest, service.CardRequest);
        Assert.Empty(balances.Value.Items);
        Assert.Empty(card.Value.Items);
    }

    private sealed class RecordingInventoryQueryService : IInventoryQueryService
    {
        public StockBalanceQuery? BalanceRequest { get; private set; }
        public StockCardQuery? CardRequest { get; private set; }

        public Task<PagedResult<StockBalanceRow>> GetStockBalancesAsync(
            StockBalanceQuery request,
            CancellationToken cancellationToken)
        {
            BalanceRequest = request;
            return Task.FromResult(
                new PagedResult<StockBalanceRow>(Array.Empty<StockBalanceRow>(), request.PageNumber, request.PageSize, 0));
        }

        public Task<PagedResult<StockCardRow>> GetStockCardAsync(
            StockCardQuery request,
            CancellationToken cancellationToken)
        {
            CardRequest = request;
            return Task.FromResult(
                new PagedResult<StockCardRow>(Array.Empty<StockCardRow>(), request.PageNumber, request.PageSize, 0));
        }
    }
}
