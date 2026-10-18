# Persistence — current implementation and L2DB target

**Current baseline:** SQL Server, RepoDb command storage, Dapper projections/procedure mapping, native Microsoft.Data.SqlClient for explicit SQL, and DbUp migrations on active `net48`. This describes source assessed at `v1a` commit `c7ad32a4b0040f87417973eabd6c12757c54f7d5` on 2026-10-18.

**Approved target:** [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) replaces active RepoDb/Dapper consumers with LINQ to DB plus a documented native SqlClient surface after [RESP](plans/20261018-002-respawn-fixture-reuse.md) establishes an accepted fixture baseline using the current DAL. Neither plan is implemented by this documentation refresh. [ADR 0009](adr/0009-linq2db-local-net48-persistence.md) owns the provider decision; the ownership and behavior rules below apply throughout.

## 1. Layer separation

Command handlers depend on business repository interfaces in Server.Application. Dedicated query-service interfaces return materialized Contracts DTOs. SQL implementation, provider mapping, execution contexts and connection ownership adapters stay in Server.Infrastructure.

Current Catalog and receipt storage uses RepoDb; query projections and security consumers use Dapper where present. Existing locking, posting and reconciliation SQL uses native SqlClient where exact SQL matters. Keep an accurate consumer inventory during L2DB; a library name is not evidence that every repository uses that library for every operation.

No provider objects, `DataConnection`, `IQueryable`, SQL connections/transactions or provider mapping attributes may cross into Application, Domain, Contracts, Views or ViewModels. The future DAL keeps existing business interfaces and explicit registrations. DbUp remains the sole schema owner.

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

### Proposed LINQ to DB adapters

L2DB proposes an Infrastructure-only session factory:

```csharp
LinqToDB.Data.DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction);
Task<LinqToDB.Data.DataConnection> OpenOwnedAsync(CancellationToken cancellationToken);
```

These methods are planned, not existing source. T01 must bind them to the pinned package's actual API and prove its behavior on net48. A borrowed wrapper obtains the current connection/transaction for each operation and owns neither. Wrapper disposal, query failure and cancellation must not close, dispose, commit or roll back UoW resources. Invalid provider/state or a missing required transaction fails before SQL without opening a fallback connection.

An owned standalone wrapper opens through the existing connection factory, owns connection disposal and cleans up if wrapper construction fails. Mapping composition is published only after successful construction and is then reused read-only; a failed initialization cannot poison all later requests.

## 3. Posting, concurrency and query behavior

Goods receipt Post owns one explicit transaction. It locks the receipt header, loads its lines and validates locked Warehouse, Employee and Products in stable ProductId order. It performs the conditional Draft/expected-rowversion transition, appends ledger entries, applies sorted balance deltas and commits. Any failure before commit rolls back status, ledger and balances together.

- `StockBalances` has the `(WarehouseId, ProductId)` primary key; ledger source identity is unique on `(DocumentType, DocumentLineId)`.
- Concurrent receipts affecting the same balance cannot lose increments. Preserve atomic `UPDATE Quantity = Quantity + @delta` and the existing lock-safe missing-row handling inside the transaction.
- Preserve receipt header → Warehouse → Employee → sorted Products → sorted StockBalances lock order. Ordinary catalog SetActive updates wait on held master-data locks rather than invalidating a posting check mid-transaction.
- Reconciliation reads ledger totals and balances in one explicit `SERIALIZABLE` transaction. It acquires shared table locks in StockLedger → StockBalances order, matching Post, to avoid mixed committed states and lock-order inversion.
- Preserve conditional affected-row checks, receipt sequence numbering, returned IDs, byte-array rowversion, SQL decimal precision/scale, append-only ledger identity and stock-card chronological tie-breakers.
- Do not replace this SQL with unrestricted entity updates, naive `MERGE` or C# read-modify-write balances. A rowversion does not replace atomic increment locking.
- Any deadlock retry is bounded and tested at an operation-safe boundary. Deliberate multiple SQL sessions inside a concurrency test are distinct from runner parallelism.

Catalog queries preserve literal substring search, Unicode, blank/trim normalization, active-only lookups, deterministic code/ID ordering, `COUNT_BIG` and long offsets. Preserve write-field allowlists, audit fields, generated IDs, missing-row behavior and SQL-error mapping. Do not add catalog concurrency tokens absent from the schema or contracts. Native parameterized SqlClient remains acceptable for exact paging/multiple-result/procedure/locking semantics; every retained surface needs a file/method/reason in the L2DB verification record.

## 4. Mapping and security boundaries

Current RepoDb mapping is initialized only after successful setup; its runtime mapping hooks remain until their last consumers are converted. Both current and target mappings must describe real schema/table, primary keys, identities, column names, nullability, Unicode lengths, decimal precision/scale and SQL-generated fields explicitly. Never map a business code as a primary key merely for convenience. Distinguish entity-not-found from SQL failure.

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

RESP may reset data only in explicitly owned, disposable test databases through its reviewed fixture design. Preserve only the verified migration journal and ownership metadata; clear/reseed mutable seed data. Identity and sequence behavior, independent sessions and exceptional fresh-database tests require explicit reset contracts. This is test lifecycle work, never a permission to reset an application database. Preserve approved-instance checks, ownership journals and positive teardown.

## 6. Validation and rollout

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) describes behavioral coverage. Keep focused tests for each changed feature and broaden across SQL owners when shared connection/UoW/mapping/harness/migration behavior changes. RESP and L2DB final milestones require complete applicable active net48 SQL evidence through the guarded tooling. There is no automatic full repository/GUI run after every task or code PR, and no mandatory multi-pass performance benchmark.

Required invariants include commit/rollback/dispose-without-commit, two repositories sharing a transaction, same-transaction read visibility, independent UoWs, late transaction binding, resolver/lifetime failures, cancellation/exception cleanup, duplicate/concurrent posting, reconciliation and least-privileged security behavior. Mocks alone cannot prove SQL transaction/locking correctness.

RESP is accepted against current RepoDb/Dapper first. L2DB-T00 records that accepted commit and freezes fixture boundaries, reset behavior and runner protocol during DAL conversion. Preserve independent native-SQL setup/assertions so a replacement DAL cannot validate itself. Retain necessary fresh-database exceptions; fixture reuse never relaxes observable isolation. Only verified code cutover allows current implementation documentation to describe LINQ to DB as delivered.
