# Development Plan — MyDmsVn v1 → v2

**Status:** P0 complete on 2026-10-08; P1 is next. **Codex must not skip prerequisites or mark unverified tests as passed.**

## Operating rules

- Execute P0 → P7 sequentially; P0–P6 produce Local Mode MVP; P7 enables Remote Mode.
- For each task, implement a small vertical change with tests, record results, and update checklist truthfully.
- Exact targets/packages/user decisions: `docs/TECH_STACK.md`. Architecture and boundaries: `docs/ARCHITECTURE.md`.
- Each phase exits only with measurable acceptance evidence. For user-visible scope changes, update PRD and record an ADR.

## Roadmap

| Phase | Goal | Required output / exit gate | Status |
|---|---|---|---|
| **P0** | Repository and solution foundation | Dual-target solution, DI/smoke hosts, reference boundaries, test projects; both Windows hosts build | Complete (2026-10-08) |
| **P1** | Persistence foundation | SQL connection, RepoDb maps, Dapper, DbUp migrator, explicit UoW; SQL commit/rollback tests | Not started |
| **P2** | Application pipeline/contracts | MediatR, validation, ErrorOr, transport-neutral ApiResponse mapping, client APIs; error tests | Not started |
| **P3** | Security foundation | Users/Roles/Permissions, auth, legacy hash migration abstractions, authorization behavior/tests | Not started |
| **P4** | Desktop foundation | WinForms shell, ViewModel binding, local client adapters, lookups/grid and error/busy states; STA UI smoke | Not started |
| **P5** | Sales & Inventory Foundation | 4 catalogs, goods receipts, posted ledger, balances and stock card; concurrency tests and UI | Not started |
| **P6** | Local v1 hardening | Deployment/migration instructions, logging, secure configuration, two desktop host smoke, release checklist | Not started |
| **P7** | HTTP API v2 & parity | ASP.NET Core 8 API + HTTP adapter, auth and contract parity; no shared DB credentials on remote desktop | Not started |

## P0 — Detailed tasks

See dedicated [P0 implementation plan](plans/20261008-002-phase-0-foundation.md).
- [x] P0-T01 scaffold `MyDmsVn.sln` and approved multi-target projects.
- [x] P0-T02 create central package props and compatibility-tested references.
- [x] P0-T03 implement empty DI composition roots and startup smoke.
- [x] P0-T04 create test harness/architecture checks; prove dual-target build.
- [x] P0-T05 document validated restore/build results and open constraints.

## P1 — Persistence foundation

- [x] P1-T01 select disposable SQL test database naming/configuration and implement safe test provisioning.
- [x] P1-T02 create `IDbConnectionFactory` returning fresh open connection and SQL Server implementation.
- [x] P1-T03 implement Explicit UoW state machine + repository creation and deterministic disposal.
- [x] P1-T04 establish RepoDb 1.16.x mappings and Dapper query executor sharing explicit UoW.
- [x] P1-T05 DbUp migrator skeleton and migration-history verification; test rerun/no-op.
- [ ] P1-T06 SQL integration tests for nested guard, commit, rollback, disposal, cancellation, concurrent separate UoWs.
**Exit:** P1 tests in `docs/TEST_STRATEGY.md` green on SQL Server; no static AsyncLocal transaction.

## P2 — Application pipeline + contracts

- [ ] P2-T01 define transport-neutral `ApiResponse<T>` and common error types.
- [ ] P2-T02 wire MediatR registration/pipeline order, async FluentValidation + typed ErrorOr results.
- [ ] P2-T03 define status/error/field naming mapper without ASP.NET references.
- [ ] P2-T04 define DTOs + `IxxxApiClient`, local adapter seam and basic test handler.
- [ ] P2-T05 golden JSON/semantic fixture tests; ensure none/both success-error disallowed.
**Exit:** no dynamic conversion, no HTTP dependency in Application, robust validation tests.

## P3 — Security foundation

- [ ] P3-T01 DbUp Users/Roles/UserRoles/RolePermissions/UserPermissions + uniqueness/indexes.
- [ ] P3-T02 `IPasswordHasher` and pluggable legacy password verifier (do not guess legacy algorithm).
- [ ] P3-T03 login/current-user abstractions; fail-closed authorization behavior.
- [ ] P3-T04 direct permission precedence and role union, optional cache invalidation if introduced.
- [ ] P3-T05 tests for deactivation, overrides, privilege denial, password migration fixtures.
**Exit:** unauthorized commands cannot write; compatibility preserved for PasswordSalt.

## P4 — WinForms desktop foundation

- [ ] P4-T01 create shell, bootstrap theme/grid integration validation for both runtimes.
- [ ] P4-T02 standard bindable VM, async commands, cancellation, busy/error/field messages.
- [ ] P4-T03 implement LocalApiClient DI wiring and session information.
- [ ] P4-T04 generic master list + edit form + reusable lookup/grid pattern.
- [ ] P4-T05 deterministic STA UI test harness without modal dialogs.
**Exit:** two WinForms hosts open same shell and use same desktop abstractions; no direct server infrastructure references from views.

## P5 — Sales & Inventory Foundation

See dedicated [P5 detailed plan](plans/20261008-001-sales-inventory-foundation.md).
- [ ] P5.1 Products/Warehouses/Employees/Customers + lookups, guards and UI.
- [ ] P5.2 GoodsReceipt Draft header/lines with validation and permissions.
- [ ] P5.3 Post command with atomic ledger/balance updates, idempotency and reconciliation.
- [ ] P5.4 Stock Balance/Card Dapper queries, desktop UI, concurrency/GUI regression suite.
**Exit:** all P5 acceptance cases and SQL integration tests pass.

## P6 — v1 hardening

- [ ] P6-T01 documented migration and client/schema version compatibility.
- [ ] P6-T02 installer/deployment paths, configuration and safe updater decision.
- [ ] P6-T03 structured logs, correlation IDs, authentication/audit log redaction.
- [ ] P6-T04 dual-target smoke/install/upgrade, backup/recovery exercise.
- [ ] P6-T05 release checklist and known limitations (including 2-tier SQL security).
**Exit:** repeatable deployment, support diagnostics, no untested hidden modal-dialog blocker.

## P7 — HTTP v2

- [ ] P7-T01 implement `Server.Api` host, routes, auth middleware, exception/error mapping.
- [ ] P7-T02 implement `Desktop.Infrastructure.Http` and session/token lifecycle.
- [ ] P7-T03 map local feature interfaces to HTTP and OpenAPI contracts.
- [ ] P7-T04 contract parity tests on JSON, status codes, validations, permissions and ledger posting.
- [ ] P7-T05 verify remote desktop artifacts have **no SQL connection strings** or server infra dependencies.
**Exit:** feature-complete adapter parity for P5 and independently enforceable Server.Api boundary.

## Work item completion template

For each task record: scope, files added/modified, architectural decision (if any), automated test command + result, environment-dependent checks, remaining risks, follow-up. Status must be a verifiable fact, not an aspiration.
