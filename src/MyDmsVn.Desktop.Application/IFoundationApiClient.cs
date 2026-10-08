using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.Contracts;

namespace MyDmsVn.Desktop.Application
{
    public interface IFoundationApiClient
    {
        Task<FoundationStatus> GetStatusAsync(CancellationToken cancellationToken);
    }
}
