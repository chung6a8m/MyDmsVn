using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public interface IFoundationApiClient
    {
        Task<ApiResponse<FoundationStatus>> GetStatusAsync(
            FoundationStatusRequest request,
            CancellationToken cancellationToken);
    }
}
