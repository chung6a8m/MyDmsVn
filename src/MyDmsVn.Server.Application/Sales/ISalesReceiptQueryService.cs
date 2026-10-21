using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Server.Application.Sales;

public interface ISalesReceiptQueryService
{
    Task<SalesReceiptDto?> GetByIdAsync(long saleId, CancellationToken cancellationToken);
}
