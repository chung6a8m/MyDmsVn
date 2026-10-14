using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestRunner.Reporting;

public interface ISqlDmvSnapshotReader
{
    Task<SqlDmvSnapshot?> ReadAsync(CancellationToken cancellationToken);
}

public sealed class UnavailableSqlDmvSnapshotReader : ISqlDmvSnapshotReader
{
    public Task<SqlDmvSnapshot?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SqlDmvSnapshot?>(null);
}
