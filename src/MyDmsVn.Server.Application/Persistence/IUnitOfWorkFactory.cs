using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Application.Persistence;

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();

    Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken);
}
