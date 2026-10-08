using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.Server.Infrastructure.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection OpenConnection();

    Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
