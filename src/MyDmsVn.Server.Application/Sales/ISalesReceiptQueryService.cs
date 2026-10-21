using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Sales;

public interface ISalesReceiptQueryService
{
    Task<SalesReceiptListPage> ListAsync(SalesReceiptListRequest request, CancellationToken cancellationToken);
    Task<SalesReceiptDto?> GetByIdAsync(long saleId, CancellationToken cancellationToken);
}
