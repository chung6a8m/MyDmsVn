using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyDmsVn.SqlTestHarness.Ownership;

public interface IDatabaseOwnershipJournal
{
    Task WriteCreateIntentAsync(
        DatabaseOwnershipRecord record,
        CancellationToken cancellationToken = default);

    Task<DatabaseOwnershipRecord> TransitionAsync(
        DatabaseOwnershipRecord expectedRecord,
        DatabaseOwnershipState newState,
        int? databaseId = null,
        DateTimeOffset? databaseCreateDateUtc = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseOwnershipRecord>> ReadAllAsync(
        CancellationToken cancellationToken = default);
}
