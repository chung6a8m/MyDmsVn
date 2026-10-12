using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Inventory;

public sealed class GetStockBalancesQuery
    : ApplicationRequest<PagedResult<StockBalanceRow>>, IAuthorizedRequest
{
    public GetStockBalancesQuery(StockBalanceQuery request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public StockBalanceQuery Request { get; }

    public string PermissionKey => PermissionKeys.InventoryBalancesRead;
}

public sealed class GetStockCardQuery
    : ApplicationRequest<PagedResult<StockCardRow>>, IAuthorizedRequest
{
    public GetStockCardQuery(StockCardQuery request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public StockCardQuery Request { get; }

    public string PermissionKey => PermissionKeys.InventoryStockCardRead;
}

internal sealed class GetStockBalancesQueryValidator
    : AbstractValidator<GetStockBalancesQuery>
{
    private const int MaximumSearchLength = 256;

    public GetStockBalancesQueryValidator()
    {
        RuleFor(query => query.Request.WarehouseId)
            .GreaterThan(0)
            .When(query => query.Request.WarehouseId.HasValue)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.ProductId)
            .GreaterThan(0)
            .When(query => query.Request.ProductId.HasValue)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.Search)
            .MaximumLength(MaximumSearchLength)
            .WithErrorCode("Validation.MaximumLength")
            .Must(search => search == null || search.IndexOf('\0') < 0)
            .WithErrorCode("Validation.InvalidCharacter");
        RuleFor(query => query.Request.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(1, 200)
            .WithErrorCode("Validation.Range");
    }
}

internal sealed class GetStockCardQueryValidator
    : AbstractValidator<GetStockCardQuery>
{
    public GetStockCardQueryValidator()
    {
        RuleFor(query => query.Request.WarehouseId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.ProductId)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.FromUtc)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithErrorCode("Validation.UtcRequired");
        RuleFor(query => query.Request.ToUtc)
            .Must(value => !value.HasValue || value.Value.Kind == DateTimeKind.Utc)
            .WithErrorCode("Validation.UtcRequired")
            .Must((query, toUtc) =>
                !query.Request.FromUtc.HasValue ||
                !toUtc.HasValue ||
                toUtc.Value >= query.Request.FromUtc.Value)
            .WithErrorCode("Validation.InvalidRange");
        RuleFor(query => query.Request.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(1, 200)
            .WithErrorCode("Validation.Range");
    }
}

internal sealed class GetStockBalancesQueryHandler
    : IRequestHandler<GetStockBalancesQuery, ErrorOr<PagedResult<StockBalanceRow>>>
{
    private readonly IInventoryQueryService _queryService;

    public GetStockBalancesQueryHandler(IInventoryQueryService queryService) =>
        _queryService = queryService;

    public async Task<ErrorOr<PagedResult<StockBalanceRow>>> Handle(
        GetStockBalancesQuery query,
        CancellationToken cancellationToken) =>
        await _queryService
            .GetStockBalancesAsync(query.Request, cancellationToken)
            .ConfigureAwait(false);
}

internal sealed class GetStockCardQueryHandler
    : IRequestHandler<GetStockCardQuery, ErrorOr<PagedResult<StockCardRow>>>
{
    private readonly IInventoryQueryService _queryService;

    public GetStockCardQueryHandler(IInventoryQueryService queryService) =>
        _queryService = queryService;

    public async Task<ErrorOr<PagedResult<StockCardRow>>> Handle(
        GetStockCardQuery query,
        CancellationToken cancellationToken) =>
        await _queryService
            .GetStockCardAsync(query.Request, cancellationToken)
            .ConfigureAwait(false);
}
