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


### 2.1. Repository resolution convention

Application command handlers are permitted to obtain **repository interfaces** from their own explicit UoW:

```csharp
using var uow = await _unitOfWorkFactory.CreateAsync(cancellationToken);
uow.BeginTransaction();

var repository = uow.Repository<IGoodsReceiptWriteRepository>();
// Execute command-side operations through this repository.

uow.Commit();
// Disposal rolls back an active uncommitted transaction.
```

- `TRepository` must be a business repository **interface** defined in `Server.Application` (or an appropriate inner layer), never a concrete SQL repository class. Application and Domain must not depend on `Server.Infrastructure`, `ISqlExecutionContext`, `IDbConnection`, `IDbTransaction`, RepoDb, Dapper, or SQL implementation details.
- `Repository<TRepository>()` is **only** a UoW-scoped repository resolver, not a generic service locator. Register each interface-to-implementation mapping explicitly (for example via `SqlPersistenceBuilder.AddRepository<TRepository, TImplementation>()`); unregistered interfaces must fail clearly. Do not add arbitrary `GetService<T>()` or `Resolve<T>()` APIs to the UoW.
- Resolution must bind each repository to the current UoW's connection and transaction. Multiple repositories resolved from the same UoW share that scope; no repository can silently open its own connection or transaction for participating work. Pass the active transaction explicitly to RepoDb operations, including async calls.
- Repositories belong to their UoW's lifetime. Do not use or expose them after the UoW is disposed, or share the UoW/repositories concurrently. The UoW is responsible for disposing repository instances it creates, not injected caller-scoped dependencies.
- Standalone CQRS queries use Application query-service interfaces returning DTOs; they do **not** need a UoW by default. When a query participates in an active command transaction, Infrastructure uses an explicit shared execution context with the same connection and transaction.
- The tradeoff is that repository dependencies are looked up within the handler rather than listed in its constructor. Preserve strong interface typing, explicit registrations, startup/composition validation where feasible, and SQL integration tests for multi-repository atomicity. Switching to direct repository injection would require a separately proven transaction/lifetime design, not just a syntax change.

This convention documents the existing `IUnitOfWork` contract and accepted [ADR 0002](adr/0002-explicit-unit-of-work.md); it is not a new Unit of Work strategy.

## 3. Example posting ownership

```text
PostGoodsReceiptHandler
  -> create UoW
  -> begin transaction
  -> lock receipt header
  -> load lines and lock Warehouse, Employee, then Products by ProductId
  -> receiptRepo.TryMarkPosted(receiptId, expected Draft + rowversion, ...)
  -> receiptRepo.AppendStockLedger(documentLine IDs)
  -> receiptRepo.ApplyBalanceDeltas by (WarehouseId, ProductId)
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
- Posting uses the consistent lock order GoodsReceipt header → Warehouse → Employee → Products by ProductId → StockBalances by (WarehouseId,ProductId). Catalog SetActive operations use ordinary SQL updates and therefore wait on held master-data locks rather than invalidating an in-flight Post check.
- Reconciliation reads grouped ledger totals and balances inside one explicit `SERIALIZABLE` transaction. It first acquires shared table locks in the same StockLedger → StockBalances order as Post, so it neither reports two different committed states nor creates a join-plan lock-order inversion while Post is in flight.
- `rowversion` can protect UI edits and assist optimistic consistency, but **does not replace** atomic increment locking.
- Ledger source identity, receipt status and balance update should be verified together in SQL integration tests.

## 5. RepoDb mapping

- Map table/schema/PK/identity, column names and DB types explicitly; verify mapping APIs against pinned RepoDb 1.16.x.
- Text in Vietnamese should use NVARCHAR/Unicode; only specify ANSI/VARCHAR when a documented legacy constraint requires it.
- Initialize RepoDb mapping **once after successful setup**; do not set the initialized flag before mapping completes.
- Do not map a business code as PK when SQL uses another actual PK without a documented key policy.
- Enforce nullable reference handling for `GetById`; distinguish entity-not-found from SQL failures.

## 6. Migrations and data safety

- DbUp uses numbered, immutable scripts. The implemented chain is `001_PersistenceFoundation.sql`, `002_Identity.sql`, `003_Catalog.sql`, `004_Catalog_Nul_Constraints.sql`, `005_GoodsReceipt.sql`, `006_GoodsReceipt_Number_Sequence.sql` and `007_Inventory_Stock.sql`; later migrations remain additive.
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
