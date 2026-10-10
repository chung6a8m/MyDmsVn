# Persistence design — SQL Server / RepoDb / Dapper / Explicit UoW

## 1. Separation

- Command handlers interact with `IRepository` abstractions implemented using RepoDb.
- Complex read models use `IProductQueryService`, `IInventoryQueryService` and related Application abstractions, implemented with Dapper; query services return **DTOs**, not mutable Domain entities.
- All persistence implementations are in `Server.Infrastructure`. SQL mappings reflect real schema explicitly.
- DbUp scripts are embedded in a dedicated migrator and versioned deterministically.

## 2. Unit of Work contract

Preferred sketch (signature details may change after P0 compatibility checks):

```csharp
public interface IUnitOfWork : IDisposable
{
    void BeginTransaction();
    TRepository Repository<TRepository>() where TRepository : class;
    void Commit();
    void Rollback();
}

public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
```

Rules:
1. Factory creates a **fresh opened IDbConnection** and transfers its ownership to the UoW.
2. UoW owns one connection and at most one active IDbTransaction.
3. Repositories are **scoped to the UoW** and use that connection; all RepoDb writes/reads get the current transaction explicitly (including async methods).
   `IUnitOfWorkFactory` is resolved from the caller DI scope so repository constructors receive the same scoped request/user/tenant context. The UoW disposes repository instances it creates, but it does not own or dispose caller-scoped dependencies.
4. Dapper query services that need to participate in a transaction get the same connection + transaction via a scoped, explicit execution context.
5. BeginTransaction twice is an error unless a future ADR explicitly defines nested semantics; do **not** silently return or replace.
6. Commit/Rollback allowed only in legal states, cleanup occurs exactly once; dispose of uncommitted transaction always attempts rollback.
7. No global `static AsyncLocal<IDbTransaction>`, implicit thread-local state or ambient shared transaction.
8. No concurrent commands on one connection/UoW; create separate scopes for parallel work. Cancellation must dispose safely.
9. Sync methods are sufficient for initial ownership API, but async `CommitAsync/RollbackAsync/DisposeAsync` may be added where supported by target-specific implementations.
10. Repositories are registered explicitly with DI; do not discover the first assignable type by arbitrary assembly scan.

## 3. Example posting ownership

```text
PostGoodsReceiptHandler
  -> create UoW
  -> begin transaction
  -> receiptRepo.TryMarkPosted(receiptId, expected Draft, ...)
  -> ledgerRepo.AppendOnce(documentLine IDs)
  -> balances.ApplyDeltasByWarehouseProduct(...)
  -> commit
  -> dispose
```

Any failure before commit must rollback. Never open another independent connection from a repository inside that handler. A status update is **conditional**; 0 affected rows means missing/already-posted/concurrent transition, resolved using a read as necessary. Enforce a unique index on movement source identity to guard against double ledger entries.

## 4. Concurrency and balance consistency

For P5:
- ```text
  StockBalances PRIMARY KEY (WarehouseId, ProductId)
  StockLedger UNIQUE (DocumentType, DocumentLineId)
  GoodsReceipt Status conditional update Draft -> Posted
  ```
- Posting multiple receipts to same warehouse/product must not lose increments.
- Suggested SQL strategy: atomic `UPDATE Quantity = Quantity + @delta`; for missing rows use an explicit serializable/UPDLOCK/HOLDLOCK-safe upsert or controlled retry on unique-key violation. Upsert and ledger insert remain **inside one transaction**.
- Avoid unguarded "read quantity, add in C#, overwrite" or naive concurrent `MERGE`.
- Sort balance keys before applying multiple increments to reduce deadlock risk. Deadlocks/concurrency faults get bounded, explicitly tested retry only at an operation-safe boundary.
- `rowversion` can protect UI edits and assist optimistic consistency, but **does not replace** atomic increment locking.
- Ledger source identity, receipt status and balance update should be verified together in SQL integration tests.

## 5. RepoDb mapping

- Map table/schema/PK/identity, column names and DB types explicitly; verify mapping APIs against pinned RepoDb 1.16.x.
- Text in Vietnamese should use NVARCHAR/Unicode; only specify ANSI/VARCHAR when a documented legacy constraint requires it.
- Initialize RepoDb mapping **once after successful setup**; do not set the initialized flag before mapping completes.
- Do not map a business code as PK when SQL uses another actual PK without a documented key policy.
- Enforce nullable reference handling for `GetById`; distinguish entity-not-found from SQL failures.

## 6. Migrations and data safety

- DbUp uses numbered, immutable scripts. The implemented chain is `001_PersistenceFoundation.sql`, `002_Identity.sql`, `003_Catalog.sql`, `004_Catalog_Nul_Constraints.sql`, `005_GoodsReceipt.sql` and `006_GoodsReceipt_Number_Sequence.sql`; later Inventory scripts remain future additive migrations.
- History table under controlled schema; apply once, in order, with meaningful deployment logs.
- Run migrator as a **separate controlled deployment command**, not per-desktop login. Client DB logins should not require DDL.
- Require an explicit application database in the migration connection string and reject SQL Server system databases (`master`, `model`, `msdb`, `tempdb`) before opening a connection.
- Provide a clean reset script only for clearly named disposable **test** databases, with an explicit safety check; do not auto-drop existing DBs.
- Migration tests: new database, repeated no-op run, upgrade from last baseline, rollback **operational** procedure (DB backup/restore; DbUp does not automatically downgrade).
- Schema, RepoDb mapping and DTO projection must evolve together.

## 7. Tests required before P5

- UoW commit, rollback and dispose-without-commit;
- two repositories in same transaction;
- Dapper query seeing uncommitted same-transaction data when needed;
- parallel UoWs without cross-contamination;
- duplicate posting and concurrent balance increments;
- no leaked connections on exception and cancellation.

These must run against SQL Server in an isolated test environment; mocks alone do not prove ADO.NET transaction correctness.
