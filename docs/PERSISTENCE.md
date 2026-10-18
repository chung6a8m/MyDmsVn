# Persistence — LINQ to DB/native implementation

**Current implementation:** Catalog uses LINQ to DB writes/typed reads and a native SqlClient count/page escape after [T03 verification](L2DB_VERIFICATION.md#l2db-t03--catalog-conversion). Inventory now uses LINQ writes/reference loads and typed native batch readers after [T04 verification](L2DB_VERIFICATION.md#l2db-t04--inventory-conversion). Identity/Security/operational consumers use typed native SqlClient after [T05 verification](L2DB_VERIFICATION.md#l2db-t05--identity-security-and-operations-conversion), with transitional hooks/packages/pilots removed in T06; explicit UoW and DbUp remain on active net48. The pre-migration baseline was assessed at `v1a` commit `c7ad32a4b0040f87417973eabd6c12757c54f7d5` on 2026-10-18.

**Delivered direction and remaining acceptance:** [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) replaces active RepoDb/Dapper consumers with LINQ to DB plus a documented native SqlClient surface after [RESP](plans/20261018-002-respawn-fixture-reuse.md) established the accepted pre-migration fixture baseline. RESP and L2DB-T00–T06 now have scoped evidence; T07 frozen-revision package/manual acceptance remains pending. [ADR 0009](adr/0009-linq2db-local-net48-persistence.md) owns the provider decision; the ownership and behavior rules below apply throughout.

## 1. Layer separation

Command handlers depend on business repository interfaces in Server.Application. Dedicated query-service interfaces return materialized Contracts DTOs. SQL implementation, provider mapping, execution contexts and connection ownership adapters stay in Server.Infrastructure.

Catalog now uses explicit LINQ allowlists/mappings and owned typed queries, with native COUNT_BIG/long-offset lists. Receipt/header/line/ledger storage and Warehouse/Employee reference loads now use registered LINQ mappings and explicit allowlists. Native readers replace Inventory Dapper projections while retaining the same SQL algorithms and per-call connection/transaction ownership. The obsolete Catalog/Inventory RepoDb mappings are removed; T06 removes generic legacy hooks and converts Foundation probes to registered LINQ mappings plus independent native test oracles. Identity/Security/operational consumers now use typed native readers/parameters for the existing procedure/diagnostic/maintenance SQL. Readers drain deferred errors before reporting success; credential/maintenance audit times bind as DateTime2 to match schema precision. Existing locking, posting and reconciliation SQL uses native SqlClient where exact SQL matters. Keep an accurate consumer inventory during L2DB; a library name is not evidence that every repository uses that library for every operation.

No provider objects, `DataConnection`, `IQueryable`, SQL connections/transactions or provider mapping attributes may cross into Application, Domain, Contracts, Views or ViewModels. The delivered DAL keeps existing business interfaces and explicit registrations. DbUp remains the sole schema owner.

## 2. Existing Unit of Work contract

The current interfaces live in `src/MyDmsVn.Server.Application/Persistence/`:

```csharp
public interface IUnitOfWork : IDisposable
{
    UnitOfWorkState State { get; }
    void BeginTransaction();
    TRepository Repository<TRepository>() where TRepository : class;
    void Commit();
    void Rollback();
}

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
    Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken);
}
```

The existing state values are `Created`, `ActiveTransaction`, `Committed`, `RolledBack` and `Disposed`. `SqlUnitOfWorkFactory` owns the implementation/state machine; `ISqlExecutionContext` exposes the guarded current connection and optional current transaction only within Infrastructure.

1. The factory creates a fresh opened connection and transfers its ownership to the UoW. One UoW has at most one active transaction.
2. Repositories resolved from the UoW use that connection and its current explicit transaction for all participating operations, including asynchronous calls.
3. The factory is resolved from the caller DI scope. Repositories receive the same scoped request/user context. The UoW disposes repository instances it creates, while caller-scoped dependencies remain caller-owned.
4. Begin, commit and rollback obey the existing state machine. A second begin is an error; completion/disposal prevents further database operations through the scope. Dispose of an uncommitted transaction attempts rollback and cleanup occurs exactly once.
5. A repository resolved before `BeginTransaction` must use a transaction begun later. Do not capture the constructor-time transaction as permanent state.
6. Do not use ambient `TransactionScope`, static/AsyncLocal transaction state or hidden fallback connections for participating work.
7. Do not run concurrent commands on one connection/UoW. Parallel work needs separately owned scopes; cancellation/failure must release owned resources safely.
8. Register business repository interfaces explicitly with `SqlPersistenceBuilder.AddRepository<TRepository,TImplementation>()`. Unregistered or invalid repository types fail clearly; no arbitrary first-type assembly scanning or general `GetService<T>()` API belongs in the UoW.

### Repository resolution and reads

```csharp
using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken);
uow.BeginTransaction();
var repository = uow.Repository<IGoodsReceiptWriteRepository>();
// Execute participating work and materialize the required result.
uow.Commit();
```

`Repository<TRepository>()` is a typed UoW-scoped resolver, not a general service locator. Application asks for a business interface, never `Sql*Repository` or an Infrastructure execution context. Repositories belong to the UoW lifetime and must not escape or be shared concurrently.

Standalone CQRS reads do not require a UoW by default. Infrastructure owns their connection and fully materializes DTOs before disposal. A query that needs same-transaction visibility uses the command's explicit connection/transaction; it cannot silently open its own connection. Preserve explicit registration checks and multi-repository atomicity tests. Changing to direct repository injection would require a separately proven ownership design, beyond provider replacement.

### LINQ to DB session adapters (verified T01 seam)

L2DB-T01 introduced the internal Infrastructure-only `ILinqToDbSessionFactory` seam; T03–T06 now use it for production LINQ operations:

```csharp
LinqToDB.Data.DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction);
Task<LinqToDB.Data.DataConnection> OpenOwnedAsync(CancellationToken cancellationToken);
```

These methods exist in the verified T01 seam; [verification](L2DB_VERIFICATION.md#l2db-t01--net48-package-and-resource-ownership) binds their net48/package/ownership evidence. T03/T04 now compose production repositories explicitly through this seam. The [initial T02 pilot](L2DB_VERIFICATION.md#l2db-t02--three-slice-pilot-no-go) found a baseline missing-target reader defect; the authorized repair below resolves it. Product list batching/long offsets and Security allowlisted procedures use explicit native escape paths; receipt writes interoperate with unchanged native locking/posting/balance SQL. [L2DB-PILOT-01 repair](L2DB_VERIFICATION.md#l2db-t02-blocker-repair--l2db-pilot-01) changes the baseline to ReadSingleOrDefaultAsync plus an invariant null guard, surfacing deferred SQL51031 through the existing mapper; strict User/Role NotFound parity now passes. The [final T02 GO](L2DB_VERIFICATION.md#l2db-t02-final-go--approved-runtime-startup) includes schema9/x86 package verification and successful real restricted-runtime startup, with approved temporary login/database cleanup. T03 registers the Catalog mapping and T04 registers Inventory mapping and borrowed writes/reference loads. Inventory native-only queries retain their existing factory-owned connection/transaction path. T05 converts Identity/Security/operational consumers to native SqlClient on their existing explicit factory/UoW/maintenance connection paths; T06 removes transitional hooks/packages/pilots and active old-ORM dependencies. [T06 evidence](L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup) records the native test-oracle conversion and retained fixture boundaries. Each wrapper is operation-scoped and must be materialized/disposed before a UoW state transition; repositories borrow anew for each operation. A borrowed wrapper obtains the current connection/transaction for each operation and owns neither. Wrapper disposal, query failure and cancellation must not close, dispose, commit or roll back UoW resources. Invalid provider/state or a missing required transaction fails before SQL without opening a fallback connection.

An owned standalone wrapper opens through the existing connection factory, owns connection disposal and cleans up if wrapper construction fails. Mapping composition is published only after successful construction and is then reused read-only; a failed initialization cannot poison all later requests. The internal provider composes a private candidate under an instance lock and publishes after FluentMappingBuilder.Build; the package MappingSchema remains mutable, so Infrastructure consumers must not mutate the published schema. Explicit MicrosoftDataSqlClient provider selection and a conservative v2012 dialect avoid automatic server detection opening another connection; T02 reviews the generated SQL before rollout. UseConnection and UseTransaction are mutually exclusive option paths. Borrowed transactions stay UoW-owned; owned standalone wrappers use DisposeConnection=true. The wrapper default command timeout is 30 seconds and async operations receive caller cancellation.

## 3. Posting, concurrency and query behavior

Goods receipt Post owns one explicit transaction. It locks the receipt header, loads its lines and validates locked Warehouse, Employee and Products in stable ProductId order. It performs the conditional Draft/expected-rowversion transition, appends ledger entries, applies sorted balance deltas and commits. Any failure before commit rolls back status, ledger and balances together.

- `StockBalances` has the `(WarehouseId, ProductId)` primary key; ledger source identity is unique on `(DocumentType, DocumentLineId)`.
- Concurrent receipts affecting the same balance cannot lose increments. Preserve atomic `UPDATE Quantity = Quantity + @delta` and the existing lock-safe missing-row handling inside the transaction.
- Preserve receipt header → Warehouse → Employee → sorted Products → sorted StockBalances lock order. Ordinary catalog SetActive updates wait on held master-data locks rather than invalidating a posting check mid-transaction.
- Reconciliation reads ledger totals and balances in one explicit `SERIALIZABLE` transaction. It acquires shared table locks in StockLedger → StockBalances order, matching Post, to avoid mixed committed states and lock-order inversion.
- Preserve conditional affected-row checks, receipt sequence numbering, returned IDs, byte-array rowversion, SQL decimal precision/scale, append-only ledger identity and stock-card chronological tie-breakers.
- Do not replace this SQL with unrestricted entity updates, naive `MERGE` or C# read-modify-write balances. A rowversion does not replace atomic increment locking.
- Any deadlock retry is bounded and tested at an operation-safe boundary. Deliberate multiple SQL sessions inside a concurrency test are distinct from concurrent test-project execution.

Catalog queries preserve literal substring search, Unicode, blank/trim normalization, active-only lookups, deterministic code/ID ordering, `COUNT_BIG` and long offsets. Preserve write-field allowlists, audit fields, generated IDs, missing-row behavior and SQL-error mapping. Do not add catalog concurrency tokens absent from the schema or contracts. Native parameterized SqlClient remains acceptable for exact paging/multiple-result/procedure/locking semantics; every retained surface needs a file/method/reason in the L2DB verification record.

## 4. Mapping and security boundaries

The composed LINQ mapping schema is published only after successful setup and retries after failure; no legacy mapping hook remains. Mappings must describe real schema/table, primary keys, identities, column names, nullability, Unicode lengths, decimal precision/scale and SQL-generated fields explicitly. Never map a business code as a primary key merely for convenience. Distinguish entity-not-found from SQL failure.

L2DB mappings stay in Infrastructure, including mappings of existing Domain POCOs. Operation-specific insert/update allowlists prevent generated/audit fields from being overwritten. Bulk copy is only in scope if an inventoried existing consumer needs it and shared-transaction, identity, constraints and cancellation semantics are proved.

Security administration uses the existing allowlisted procedures and maintenance/runtime authority boundary. Preserve current-subject/account checks, registered Active keys, the protected singleton superuser, direct-user precedence, active-role grants and default deny. Preserve additive import with revision/digest revalidation, 1 MiB UTF-8/5,000-entry limits and atomic audit. No generic ORM DML on restricted RBAC/binding tables and no widened SQL grants are authorized by L2DB. Preserve legacy password columns. See [SECURITY](SECURITY.md) and [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md).

## 5. Migrations and data safety

The current DbUp scripts under `src/MyDmsVn.Server.DbMigrator/Scripts/` are:

| Script | Responsibility |
|---|---|
| `001_PersistenceFoundation.sql` | Persistence foundation. |
| `002_Identity.sql` | Supported identity/RBAC schema and singleton superuser binding. |
| `003_Catalog.sql` | Catalog schema. |
| `004_Catalog_Nul_Constraints.sql` | Catalog NUL constraints. |
| `005_GoodsReceipt.sql` | Receipt schema. |
| `006_GoodsReceipt_Number_Sequence.sql` | Server-issued receipt sequence. |
| `007_Inventory_Stock.sql` | Stock ledger/balance schema. |
| `008_Security_Administration.sql` | Administration procedure, assignment revisions and security audit. |
| `009_Security_Permission_Import.sql` | Assignment-import read/apply procedures using existing revisions and audit. |

Preserve the current supported post-security-rebuild baseline, numbered scripts and DbUp history. The earlier unreleased security redesign is historical and does not authorize another schema rewrite or data reset. L2DB changes DAL implementation, not production schema, grants, assignments or migration history. Any necessary production schema change needs a separate reviewed scope decision.

Run migrations as a controlled deployment command, not from desktop login. Require an explicit application database and reject SQL Server system databases before connecting. Runtime client accounts must not need DDL. The current migrator admits only Empty or Compatible databases; a valid older prefix is rejected as `Migration.UnsupportedExistingDatabase`, not automatically upgraded or resumed. Migration coverage includes clean creation, repeated no-op application and supported-baseline compatibility/refusal; operational rollback is compatible code redeployment or backup/restore, not an automatic DbUp downgrade.

RESP may reset data only in explicitly owned, disposable test databases through its reviewed fixture design. Preserve only the verified migration journal and ownership metadata; clear/reseed mutable seed data. Identity and sequence behavior, independent sessions and exceptional fresh-database tests require explicit reset contracts. This is test lifecycle work, never a permission to reset an application database. Use the explicitly designated non-production instance and retain local live instance/allocation/marker validation before reset or teardown. Ambiguous or interrupted allocations need manual review; no ownership journal or automatic orphan cleanup remains.

## 6. Validation and rollout

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) describes behavioral coverage. Keep focused tests for each changed feature and broaden across SQL owners when shared connection/UoW/mapping/fixture/migration behavior changes. RESP and L2DB final milestones require complete applicable active net48 SQL evidence through direct sequential project commands and the local owned-database lifecycle. There is no automatic full repository/GUI run after every task or code PR, and no mandatory multi-pass performance benchmark.

Required invariants include commit/rollback/dispose-without-commit, two repositories sharing a transaction, same-transaction read visibility, independent UoWs, late transaction binding, resolver/lifetime failures, cancellation/exception cleanup, duplicate/concurrent posting, reconciliation and least-privileged security behavior. Mocks alone cannot prove SQL transaction/locking correctness.

RESP was accepted against the pre-migration RepoDb/Dapper baseline first. L2DB-T00 records that accepted commit and freezes fixture boundaries, reset behavior and direct test commands during DAL conversion. Preserve independent native-SQL setup/assertions so a replacement DAL cannot validate itself. Retain necessary fresh-database exceptions; fixture reuse never relaxes observable isolation. Only verified code cutover allows current implementation documentation to describe LINQ to DB as delivered.
