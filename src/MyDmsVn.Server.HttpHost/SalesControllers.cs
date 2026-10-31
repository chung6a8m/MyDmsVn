using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using ErrorOr;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Catalog;
using MyDmsVn.Server.Application.Identity;
using MyDmsVn.Server.Application.Sales;

namespace MyDmsVn.Server.HttpHost;

public abstract class WireApiController : ApiController
{
    protected WireApiController(IMediator mediator, IRequestCorrelationAccessor correlation) { Mediator = mediator; Correlation = correlation; }
    protected IMediator Mediator { get; }
    private IRequestCorrelationAccessor Correlation { get; }
    protected async Task<HttpResponseMessage> Execute<T>(Func<Task<ErrorOr<T>>> operation, Func<T, object> map)
    {
        try
        {
            var result = await operation().ConfigureAwait(false);
            return result.IsError ? HttpJson.From(ApiResponseMapper.Map(result, "Request", Correlation.CorrelationId)) : HttpJson.Success(map(result.Value));
        }
        catch (WireInputException error)
        {
            return HttpJson.From(ApiResponse<object>.Failure(new ApiError(ApiStatusCode.BadRequest, "ValidationError", "The request is invalid.",
                new[] { new ApiErrorDetail("Validation.InvalidValue", "The field or request format is invalid.", error.Field) }, Correlation.CorrelationId)));
        }
    }
}

[RoutePrefix("api/v1/sales-receipts")]
public sealed class SalesReceiptsController : WireApiController
{
    public SalesReceiptsController(IMediator mediator, IRequestCorrelationAccessor correlation) : base(mediator, correlation) { }
    [HttpGet, Route("")]
    public Task<HttpResponseMessage> List(CancellationToken cancellationToken) => Execute<SalesReceiptListPage>(async () =>
    {
        await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new ListSalesReceiptsQuery(SalesWireInput.List(Request)), cancellationToken).ConfigureAwait(false);
    }, SalesWireOutput.Page);
    [HttpGet, Route("{saleId}")]
    public Task<HttpResponseMessage> Get(string saleId, CancellationToken cancellationToken) => Execute<SalesReceiptDto>(async () =>
    {
        SalesWireInput.NoQuery(Request); await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new GetSalesReceiptByIdQuery(SalesWireInput.Long(saleId)), cancellationToken).ConfigureAwait(false);
    }, SalesWireOutput.Detail);
    [HttpPost, Route("")]
    public Task<HttpResponseMessage> Create(CancellationToken cancellationToken) => Execute<SalesReceiptDto>(async () =>
    {
        var body = await SalesWireInput.Body(Request).ConfigureAwait(false);
        return await Mediator.Send(new CreateSalesReceiptDraftCommand(SalesWireInput.Create(body)), cancellationToken).ConfigureAwait(false);
    }, SalesWireOutput.Detail);
    [HttpPut, Route("{saleId}/draft")]
    public Task<HttpResponseMessage> Update(string saleId, CancellationToken cancellationToken) => Execute<SalesReceiptDto>(async () =>
    {
        var id = SalesWireInput.Long(saleId); var body = await SalesWireInput.Body(Request).ConfigureAwait(false);
        return await Mediator.Send(new UpdateSalesReceiptDraftCommand(SalesWireInput.Update(id, body)), cancellationToken).ConfigureAwait(false);
    }, SalesWireOutput.Detail);
    [HttpPost, Route("{saleId}/post")]
    public Task<HttpResponseMessage> Post(string saleId, CancellationToken cancellationToken) => Execute<PostSalesReceiptResponse>(async () =>
    {
        var id = SalesWireInput.Long(saleId); var body = await SalesWireInput.Body(Request).ConfigureAwait(false);
        return await Mediator.Send(new PostSalesReceiptCommand(SalesWireInput.Post(id, body)), cancellationToken).ConfigureAwait(false);
    }, SalesWireOutput.Post);
}

[RoutePrefix("api/v1/catalog")]
public sealed class CatalogLookupController : WireApiController
{
    public CatalogLookupController(IMediator mediator, IRequestCorrelationAccessor correlation) : base(mediator, correlation) { }
    [HttpGet, Route("products/lookup")]
    public Task<HttpResponseMessage> Products(CancellationToken cancellationToken) => Lookup(request => new LookupProductsQuery(request), cancellationToken);
    [HttpGet, Route("customers/lookup")]
    public Task<HttpResponseMessage> Customers(CancellationToken cancellationToken) => Lookup(request => new LookupCustomersQuery(request), cancellationToken);
    [HttpGet, Route("warehouses/lookup")]
    public Task<HttpResponseMessage> Warehouses(CancellationToken cancellationToken) => Lookup(request => new LookupWarehousesQuery(request), cancellationToken);
    [HttpGet, Route("employees/lookup")]
    public Task<HttpResponseMessage> Employees(CancellationToken cancellationToken) => Lookup(request => new LookupEmployeesQuery(request), cancellationToken);
    private Task<HttpResponseMessage> Lookup(Func<CatalogLookupRequest, IRequest<ErrorOr<IReadOnlyList<CatalogLookupDto>>>> query, CancellationToken cancellationToken) =>
        Execute<IReadOnlyList<CatalogLookupDto>>(async () =>
        {
            await SalesWireInput.EmptyBody(Request).ConfigureAwait(false);
            return await Mediator.Send(query(SalesWireInput.Lookup(Request)), cancellationToken).ConfigureAwait(false);
        }, rows => rows);
}
