using System;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;
using MyDmsVn.Desktop.Application;
using MyDmsVn.Server.Application.Sales;

namespace MyDmsVn.Desktop.Infrastructure.Local;

internal sealed class LocalSalesReceiptApiClient : ISalesReceiptApiClient
{
    private readonly LocalRequestDispatcher _dispatcher;
    private readonly long? _expectedSessionVersion;
    public LocalSalesReceiptApiClient(LocalRequestDispatcher dispatcher) : this(dispatcher, null) { }
    private LocalSalesReceiptApiClient(LocalRequestDispatcher dispatcher, long? expectedSessionVersion)
    { _dispatcher = dispatcher; _expectedSessionVersion = expectedSessionVersion; }
    public ISalesReceiptApiClient ForSession(long expectedSessionVersion)
    {
        if (_expectedSessionVersion.HasValue)
        {
            if (_expectedSessionVersion.Value != expectedSessionVersion)
                throw new InvalidOperationException("A Sales client cannot be rebound to another session.");
            return this;
        }
        return new LocalSalesReceiptApiClient(_dispatcher, expectedSessionVersion);
    }
    public Task<ApiResponse<SalesReceiptListPage>> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken) =>
        _dispatcher.SendForCurrentSessionAsync(new ListSalesReceiptsQuery(request), "Request", cancellationToken, _expectedSessionVersion);
    public Task<ApiResponse<SalesReceiptDto>> GetAsync(long saleId, CancellationToken cancellationToken) =>
        _dispatcher.SendForCurrentSessionAsync(new GetSalesReceiptByIdQuery(saleId), "Request", cancellationToken, _expectedSessionVersion);
    public Task<ApiResponse<SalesReceiptDto>> CreateDraftAsync(SaveSalesReceiptRequest request, CancellationToken cancellationToken) =>
        _dispatcher.SendForCurrentSessionAsync(new CreateSalesReceiptDraftCommand(request), "Request", cancellationToken, _expectedSessionVersion);
    public Task<ApiResponse<SalesReceiptDto>> UpdateDraftAsync(UpdateSalesReceiptDraftRequest request, CancellationToken cancellationToken) =>
        _dispatcher.SendForCurrentSessionAsync(new UpdateSalesReceiptDraftCommand(request), "Request", cancellationToken, _expectedSessionVersion);
    public Task<ApiResponse<PostSalesReceiptResponse>> PostAsync(PostSalesReceiptRequest request, CancellationToken cancellationToken) =>
        _dispatcher.SendForCurrentSessionAsync(new PostSalesReceiptCommand(request), "Request", cancellationToken, _expectedSessionVersion);
}
