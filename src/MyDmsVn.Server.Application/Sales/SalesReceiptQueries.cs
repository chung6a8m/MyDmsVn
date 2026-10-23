using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Sales;

public sealed class ListSalesReceiptsQuery : ApplicationRequest<SalesReceiptListPage>, IAuthorizedRequest
{
    public ListSalesReceiptsQuery(SalesReceiptListRequest request) => Request = request ?? throw new System.ArgumentNullException(nameof(request));
    public SalesReceiptListRequest Request { get; }
    public string PermissionKey => PermissionKeys.SalesReceiptsRead;
}

internal sealed class ListSalesReceiptsQueryHandler : IRequestHandler<ListSalesReceiptsQuery, ErrorOr<SalesReceiptListPage>>
{
    private readonly ISalesReceiptQueryService _queries;
    public ListSalesReceiptsQueryHandler(ISalesReceiptQueryService queries) => _queries = queries;
    public async Task<ErrorOr<SalesReceiptListPage>> Handle(ListSalesReceiptsQuery query, CancellationToken token) => await _queries.ListAsync(SalesReceiptCursor.Parse(query.Request).Request, token).ConfigureAwait(false);
}

internal sealed class ListSalesReceiptsQueryValidator : AbstractValidator<ListSalesReceiptsQuery>
{
    public ListSalesReceiptsQueryValidator() => RuleFor(query => query.Request).Custom((request, context) =>
    {
        try { SalesReceiptCursor.Parse(request); }
        catch (ValidationException error) { foreach (var failure in error.Errors) context.AddFailure(failure); }
    });
}

public sealed class GetSalesReceiptByIdQuery : ApplicationRequest<SalesReceiptDto>, IAuthorizedRequest
{
    public GetSalesReceiptByIdQuery(long saleId) => SaleId = saleId;
    public long SaleId { get; }
    public string PermissionKey => PermissionKeys.SalesReceiptsRead;
}

internal sealed class GetSalesReceiptByIdQueryHandler : IRequestHandler<GetSalesReceiptByIdQuery, ErrorOr<SalesReceiptDto>>
{
    private readonly ISalesReceiptQueryService _queries;
    public GetSalesReceiptByIdQueryHandler(ISalesReceiptQueryService queries) => _queries = queries;
    public async Task<ErrorOr<SalesReceiptDto>> Handle(GetSalesReceiptByIdQuery query, CancellationToken cancellationToken)
    {
        var detail = await _queries.GetByIdAsync(query.SaleId, cancellationToken).ConfigureAwait(false);
        if (detail == null) return Error.NotFound("SalesReceipt.NotFound", "The sales receipt was not found.");
        return detail;
    }
}

internal sealed class GetSalesReceiptByIdQueryValidator : AbstractValidator<GetSalesReceiptByIdQuery>
{
    public GetSalesReceiptByIdQueryValidator() => RuleFor(query => query.SaleId).GreaterThan(0).WithErrorCode("Validation.GreaterThan");
}
