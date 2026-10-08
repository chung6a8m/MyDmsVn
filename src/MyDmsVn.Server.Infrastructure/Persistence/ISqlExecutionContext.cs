using System.Data;

namespace MyDmsVn.Server.Infrastructure.Persistence;

public interface ISqlExecutionContext
{
    IDbConnection Connection { get; }

    IDbTransaction? Transaction { get; }
}
