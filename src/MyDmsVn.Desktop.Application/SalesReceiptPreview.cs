using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Desktop.Application;

public interface ISalesReceiptPreviewCalculator
{
    decimal CalculateRevenue(decimal qty, decimal unitPrice, bool isGift);
    decimal SumAmounts(IEnumerable<decimal> amounts);
}

public enum SalesCommandIntent { Save, Post }

public interface ISalesCommandBufferAdmission
{
    Task<bool> TryCommitPendingEditsAsync(SalesCommandIntent intent, CancellationToken cancellationToken);
}
