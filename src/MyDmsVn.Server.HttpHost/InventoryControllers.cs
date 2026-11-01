using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using MediatR;
using MyDmsVn.Contracts;
using MyDmsVn.Server.Application;
using MyDmsVn.Server.Application.Inventory;
using MyDmsVn.Server.Application.Operations;

namespace MyDmsVn.Server.HttpHost;

[RoutePrefix("api/v1/goods-receipts")]
public sealed class GoodsReceiptsController : WireApiController
{
    public GoodsReceiptsController(IMediator mediator, IRequestCorrelationAccessor correlation) : base(mediator, correlation) { }

    [HttpGet, Route("")]
    public Task<HttpResponseMessage> List(CancellationToken cancellationToken) => Execute<PagedResult<GoodsReceiptDto>>(async () =>
    {
        await InventoryWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new ListGoodsReceiptsQuery(InventoryWireInput.List(Request)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.ReceiptPage);

    [HttpGet, Route("{receiptId}")]
    public Task<HttpResponseMessage> Get(string receiptId, CancellationToken cancellationToken) => Execute<GoodsReceiptDto>(async () =>
    {
        InventoryWireInput.NoQuery(Request);
        await InventoryWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new GetGoodsReceiptByIdQuery(InventoryWireInput.Long(receiptId)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.Detail);

    [HttpPost, Route("")]
    public Task<HttpResponseMessage> Create(CancellationToken cancellationToken) => Execute<GoodsReceiptDto>(async () =>
    {
        var body = await InventoryWireInput.Body(Request).ConfigureAwait(false);
        var input = InventoryWireInput.Create(body);
        return await Mediator.Send(new CreateGoodsReceiptDraftCommand(input, Operation(WebOperationKind.ReceiptCreate, null, body)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.Detail);

    [HttpPut, Route("{receiptId}/draft")]
    public Task<HttpResponseMessage> Update(string receiptId, CancellationToken cancellationToken) => Execute<GoodsReceiptDto>(async () =>
    {
        var id = InventoryWireInput.Long(receiptId);
        var body = await InventoryWireInput.Body(Request).ConfigureAwait(false);
        var input = InventoryWireInput.Update(id, body);
        return await Mediator.Send(new UpdateGoodsReceiptDraftCommand(input, Operation(WebOperationKind.ReceiptUpdate, id, body)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.Detail);

    [HttpPost, Route("{receiptId}/post")]
    public Task<HttpResponseMessage> Post(string receiptId, CancellationToken cancellationToken) => Execute<PostGoodsReceiptResponse>(async () =>
    {
        var id = InventoryWireInput.Long(receiptId);
        var body = await InventoryWireInput.Body(Request).ConfigureAwait(false);
        var input = InventoryWireInput.Post(id, body);
        return await Mediator.Send(new PostGoodsReceiptCommand(input, Operation(WebOperationKind.ReceiptPost, id, body)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.Post);
}

[RoutePrefix("api/v1/inventory")]
public sealed class InventoryController : WireApiController
{
    public InventoryController(IMediator mediator, IRequestCorrelationAccessor correlation) : base(mediator, correlation) { }

    [HttpGet, Route("balances")]
    public Task<HttpResponseMessage> Balances(CancellationToken cancellationToken) => Execute<PagedResult<StockBalanceRow>>(async () =>
    {
        await InventoryWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new GetStockBalancesQuery(InventoryWireInput.Balances(Request)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.BalancesPage);

    [HttpGet, Route("stock-card")]
    public Task<HttpResponseMessage> StockCard(CancellationToken cancellationToken) => Execute<PagedResult<StockCardRow>>(async () =>
    {
        await InventoryWireInput.EmptyBody(Request).ConfigureAwait(false);
        return await Mediator.Send(new GetStockCardQuery(InventoryWireInput.StockCard(Request)), cancellationToken).ConfigureAwait(false);
    }, InventoryWireOutput.StockCardPage);
}
