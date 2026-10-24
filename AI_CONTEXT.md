# MyDmsVn — Current context for Codex

**Active base:** `v1a-pro-ui-next` · **Updated:** 2026-10-24 · **Inspected product baseline:** `03acacdaea6a750aa88b33d61ea139379edcb326` (merged PR #45). The original UI-NEXT activation at `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639` and correction inspection at `codex/uin-t13@6ff4b9829a4b69f992d75fb52047e991c1843b96` remain historical provenance. This context distinguishes source facts, candidate evidence and proposed work.

## Owner decision and next work

Read the [current execution index](docs/plans/README.md). Its selected plan is the only owner of authorized scope, task state and verification. Cross-repository organization decisions belong to [MyDmsVn.Docs](https://github.com/chung6a8m/MyDmsVn.Docs/blob/main/proposals/2026-10-24-adoption.md); proposals and future phase names do not activate implementation.

[UI-NEXT](docs/ui-next/README.md) is now a historical reference. Its [roadmap](docs/ui-next/20261023-active-roadmap.md), [design/control policy](docs/ui-next/20261023-design-and-control-policy.md), [implementation](docs/ui-next/20261023-implementation-plan.md) and [acceptance](docs/ui-next/20261023-validation-and-handoff.md) retain their candidate-specific results. The [T13 ledger](docs/ui-next/evidence/20261024-uin-t13-integration.md) retains the attended zero-document FAIL after Owner Save, passing no-interaction control and dated bounded acceptance. [First-review](docs/ui-next/evidence/20261024-pr45-review-corrections.md) and [round-2](docs/ui-next/evidence/20261024-pr45-review-round2.md) corrections have separate evidence.

The [DateInput evidence](docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md) records the first candidate's calendar-highlight failure, repaired candidate checks at physical native100/native125 and dated bounded Owner visual acceptance at 125%. The net48 app window reports 96 DPI even on the 120-DPI physical monitor. These records supersede the original NOT STARTED/pending handoff; they do not establish results for another candidate or production release.

The [pre-UI-NEXT archive](docs/archive/20261023-pre-ui-next/README.md) retains the six earlier plans and their frozen index. Unfinished SAL acceptance, full integration repeats and production-release work remain excluded unless separately scheduled; no routing change completes or waives them.

## Archived acceptance and implemented source

SAL T00–T06 checkpoints and the T07 Sales workspace are implemented in the merged source. R3/R4 fixes are reviewed/approved at `f5039cb`. [T08 evidence](docs/evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS and original failed trials retained. Native125, attended observations and dated Owner acceptance remain OPEN. The archive does not certify full SAL acceptance or deployment readiness.

[BCF](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [BCI](docs/BUSINESS_COMPACT_ACCEPTANCE.md) are complete only within their signed scopes. Preserve candidate/test/package distinctions, original failures, accepted visual differences, BCF's six strict wide125 NOT TESTED rows, deferred T04 Minor and native100 composite/no-independent-no-write limitation. RESP, L2DB and GUI-P5 remain completed historical baselines, including GUI-P5's Warehouse 125% Dark Down NOT TESTED exception. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain UNACCEPTED / UNSCHEDULED; the deleted REL/P6 and modern-runtime procedures are not restored.

## Current source facts

- Product and all eleven active test projects target `net48`; package forms are Windows x86 Desktop.App and separate admin DbMigrator. AppCore/net8.0-windows remains PARKED / NOT SHIPPED.
- WinForms invokes Desktop.Application and Server.Application through the in-process Local adapter. Preserve transport-neutral DTOs, Clean Architecture and explicit UoW repository resolution.
- Catalog/Inventory/Sales persistence uses LINQ to DB; other SQL consumers use typed native SqlClient. One explicit UoW owns the connection/current transaction, and DbUp owns schema scripts. Provider/runtime changes require separately selected scope; the current execution index owns scheduling.
- Permissions use code-defined Active keys, current identity checks and a configured database-bound superuser. Unknown/deprecated keys deny before superuser; direct Deny wins for ordinary identities. Preserve password hashing/salt and legacy algorithm handling.
- Receipt Post and Sales Post atomically write quantity/value companions and balances under the common transaction-owned Shared coordinator and sorted stock locks. Reconciliation takes Exclusive and reports source/quantity/value issues without repair. Sales cost freezes at actual Post; duplicate Product rows remain independent and gifts have zero revenue with full quantity/cost.
- Sales has the seven-column editor, manual prices, independently authorized Draft/Get/Post/List APIs, filter-bound SaleId DESC keyset navigation and exact fixed-point previews. Payments, debt, returns and new pricing/tax workflows remain excluded. [Business](docs/DIRECT_SALES.md), [costing](docs/DIRECT_SALES_COSTING.md), [query/UI](docs/DIRECT_SALES_QUERY_AND_UI.md) and [ADR 0011](docs/adr/0011-direct-sales-and-moving-average-costing.md) remain contract references, not active task queues.
- Desktop preflight requires complete scripts001–010. The admin migrator admits Empty/exact10 and separately verified exact9 under pre-existing RESTRICTED_USER, the supported SQL-login identity inventory and visible drain checks; it does not change mode or kill clients. Prefixes001–008 and partial/unknown schemas remain refused. Historical schema9 packages retain their exact evidence. Local direct SQL, trusted maintenance/privileged administrators and inherited collation portability remain explicit limits.

## UI baseline and ownership

| Dependency | Historical correction-inspection pin |
|---|---|
| MyDmsVn.BootstrapSourceGrid | `02888dec75878a081f2caac9b22829ae3a2f7d72` |
| Bootstrap5WinFormUI nested dependency | `c91993f4f44a60210f4c6c4106e82ebc69931a0a` |
| SourceGrid nested dependency | `f4e457b43582bf01892f50bdc74aa480531e5944` |

These are the consumed pins at the historical `6ff4b98` correction inspection, not the pins at the product baseline above and not assertions about upstream HEADs. The selected plan records its own inspected baseline; read gitlinks recursively before a future change and preserve intervening work.

The pinned shared UI includes `BootstrapToastContainer`, `BootstrapAlert`, input `ValidationState`, `BootstrapSpinner`, typed decimal/date input, the request-only tab close affordance, busy host and semantic panel/label controls. Their source and bounded verification belong to the corresponding [UI-NEXT task and acceptance rows](docs/ui-next/20261023-active-roadmap.md). `BootstrapToastService` still resolves a screen for its top-level host; passing a control does not create tab scope. The application supplies workspace-owned feedback and admission policy. DateInput Unified Compact and segment-preserving caret correction are implemented at the exact pins in the [candidate evidence](docs/ui-next/evidence/20261024-dateinput-unified-compact-correction.md); the repaired running visual received dated, bounded Owner acceptance at physical 125%.

MyDmsVn owns workspace-instance identity and session/document generations, message/validation routing, command admission and dirty/recovery policy. Shared UI owns generic rendering, theme behavior and lifecycle. Keep controls out of Desktop.Application/public DTOs. Do not route late results by whichever tab happens to be selected, use a module key as a replacement for instance identity, or let a busy veil replace authorization/editability checks. Cancellation does not prove a mutation rolled back.

`CatalogControl` remains shared by four catalogs. Goods Receipt and Sales retain their app-owned SourceGrid adapters, stable line identity, pending/committed lookup distinction, strict numeric parsing, raw edit buffers, writable accessibility and session disposal. Ordinary same-identity rebinding must not reselect or overwrite native text/caret/range. Whole-string Text/UIA assignment is not native per-key proof.

Keep Posted/busy/denied/`RequiresPostOutcomeReload` guards authoritative. Resolve pending inline/header edits before Save/Post or normal close decisions; rejected navigation retains raw input. Existing Back/List admission occurs before focus-loss validation, cancels query-only presentation only after admission and preserves the same control/editor/adapter. Permission denial scrubs protected presentation; stale query/lookup/permission callbacks must not restore it. Forced logout/session replacement must still clear/dispose workspaces regardless of ordinary dirty-close policy. No implicit Post retry or automatic unlock after a denied recovery reload is allowed.

## Execution and evidence rules

Read [AGENTS](AGENTS.md), the [execution index](docs/plans/README.md), selected task/references, [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md) and [tests/README](tests/README.md). Select focused source-owned checks for changed behavior. Historical UI-NEXT/SAL-T08/BCF matrices are not automatically active gates; no full six-owner fresh/reuse repeat or production-release task is inherited. Documentation-only changes require diff/link/status/encoding checks, not SQL/GUI execution.

Interactive Windows/net48, serialized STA, WinFormsTestGuard, finite waits, bounded GUI-owned subprocesses and verified disposal remain required for GUI evidence. Keep hosted/native100/native125 and packaged/attended results distinct; test-session injection does not prove actual Login. Record actual client/work-area geometry and distinguish captures from physical-display observations.

SQL, when genuinely affected, requires explicitly designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master, sequential owners and positive identity/token checks before cleanup. Preserve reusable fixtures, dedicated fresh exceptions, worker drain and terminal quarantine. Do not restore the removed runner, gate manifest, approval-file or prefix-only drop flow.

Report exact source/test/package revisions, actual discovery/execution, environment, sanitized evidence and unresolved resources. New API names and test selectors remain proposals until source exists and nonempty discovery is verified. Missing, skipped or blocked mandatory cases never establish PASS. Historical totals and acceptance do not transfer to another candidate or imply production readiness.
