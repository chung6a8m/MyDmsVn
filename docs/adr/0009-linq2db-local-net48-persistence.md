# ADR 0009 — LINQ to DB persistence, Local v1, active net48

**Date:** 2026-10-18
**Status:** Owner-approved direction (1A + 2A + 3A); proposed implementation design, NOT IMPLEMENTED.
**Implementation order:** [RESP](../plans/20261018-002-respawn-fixture-reuse.md), then [L2DB](../plans/20261018-001-linq2db-net48-persistence-migration.md); these are the only active plans.
**Original assessment:** `main` at `8cfa25351338666b1a751ee516d5a479cdb37b7f` (PR #35 merge).
**Documentation refresh:** `v1a` at `c7ad32a4b0040f87417973eabd6c12757c54f7d5`, 2026-10-18; no implementation or runtime verification in this refresh.

## Context and authority

The owner accepted Local v1 followed by HTTP v2, LINQ to DB with DbUp, and retention of the repository's runtime baseline. The owner explicitly clarified that 3A keeps `net48` as in the repository. This means the accepted NET8-PAUSE profile, not restoration of the earlier dual-target suggestion.

At the current documentation base, application, SQL test and Desktop projects actively target `net48`; the SQL runner and its tests execute on tool-only `net8.0`, and the shared SqlTestHarness library targets `netstandard2.0`. AppCore and historical modern product cases stay parked. The permission rebuild and existing runner safeguards are implemented. Remaining P5 interactive/P6 operational acceptance, HTTP v2/P7 and modern product runtime are deferred backlog, without a completion claim or waiver. See [HISTORY](../HISTORY.md) for historical evidence and [DEVELOPMENT_PLAN](../DEVELOPMENT_PLAN.md) for status.

This decision narrowly supersedes the RepoDb-command/Dapper-query library requirement in existing guidance when executing the reviewed L2DB plan. It does not supersede ADR 0001 transport boundaries, ADR 0002 UoW ownership, ADR 0003 password compatibility, ADR 0004 posting invariants, ADR 0005 isolation, ADR 0007 permissions, or ADR 0008's runtime pause. Until cutover is verified, current RepoDb/Dapper code remains the delivered implementation; adding this document is not evidence of migration.

## Decision and scope

Keep WinForms, CommunityToolkit.Mvvm, Microsoft DI, MediatR, FluentValidation, ErrorOr, Contracts, SQL Server, Microsoft.Data.SqlClient, Serilog and DbUp. Use LINQ to DB as the target application DAL behind existing repository/query interfaces. Keep a small, explicitly documented native SqlClient surface for locking SQL, multi-result operations and security/maintenance procedures where a rewrite would increase risk or change semantics.

The intended end state has no active product dependency on RepoDb or Dapper, no second authorization implementation and no runtime ORM-selection flag. Transitional implementations exist only during the reviewed migration; pilot-only comparison adapters are deleted before acceptance. Independent test setup/assertions may use native parameterized SqlClient instead of querying through the same new DAL they test. Git retains historical documents and evidence; current guidance may remove superseded roadmap/policy pages. Parked package records and logical-case identities are not executable legacy product paths, and this docs refresh does not delete or reactivate them.

`linq2db` **6.3.0** is the initial, explicit compatibility candidate from the preceding assessment, not a claim that it is the newest release. Its published package includes `net462`, `netstandard2.0` and `net8.0` assets. T01 must prove the actual `net48` restore/build/runtime combination with the repository's pinned SqlClient and transitive packages. No floating version, broad dependency upgrade, `linq2db.SqlServer` scaffolding bundle, EF Core, Identity framework replacement or T4 generation is required. A candidate failure blocks rollout; it does not authorize reactivation of .NET 8 or upgrade to .NET 10.

## Runtime and transport contract

- Product, SQL and Desktop execution remains `net48`. Tool-only runner/test `net8.0` is preserved as tooling, never counted as product coverage.
- Do not add AppCore to the solution/release, execute parked targets or discard their historical identities. Historical duration waivers remain unmeasured observations, not evidence of a speedup.
- Keep Local in-process dispatch and the existing API DTO/result semantics. No HTTP host, service installation, token redesign or UI replacement belongs to this migration.
- Deferred HTTP v2/P7 still requires a separately approved implementation/runtime decision. Keeping Local does not remove its two-tier trust limitation or make client-held SQL credentials tamper-proof.

## Persistence and ownership design

Application and Domain retain the existing `IUnitOfWorkFactory`, `IUnitOfWork` and business repository/query interfaces. `Repository<TRepository>()` remains a typed, explicitly registered UoW-scoped resolver, not general service resolution. No `DataConnection`, LINQ to DB type, SQL connection or `IQueryable` crosses into Application, Domain, Contracts, Views or ViewModels.

The existing UoW remains the sole owner of its already-open connection and optional transaction. The proposed Infrastructure-only session factory has two distinct operations:

```csharp
DataConnection Borrow(ISqlExecutionContext context, bool requireTransaction);
Task<DataConnection> OpenOwnedAsync(CancellationToken cancellationToken);
```

`Borrow` first obtains the guarded current connection and current transaction from the context; it never caches a transaction at repository construction. A repository resolved before `BeginTransaction` must still participate in the transaction begun later. A borrowed wrapper owns neither connection nor transaction: disposal, query failure and cancellation must not commit, roll back, close or dispose them. The UoW's existing state machine and cleanup remain authoritative. A completed/disposed UoW must reject new borrowed operations.

`OpenOwnedAsync` uses the existing connection factory for a standalone query, transfers ownership explicitly to the wrapper and disposes the connection if wrapper construction fails. Neither method falls back to a newly opened connection when a borrowed provider/type/state is invalid. Do not use `TransactionScope`, static/AsyncLocal transaction state, per-operation connection-string constructors for transactional work or concurrent commands on one UoW.

Use an explicitly composed mapping schema, built successfully before publication and then reused read-only. Mappings live in Infrastructure, including mappings of existing Domain POCOs; ORM attributes must not leak into inner layers. Map schema/table, PK/identity, column names, nullability, Unicode lengths, numeric precision/scale, SQL-generated fields and receipt `rowversion` explicitly. Preserve operation-specific insert/update allowlists; an unrestricted entity update is not an acceptable replacement.

The factory method shapes are proposed internal contracts, not existing APIs. T01 binds them to the exact pinned LINQ to DB API and tests its external-resource disposal behavior before business cutover.

## Observable behavior that cannot change

**Catalog:** preserve all four catalogs, audit-field write restrictions, identity values, employee-user linkage errors, active-only lookups, trim/blank search normalization and literal substring matching. Existing Product list uses `CHARINDEX`, `COUNT_BIG`, a long offset and `Code, ProductId` ordering. Do not introduce SQL wildcard semantics, an Int32 total/offset truncation, client-side filtering/paging or an unreviewed round-trip increase. Do not invent a catalog `rowversion` requirement where the current schema/contracts do not have one.

**Inventory:** preserve receipt-number generation, header/line IDs, decimal quantities/costs, byte-array rowversions, conditional Draft-to-Posted update, append-only ledger source uniqueness, deterministic lock order and atomic balance increments. Preserve reconciliation's explicit SERIALIZABLE transaction and lock ordering. Keep the proven SQL for these operations rather than replacing it with generic update/MERGE. Bulk copy is not enabled just because LINQ to DB supports it: only an inventoried existing need can justify it, with identity, constraint, cancellation and shared-transaction tests.

**Security:** retain ADR 0007's current-subject validation, Active-key validation, protected singleton superuser, direct-user decision precedence, active-role grants and default deny. Keep next-protected-request effectiveness, additive import, revision/digest revalidation, 1 MiB UTF-8/5,000-entry limits, protected-account maintenance boundaries, legacy password columns and atomic audit. No generic ORM DML on restricted identity/RBAC tables; keep the allowlisted procedures and actual least-privileged principal tests. Preserve SQL error-to-application-result mapping, including procedure-specific conflict/forbidden codes, without exposing SQL or secret payloads.

**Schema and data:** DbUp remains the only schema migration owner. This is a DAL replacement on the supported post-SEC-REBUILD schema, not another security reset. No production migration-script rewrite, create/drop/seed, schema/history change or database reset is authorized. Existing data and assignments remain intact. Necessary schema changes require a separate reviewed scope change.

## Pilot, rollout and stop rules

Before L2DB-T00, require accepted RESP net48 evidence using the current RepoDb/Dapper product. Record the accepted fixture/reset boundaries, fresh-database exceptions, source/case identities, runtime matrix and cleanup outcomes. Freeze fixture behavior and runner protocol during DAL migration; a required harness repair must be accepted separately before dependent DAL gates resume.

Before module-wide conversion, prove three representative slices on approved disposable SQL under that accepted harness: Product CRUD/query, a receipt transaction combining LINQ writes with existing native posting operations and rollback, and security procedure/result mapping under runtime privileges. Preserve the existing providers in production composition while pilot-only adapters are assessed; never shadow-write a real user operation through both providers.

Proceed only with correct results, ownership/cleanup, supported runtime packaging, stable error contracts and no unexplained increase in command/round-trip counts. Record code complexity and query characteristics. A faster full SQL suite is not a prerequisite or a promised outcome: ORM replacement does not remove database creation/migration cost. Keep DbUp, observable test isolation, accepted RESP fixture boundaries/reset semantics/fresh-database exceptions and current serialization. Preserve executable runner/manifest/approval/lease/watchdog checks as accepted after RESP. Another fixture redesign, parallelism change, fingerprint-v2 migration, Docker or new CI jobs are outside L2DB. RESP remains test-only; it never resets an application database.

After pilot acceptance, convert Catalog, then Inventory, then Identity/Security and remaining operational consumers. Remove transitional providers/mappings/packages and update current documentation only after the corresponding code changes exist. One planned final SQL/package/smoke milestone follows the frozen implementation. [TEST_POLICY](../TEST_POLICY.md) requires focused task tests and broader SQL for shared UoW/harness/migration changes, without automatically adding a full repository or GUI run at every code-PR boundary. Package or source changes after a green gate invalidate the affected evidence. Multi-pass benchmarks are optional separate measurement work, not correctness prerequisites.

Rollback means reverting the DAL code/package changes or redeploying the prior **post-SEC-REBUILD** binary against its compatible unchanged schema, after compatibility verification. It never means reviving the pre-rebuild permission subsystem, deleting data or enabling a permanent provider fallback.

## Acceptance and consequences

Use focused affected tests per task and broader SQL where shared persistence contracts change, then the plan-specific metadata preflight and strict manifest-owned Final across the active matrix. T07 requires net48 package verification and a documented packaged Local smoke covering login, catalog, receipt/posting and security behavior. Automated guarded GUI D2 is conditional on changed UI behavior or a smoke regression requiring it; unexecuted D2 is NOT RUN with rationale, never PASS. Preserve SQL approval, workspace/global resource leases, path budgets, watchdogs, independent source/discovery/TRX, zero mandatory skips and positive owned-database cleanup. Historical totals are not target counts or fresh candidate evidence.

Expected benefit: fewer application DAL conventions and more refactorable typed queries while preserving explicit SQL control. Costs: LINQ translation/mapping expertise, provider binding/disposal proof, a controlled migration and refreshed test fingerprints. The decision does not solve the Local trust boundary or prove a runtime/test speedup.

Affected current guidance at code cutover: AGENTS, ARCHITECTURE, TECH_STACK, PERSISTENCE, PRD, DEVELOPMENT_PLAN, TEST_POLICY and security/operations/package guidance where actually changed. This documentation refresh distinguishes delivered RepoDb/Dapper code from the proposed L2DB target now; a later code cutover updates delivered-state statements only after verification. Historical reports remain available through Git/HISTORY and are never rewritten as if LINQ to DB produced their results.

## Sources checked for this design

Current repository observations are pinned to the documentation-refresh SHA: project/runtime/package declarations, `SqlUnitOfWorkFactory.cs`, Catalog/Inventory repositories and queries, `SqlSecurityAdministrationStore.cs`, Application persistence/contract interfaces, Foundation tests and the checked-in test wrappers/manifest. The L2DB plan lists their exact source paths. [HISTORY](../HISTORY.md) retains links to earlier permission and runner acceptance at their original revisions; that evidence is context, not acceptance for RESP/L2DB.

External primary references, checked 2026-10-18 (capability documentation, not runtime-test evidence):
- [linq2db 6.3.0 package assets](https://www.nuget.org/packages/linq2db/6.3.0).
- [Official project documentation](https://linq2db.github.io/).
- [ConnectionOptions ownership contract](https://linq2db.github.io/api/linq2db/linq2db--LinqToDB.Data.ConnectionOptions.html).
- [Bulk-copy modes and provider-specific caveats](https://linq2db.github.io/articles/sql/Bulk-Copy.html).
