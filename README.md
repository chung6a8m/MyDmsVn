# MyDmsVn

Windows desktop-first SME business software, beginning with Sales & Inventory Foundation. Local v1 invokes Application handlers in process through the Local adapter.

## Current roadmap — v1a-pro

**Updated:** 2026-10-22. **SAL — Direct Sales with moving weighted average** is the sole ACTIVE roadmap, based on merged PR #41 at `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. T00 foundations and the [reviewed T01 schema/bootstrap checkpoint](docs/evidence/sal/20261022-t01-schema-bootstrap.md) are complete; [T02 valued inventory](docs/evidence/sal/20261022-t02-valued-inventory.md) is complete and [T03 Draft/detail](docs/evidence/sal/20261022-t03-sales-draft.md) is complete; T04 is IN PROGRESS per the [execution index](docs/plans/README.md). Full SAL acceptance remains incomplete.

The approved flow is a direct Draft → Posted sale with manual prices, duplicate Product rows, gifts and stock/cost tracking. The grid has seven columns; the list adds Product filtering and SaleId DESC keyset navigation using LINQ to DB. Cost is moving weighted average per warehouse/product at actual Post. Payments, debt and returns are excluded.

[Execution index](docs/plans/README.md) · [SAL plan](docs/plans/20261021-002-direct-sales-and-moving-average-costing.md) · [Business design](docs/DIRECT_SALES.md) · [Costing](docs/DIRECT_SALES_COSTING.md) · [Query/UI](docs/DIRECT_SALES_QUERY_AND_UI.md) · [Acceptance](docs/DIRECT_SALES_ACCEPTANCE.md)

BCF-T00–T06 remain COMPLETE / OWNER ACCEPTED in the [final bounded scope](docs/evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21). Preserve exact candidate evidence, visual differences, six wide125 NOT TESTED, deferred T04 Minor and native100 composite limitation. Production release prerequisites remain UNACCEPTED / UNSCHEDULED.

## Current implementation

- Product, WinForms and all ten active test projects target `net48`. Desktop and separate admin migrator publish as Windows x86 directories. AppCore/net8 remains PARKED / NOT SHIPPED.
- Clean Architecture, MediatR, FluentValidation, ErrorOr, explicit UnitOfWork, LINQ to DB plus typed native SqlClient, SQL Server and DbUp. BCF changes no provider/runtime/API/schema contract.
- Accepted test-only Respawn 4 reusable owned fixtures, deterministic reset/seed, dedicated fresh exceptions and direct `dotnet test`/Test Explorer. The old SQL harness, runner, gate manifest and approval-file flow are removed.
- WinForms uses CommunityToolkit.Mvvm, Bootstrap5WinFormUI and BootstrapSourceGrid. Four catalogs, Goods Receipt Draft/Post, stock ledger/balances/card, code-defined permissions and one configured superuser exist.
- PR #40 delivers Business Compact and real inline Product/Quantity/Unit-cost editors through the app-owned receipt grid adapter. BCI is [complete in its signed UI scope](docs/BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21); its evidence remains historical. PR #41 merges the BCF list/header lookup repairs and compact presentation refinement with their own accepted evidence.
- RESP, L2DB and GUI-P5 are completed baselines. Preserve their exact revisions and accepted exceptions, including GUI-P5's Warehouse 125% Dark Down NOT TESTED result; no old result transfers automatically to SAL.
- Historical accepted release schema metadata reflects schema9/scripts001–009; T01 schema10 development source has no new package acceptance. Production installation, recovery, support and release sign-off remain [unaccepted/unscheduled prerequisites](docs/DEFERRED_WORK.md#release-prerequisites).

## Working with Codex

Read [AGENTS.md](AGENTS.md), [AI_CONTEXT.md](AI_CONTEXT.md), the [active plan index](docs/plans/README.md), the SAL design/costing/query/acceptance documents, and the sole [TEST_POLICY.md](docs/TEST_POLICY.md). Use focused tests during implementation; SAL-T08 requires a coherent frozen inventory/sales/schema candidate with new SQL, Windows/UI and package evidence. Development source requires schema10 at T01 commit `81e8aab`; the historical shipped baseline remains schema9. Receipt/reconciliation now use the shared valuation protocol; Sales Post and coherent runtime deployment remain incomplete, so this checkpoint is not deployable. Documentation-only changes require document checks, not a new SQL/GUI run.

Completed plans and evidence remain historical references. [Retired/out-of-scope directions](docs/DEFERRED_WORK.md) are not a second execution queue; [History](docs/HISTORY.md) records their provenance.

## Documentation

[Docs index](docs/README.md) · [Development status](docs/DEVELOPMENT_PLAN.md) · [Architecture](docs/ARCHITECTURE.md) · [Persistence](docs/PERSISTENCE.md) · [Security](docs/SECURITY.md) · [Test policy](docs/TEST_POLICY.md) · [Release packaging](docs/LOCAL_RELEASE_PACKAGING.md)
