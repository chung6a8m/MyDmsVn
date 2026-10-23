# MyDmsVn — Current context for Codex

**Active base:** `v1a-pro-ui-next` · **Updated:** 2026-10-23 · **Source baseline:** `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` (includes merged PR #43 and the migrated assessment). This handoff distinguishes source facts, historical evidence and proposed work.

## Owner decision and next work

**UI-NEXT is the sole ACTIVE roadmap.** Read the [roadmap](docs/ui-next/20261023-active-roadmap.md), [design and control policy](docs/ui-next/20261023-design-and-control-policy.md), [implementation plan](docs/ui-next/20261023-implementation-plan.md) and [validation and handoff](docs/ui-next/20261023-validation-and-handoff.md). UIN-T00–T02 are VERIFIED in their bounded task scope, with Light/Dark native100/native125 evidence on the current candidate; UIN-T03 is VERIFIED for current Goods/Sales adapters at native100; UIN-T04 is VERIFIED for its pure decimal parser/projection scope; UIN-T05 is VERIFIED for its bounded native decimal control, BSG adapter and Sales/Goods receipt pilots at measured Light/Dark native100/native125; UIN-T06–T13 remain NOT STARTED. Final package/Owner acceptance remains at T13. Scope covers workspace-owned Toast/Alert feedback, field/cell validation, closable tabs and busy presentation, and semantic theme surfaces with less repeated application code.

The [archive boundary](docs/archive/20261023-pre-ui-next/README.md) retires the six previous plan files and their execution index. Old unfinished acceptance, full SAL integration repeats and production release work are excluded from the new queue; they are neither completed nor waived. The [former index](docs/plans/README.md), design documents and evidence remain historical or source-contract references.

## Archived acceptance and implemented source

SAL T00–T06 checkpoints and the T07 Sales workspace are implemented in the merged source. R3/R4 fixes are reviewed/approved at `f5039cb`. [T08 evidence](docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS and original failed trials retained. Native125, attended observations and dated Owner acceptance remain OPEN. The archive does not certify full SAL acceptance or deployment readiness.

[BCF](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [BCI](docs/BUSINESS_COMPACT_ACCEPTANCE.md) are complete only within their signed scopes. Preserve candidate/test/package distinctions, original failures, accepted visual differences, BCF's six strict wide125 NOT TESTED rows, deferred T04 Minor and native100 composite/no-independent-no-write limitation. RESP, L2DB and GUI-P5 remain completed historical baselines, including GUI-P5's Warehouse 125% Dark Down NOT TESTED exception. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED; the deleted REL/P6 and modern-runtime procedures are not restored.

## Current source facts

- Product and all ten active test projects target `net48`; package forms are Windows x86 Desktop.App and separate admin DbMigrator. AppCore/net8.0-windows remains PARKED / NOT SHIPPED.
- WinForms invokes Desktop.Application and Server.Application through the in-process Local adapter. Preserve transport-neutral DTOs, Clean Architecture and explicit UoW repository resolution.
- Catalog/Inventory/Sales persistence uses LINQ to DB; other SQL consumers use typed native SqlClient. One explicit UoW owns the connection/current transaction, and DbUp owns schema scripts. No provider/runtime migration is in UI-NEXT.
- Permissions use code-defined Active keys, current identity checks and a configured database-bound superuser. Unknown/deprecated keys deny before superuser; direct Deny wins for ordinary identities. Preserve password hashing/salt and legacy algorithm handling.
- Receipt Post and Sales Post atomically write quantity/value companions and balances under the common transaction-owned Shared coordinator and sorted stock locks. Reconciliation takes Exclusive and reports source/quantity/value issues without repair. Sales cost freezes at actual Post; duplicate Product rows remain independent and gifts have zero revenue with full quantity/cost.
- Sales has the seven-column editor, manual prices, independently authorized Draft/Get/Post/List APIs, filter-bound SaleId DESC keyset navigation and exact fixed-point previews. Payments, debt, returns and new pricing/tax workflows remain excluded. [Business](docs/DIRECT_SALES.md), [costing](docs/DIRECT_SALES_COSTING.md), [query/UI](docs/DIRECT_SALES_QUERY_AND_UI.md) and [ADR 0011](docs/adr/0011-direct-sales-and-moving-average-costing.md) remain contract references, not active task queues.
- Desktop preflight requires complete scripts001–010. The admin migrator admits Empty/exact10 and separately verified exact9 under pre-existing RESTRICTED_USER, the supported SQL-login identity inventory and visible drain checks; it does not change mode or kill clients. Prefixes001–008 and partial/unknown schemas remain refused. Historical schema9 packages retain their exact evidence. Local direct SQL, trusted maintenance/privileged administrators and inherited collation portability remain explicit limits.

## UI baseline and ownership

| Dependency | Inspected pin |
|---|---|
| MyDmsVn.BootstrapSourceGrid | `2bd7874f7e80b22ef181763749c2a2938ab7f16e` |
| Bootstrap5WinFormUI nested dependency | `2be9a4d778fda7016207b75470020be6126f2f3f` |
| SourceGrid nested dependency | `f4e457b43582bf01892f50bdc74aa480531e5944` |

Existing shared primitives include `BootstrapToastContainer`, `BootstrapAlert`, input `ValidationState` and `BootstrapSpinner`. `BootstrapToastService` resolves a screen for its top-level host; passing a control does not create tab scope. The tab control has no close-request contract, and the grid's loading implementation is not a generic busy host. UI-NEXT's proposed services, close affordances, busy host and semantic surface APIs remain unimplemented until their owning tasks provide source and evidence.

MyDmsVn owns workspace-instance identity and session/document generations, message/validation routing, command admission and dirty/recovery policy. Shared UI owns generic rendering, theme behavior and lifecycle. Keep controls out of Desktop.Application/public DTOs. Do not route late results by whichever tab happens to be selected, use a module key as a replacement for instance identity, or let a busy veil replace authorization/editability checks. Cancellation does not prove a mutation rolled back.

`CatalogControl` remains shared by four catalogs. Goods Receipt and Sales retain their app-owned SourceGrid adapters, stable line identity, pending/committed lookup distinction, strict numeric parsing, raw edit buffers, writable accessibility and session disposal. Ordinary same-identity rebinding must not reselect or overwrite native text/caret/range. Whole-string Text/UIA assignment is not native per-key proof.

Keep Posted/busy/denied/`RequiresPostOutcomeReload` guards authoritative. Resolve pending inline/header edits before Save/Post or normal close decisions; rejected navigation retains raw input. Existing Back/List admission occurs before focus-loss validation, cancels query-only presentation only after admission and preserves the same control/editor/adapter. Permission denial scrubs protected presentation; stale query/lookup/permission callbacks must not restore it. Forced logout/session replacement must still clear/dispose workspaces regardless of ordinary dirty-close policy. No implicit Post retry or automatic unlock after a denied recovery reload is allowed.

## Execution and evidence rules

Read [AGENTS](AGENTS.md), the [active roadmap](docs/ui-next/20261023-active-roadmap.md), task/design/validation documents, [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md) and [tests/README](tests/README.md). UI-NEXT selects focused source-owned Application/Session/WinForms/vendor checks for its changed contracts. Archived SAL-T08 and BCF matrices are not active gates, and no full six-owner fresh/reuse repeat or production-release task is inherited. Documentation-only changes require diff/link/status/encoding checks, not SQL/GUI execution.

Interactive Windows/net48, serialized STA, WinFormsTestGuard, finite waits, bounded GUI-owned subprocesses and verified disposal remain required for GUI evidence. Keep hosted/native100/native125 and packaged/attended results distinct; test-session injection does not prove actual Login. Record actual client/work-area geometry and distinguish captures from physical-display observations.

SQL, when genuinely affected, requires explicitly designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master, sequential owners and positive identity/token checks before cleanup. Preserve reusable fixtures, dedicated fresh exceptions, worker drain and terminal quarantine. Do not restore the removed runner, gate manifest, approval-file or prefix-only drop flow.

Report exact source/test/package revisions, actual discovery/execution, environment, sanitized evidence and unresolved resources. New API names and test selectors remain proposals until source exists and nonempty discovery is verified. Missing, skipped or blocked mandatory cases never establish PASS. Historical totals and acceptance do not transfer to UI-NEXT or imply production readiness.
