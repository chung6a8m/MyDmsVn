# Architecture — MyDmsVn

**Inspected architecture baseline:** Local v1 at `v1a-pro-ui-next@7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` (merged PR #43), 2026-10-23. The [execution index](plans/README.md) selects current authorized work; [UI-NEXT](ui-next/README.md) retains later design/implementation evidence. The A0 routing update does not re-audit every technical statement below. The [archive boundary](archive/20261023-pre-ui-next/README.md) retains source contracts and candidate acceptance limits.

## Implemented Sales and active UI direction

[ADR 0011](adr/0011-direct-sales-and-moving-average-costing.md) and [Direct Sales](DIRECT_SALES.md) describe the implemented Sales module, transport-neutral cursor contracts, independent Read/Write/Post keys and schema10/value inventory. Receipt Post, Sales Post and reconciliation share the transaction-owned protocol, with sorted stock locks, immutable costs and paired quantity/value effects. The [costing](DIRECT_SALES_COSTING.md) and [query/UI](DIRECT_SALES_QUERY_AND_UI.md) specifications retain exact rules. Sales runs within the existing projects, Local composition root, net48 runtime and providers.

R3/R4 source fixes are reviewed/approved at `f5039cb`. [T08 evidence](evidence/sal/20261021-t08-integrated-acceptance.md) distinguishes product/package `f5039cb` from GUI test `4697744`; candidate-specific core/SQL/package/native100 runnable automation passed. Native125, attended observations and dated Owner acceptance remain OPEN in the archive. Earlier BCF/BCI/GUI-P5/L2DB/RESP acceptance remains bounded by its original signed scope; [production release prerequisites](DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED.

UI-NEXT's [design and control policy](archive/20261025-pre-next-roadmap/ui-next/20261023-design-and-control-policy.md) assigns generic rendering, native control semantics, theme behavior and lifecycle to shared UI. MyDmsVn owns workspace/session/document identity, feedback and validation routing, dirty-close admission, operation cancellation and recovery policy. Historical proposal names require source verification before use; later UI-NEXT implementation/evidence remains linked from its reference index. No WinForms object enters Desktop.Application or public transport DTOs; no new provider/runtime/HTTP host or business workflow is implied.

## 1. Current system and approved direction

MyDmsVn is a Windows desktop modular monolith organized into Identity, Catalog, Inventory and Sales with Clean Architecture boundaries. The current product runs on `net48`: WinForms calls Desktop.Application API abstractions, the Local adapter dispatches in-process through Server.Application, and Infrastructure accesses SQL Server.

The [frozen pre-UI-NEXT index](archive/20261023-pre-ui-next/plans/README.md) records archived BCF and BCI alongside [RESP](archive/20261023-pre-ui-next/plans/20261018-002-respawn-fixture-reuse.md), [L2DB](archive/20261023-pre-ui-next/plans/20261018-001-linq2db-net48-persistence-migration.md) and [GUI-P5](archive/20261023-pre-ui-next/plans/20261019-001-full-winforms-gui-p5-acceptance.md) history. Current product persistence uses LINQ to DB for Catalog/Inventory/Sales and typed native SqlClient for Identity/Security/operations; DbUp remains the schema owner. [T07 correctness/package/limited authenticated Local smoke](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) is accepted in its original scope; GUI-P5 supplies later, separately scoped GUI evidence. Neither acceptance establishes production release readiness. Current Local direct-SQL trust and runtime boundaries are unchanged.

HTTP v2 and modern product runtimes remain [out of scope](DEFERRED_WORK.md#out-of-scope-directions). The future HTTP design preserves the same ViewModels, feature API interfaces, DTOs and Application behavior, with an HTTP adapter and a separately trusted API host. It requires a separate runtime decision before implementation; UI-NEXT does not create an HTTP host or restore a modern product target.

```mermaid
flowchart TD
    UI["WinForms Views and ViewModels"] --> DA["Desktop.Application APIs"]
    DA --> LOCAL["Local adapter"]
    LOCAL --> APP["Server.Application"]
    APP --> REPO["Business repository/query interfaces"]
    REPO --> INFRA["Server.Infrastructure implementations"]
    INFRA --> SQL["SQL Server"]
    MIG["Controlled DbUp migrator"] --> SQL
```

The diagram shows the current runtime call path; the host binds interfaces to implementations. Compile-time dependency rules are specified below. Local executes server logic inside the desktop process and is a two-tier deployment. Someone controlling the desktop process or runtime SQL credentials can bypass Application checks. Restricted SQL permissions reduce exposure, but a separately enforceable hostile-client boundary belongs to the deferred HTTP host. Keep maintenance credentials out of Desktop configuration.

## 2. Dependency rules

| Component | Allowed dependencies and responsibility |
|---|---|
| Server.Domain | Shared primitives; no Application, Infrastructure, HTTP or WinForms dependency. |
| Server.Application | Domain and justified Contracts DTOs; handlers, validation, authorization and inner repository/query interfaces. |
| Server.Infrastructure | Application/Domain interfaces; SQL providers, explicit mappings, persistence, identity and operational adapters. |
| Desktop.Application | Contracts; feature API interfaces and ViewModels. |
| Desktop.WinForms | Desktop.Application and presentation controls; no direct SQL or Server.Infrastructure reference. |
| Desktop.Infrastructure.Local | Desktop abstractions and Server.Application dispatch; Infrastructure is wired at the host composition root. |
| Desktop.App | Current net48 host; composes UI, Local adapter, Application and SQL Infrastructure. |
| Future HTTP components | The HTTP adapter depends on Desktop.Application/Contracts; the API host composes Application/Infrastructure. These components are deferred. |

Public Contracts do not expose Domain entities, provider types, ADO.NET connections, transactions, MediatR requests, ErrorOr objects or UI classes. Application handlers do not return HTTP framework results. LINQ to DB types, mapping attributes and `IQueryable` remain outside inner layers and public contracts.

### Repository resolution and query ownership

`IUnitOfWork.Repository<TRepository>()` is the established typed repository resolver. `IUnitOfWork`, its factory and business repository interfaces live in `Server.Application/Persistence` or the corresponding Application feature. Concrete repository factories and `ISqlExecutionContext` live in Infrastructure.

- Handlers resolve business repository interfaces from an explicitly owned UoW. They do not resolve concrete SQL repositories or arbitrary services.
- Interface-to-implementation registration is explicit. Missing registrations fail clearly; arbitrary first-type assembly scanning is prohibited.
- All participating repositories share the UoW-owned open connection and its current explicit transaction, including asynchronous operations.
- Standalone read services return materialized DTOs and may own a separate connection. A read participating in a command transaction uses that command's connection and transaction.
- Repository lifetime ends with the UoW. Preserve caller-scoped dependencies and the existing transaction/state protections; do not share a UoW across concurrent commands.

See [PERSISTENCE](PERSISTENCE.md) and [ADR 0002](adr/0002-explicit-unit-of-work.md). L2DB changes provider adapters while retaining this ownership model.

## 3. Modules and request handling

Identity owns account validation, code-defined permission evaluation and security administration. Catalog owns Products, Warehouses, Employees and Customers. Inventory owns goods receipt draft/posting, quantity/value ledger, stock inquiries and reconciliation. Sales owns direct sales Draft/Post and keyset queries, sharing inventory valuation and transaction coordination. Commands, queries, validators, handlers and DTO mapping remain near their feature. Business expansion beyond those implemented contracts needs a separately approved scope.

A request enters a Local adapter, receives an immutable request identity, passes through authentication/authorization and FluentValidation, and reaches an Application handler returning `ErrorOr<T>`. A transport-neutral mapper produces `ApiResponse<T>`. Preserve the current pipeline ordering, deterministic error mapping, field paths, cancellation and safe diagnostics. A future HTTP adapter must preserve the same behavior with meaningful HTTP statuses. See [CONTRACTS](CONTRACTS.md).

## 4. Transactions and inventory invariants

Each explicit UoW owns one connection and at most one transaction. Participating database commands receive that transaction explicitly; ambient/static/AsyncLocal transactions and hidden fallback connections are prohibited. Preserve legal state transitions and cleanup on exceptions/cancellation.

Each receipt/sale Post use case owns its transaction boundary. Document status/rowversion, immutable sale costs, quantity/value ledger pairs and balances succeed or roll back together. Preserve the transaction-owned Shared writer/Exclusive reconciliation coordinator, sorted stock-key locks, append-only semantics, exact allocation and consistent reconciliation reads. UI changes do not authorize a new posting algorithm or automatic mutation replay. See [PERSISTENCE](PERSISTENCE.md), [DATA_MODEL](DATA_MODEL.md) and [ADR 0004](adr/0004-p5-ledger-and-posting.md).

## 5. Security

The current security model is implemented. Require a trusted authenticated active subject and a registered Active permission key before loading the current SQL account/binding snapshot; then require that account to remain active. A valid configured and database-bound superuser match bypasses ordinary grants; otherwise a direct-user Allow/Deny wins, followed by active-role grants, with default deny. Unknown/deprecated keys are denied for every account. Session expiry, current account validation and business validation still apply to the superuser.

Code owns the immutable permission catalog; operators own assignments. Application upgrades do not synchronize permissions or auto-grant roles. The protected singleton binding is changed only through separate maintenance authority. Normal runtime SQL uses the allowlisted procedures and cannot perform generic restricted RBAC/binding writes. Retain legacy PasswordHash/PasswordSalt compatibility and atomic audit. See [SECURITY](SECURITY.md) and [ADR 0007](adr/0007-code-defined-permissions-and-single-superuser.md).

## 6. Desktop, runtime and startup

The shared WinForms library and Desktop.App actively target `net48`. Desktop.AppCore still declares `net8.0-windows` in parked source and is excluded from the active solution/release. All eleven active test projects target net48 and run directly through `dotnet test`/Test Explorer. The former standalone SQL framework is removed; the .NET 8 SDK remains a build dependency. See [TECH_STACK](TECH_STACK.md) and [PROJECT_STRUCTURE](PROJECT_STRUCTURE.md).

Use thin WinForms views and CommunityToolkit.Mvvm ViewModels, asynchronous feature APIs, deterministic busy/error state and session-aware late-result guards. Desktop.WinForms references the pinned BootstrapSourceGrid project; check its actual source/API and target compatibility before changing presentation dependencies. BCI implemented the app-owned presentation adapter and inline edit lifecycle described in the [accepted design](BUSINESS_COMPACT_UI.md). The [BCF design](BUSINESS_COMPACT_REFINEMENT.md) records implemented lookup binding and presentation refinements while preserving those boundaries; source and final candidate evidence distinguish implemented APIs from historical proposals. Tests creating controls keep STA execution, deterministic disposal and serialized global UI interaction.

Desktop.App configures Microsoft DI once. Local mode requires runtime database configuration and registers Application, SQL Infrastructure and Local dispatch. DbUp runs as a controlled migrator/deployment command, never automatically from client login. Startup validates supported schema and runtime authority without granting privileges or seeding data. A future Remote composition must exclude SQL credentials and server Infrastructure from its client dependency graph.

## 7. Validation and change control

[Test policy](TEST_POLICY.md) owns execution scope. Use focused affected tests at task boundaries; broaden SQL coverage for shared fixture/UoW/migration changes and GUI coverage for changed UI behavior. BCI retains its [accepted historical evidence](BUSINESS_COMPACT_ACCEPTANCE.md); BCF retains its [completed bounded acceptance](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md), while verification for separately authorized changes follows TEST_POLICY and any plan explicitly activated through the [execution index](plans/README.md). [UI-NEXT validation](archive/20261025-pre-next-roadmap/ui-next/20261023-validation-and-handoff.md) retains historical acceptance references. [TEST_STRATEGY](TEST_STRATEGY.md) describes required behavior coverage, not another execution queue.

Keep explicit non-production SQL configuration, local allocation ownership/cleanup, finite waits and independent case evidence. SQL projects execute sequentially; interrupted allocations require manual review because no external lease/watchdog/orphan recovery service remains. RESP fixture reuse and the L2DB conversion are completed; preserve their accepted fixture boundaries, reset behavior and direct test commands. Historical acceptance is retained in [HISTORY](HISTORY.md), with unresolved [release prerequisites](DEFERRED_WORK.md#release-prerequisites) outside the active execution queue.

Changes to runtime, transport contracts, authorization, transaction ownership, password migration or ledger semantics require an explicit decision and corresponding ADR. BCF preserved these architectural boundaries. ADR 0011 explicitly approves the SAL contract/valuation additions while retaining connection, layer and security ownership.
