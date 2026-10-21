using System.Threading;
using System.Threading.Tasks;
using ErrorOr;
using FluentValidation;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application.Security;

namespace MyDmsVn.Server.Application.Sales;

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
