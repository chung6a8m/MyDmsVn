using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyDmsVn.SqlTestHarness.Approval;
using MyDmsVn.SqlTestHarness.Ownership;

namespace MyDmsVn.SqlTestHarness.Preflight;

public interface ISqlAdministrativeSession : IDisposable
{
    Task OpenAsync(CancellationToken cancellationToken);

    Task<string?> ReadEffectiveDatabaseAsync(CancellationToken cancellationToken);

    Task<SqlInstanceIdentity?> ReadIdentityAsync(CancellationToken cancellationToken);

    Task<int?> ReadMajorVersionAsync(CancellationToken cancellationToken);

    Task<bool?> HasServerPermissionAsync(string permissionName, CancellationToken cancellationToken);

    Task<LiveDatabaseOwnership?> ReadDatabaseOwnershipAsync(
        string databaseName,
        CancellationToken cancellationToken);

    Task ExecuteAsync(
        string sql,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken);
}
