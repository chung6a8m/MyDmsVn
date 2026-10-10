using System;
using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;
using MyDmsVn.Server.Domain.Inventory;

namespace MyDmsVn.Server.Application.Inventory;

public sealed class GetGoodsReceiptByIdQuery : ApplicationRequest<GoodsReceiptDto>, IAuthorizedRequest
{
    public GetGoodsReceiptByIdQuery(long id) => Id = id;

    public long Id { get; }

    public string PermissionKey => PermissionKeys.InventoryGoodsReceiptsRead;
}

public sealed class ListGoodsReceiptsQuery
    : ApplicationRequest<PagedResult<GoodsReceiptDto>>, IAuthorizedRequest
{
    public ListGoodsReceiptsQuery(GoodsReceiptListRequest request) =>
        Request = request ?? throw new ArgumentNullException(nameof(request));

    public GoodsReceiptListRequest Request { get; }

    public string PermissionKey => PermissionKeys.InventoryGoodsReceiptsRead;
}

internal sealed class GetGoodsReceiptByIdQueryValidator
    : AbstractValidator<GetGoodsReceiptByIdQuery>
{
    public GetGoodsReceiptByIdQueryValidator() =>
        RuleFor(query => query.Id)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
}

internal sealed class ListGoodsReceiptsQueryValidator
    : AbstractValidator<ListGoodsReceiptsQuery>
{
    private const int MaximumSearchLength = 128;

    public ListGoodsReceiptsQueryValidator()
    {
        RuleFor(query => query.Request.PageNumber)
            .GreaterThan(0)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(1, 200)
            .WithErrorCode("Validation.Range");
        RuleFor(query => query.Request.WarehouseId)
            .GreaterThan(0)
            .When(query => query.Request.WarehouseId.HasValue)
            .WithErrorCode("Validation.GreaterThan");
        RuleFor(query => query.Request.Status)
            .Must(IsSupportedStatus)
            .WithErrorCode("Validation.InvalidValue");
        RuleFor(query => query.Request.Search)
            .MaximumLength(MaximumSearchLength)
            .WithErrorCode("Validation.MaximumLength")
            .Must(HasSupportedSearchCharacters)
            .WithErrorCode("Validation.InvalidCharacter");
    }

    private static bool IsSupportedStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ||
        string.Equals(status, GoodsReceiptStatuses.Draft, StringComparison.Ordinal) ||
        string.Equals(status, GoodsReceiptStatuses.Posted, StringComparison.Ordinal);

    private static bool HasSupportedSearchCharacters(string? search) =>
        search == null || search.IndexOf('\0') < 0;
}

internal sealed class ListGoodsReceiptsQueryHandler
    : IRequestHandler<ListGoodsReceiptsQuery, ErrorOr<PagedResult<GoodsReceiptDto>>>
{
    private readonly IGoodsReceiptQueryService _queryService;

    public ListGoodsReceiptsQueryHandler(IGoodsReceiptQueryService queryService) =>
        _queryService = queryService;

    public async Task<ErrorOr<PagedResult<GoodsReceiptDto>>> Handle(
        ListGoodsReceiptsQuery query,
        CancellationToken cancellationToken) =>
        await _queryService.ListAsync(query.Request, cancellationToken).ConfigureAwait(false);
}

internal sealed class GetGoodsReceiptByIdQueryHandler
    : IRequestHandler<GetGoodsReceiptByIdQuery, ErrorOr<GoodsReceiptDto>>
{
    private readonly IGoodsReceiptQueryService _queryService;

    public GetGoodsReceiptByIdQueryHandler(IGoodsReceiptQueryService queryService) =>
        _queryService = queryService;

    public async Task<ErrorOr<GoodsReceiptDto>> Handle(
        GetGoodsReceiptByIdQuery query,
        CancellationToken cancellationToken)
    {
        var receipt = await _queryService
            .GetByIdAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);
        if (receipt == null)
        {
            return Error.NotFound("GoodsReceipt.NotFound", "The goods receipt was not found.");
        }

        return receipt;
    }
}
