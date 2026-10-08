# P1 — Persistence foundation

Status: Not started. Depends on P0. Read `docs/PERSISTENCE.md`, `docs/TEST_STRATEGY.md`, ADR 0002.

## Objective

Prove RepoDb + Dapper + DbUp with **Explicit UnitOfWork** are correct for both net48 and net8.0 before any feature uses them.

## Task list

- [x] **P1-T01 / SQL test harness:** parameterized test connection `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`; isolated, clearly named disposable database creation with guardrails; never auto-drop arbitrary DB; document local SQL prerequisites.
- [x] **P1-T02 / Connection ownership:** `IDbConnectionFactory` fresh opened `SqlConnection` per UoW, correct disposal and cancellation/exception handling; test open connection behavior.
- [x] **P1-T03 / UoW state machine:** Created → ActiveTransaction → Committed/RolledBack → Disposed; explicit Begin/Commit/Rollback behavior, double-call guards, rollback on disposal, no ambient context; test state transitions.
- [x] **P1-T04 / Repository registration:** explicit mapping interface→concrete, scoped repository creation using UoW connection and current transaction; no arbitrary assembly search or static transaction provider.
- [x] **P1-T05 / RepoDb/Dapper:** one sample test-only entity/table exercises RepoDb Write and Dapper Read with same explicit transaction, with mapping init thread-safe on 1.16.x. Do not introduce application master-data entities prematurely.
- [ ] **P1-T06 / DbUp:** dedicated controlled migrator command/library; create an immutable **test-foundation** script and history table; clean migration + repeat as no-op; no migration from client login.
- [ ] **P1-T07 / SQL tests:** cross-repository atomic commit; rollback after second failure; dispose uncommitted; Dapper sees changes in same transaction; parallel distinct scopes; safe resource cleanup; no accidental independent connection.
- [ ] **P1-T08 / Compatibility:** run tests under all feasible TFMs on Windows; document package API differences (RepoDb 1.16.0/SqlServer 1.16.1/Bulk 1.16.2) and deferred bulk operations if unused.

## Exit evidence

SQL tests pass on a disposable SQL Server database and can be repeated. UoW and repository APIs have no dependence on HTTP, UI or AsyncLocal. DbUp repeated application produces no additional side effects. If SQL unavailable, P1 is **blocked**, not complete.

## Avoid

Nested/suppressed ambient scopes, cross-connection transaction sharing, automatic migrations on every desktop startup, production data deletion and UI feature work.
