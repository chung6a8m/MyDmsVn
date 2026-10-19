using System.Threading;
using System.Threading.Tasks;
using LinqToDB.Data;

namespace MyDmsVn.Server.Infrastructure.Persistence;

internal interface ILinqToDbSessionFactory
{
    DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction);
    Task<DataConnection> OpenOwnedAsync(CancellationToken cancellationToken);
}
