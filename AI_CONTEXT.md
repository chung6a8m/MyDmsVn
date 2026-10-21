# MyDmsVn — Current context for Codex

**Branch:** `v1a-pro` · **Updated:** 2026-10-21 · **Planning/source baseline:** `6040d7bcfca698fc9daba8268bb94e4454e8eacb` (PR #40 merge). This handoff records source facts and planned work, not new runtime acceptance.

## Owner decision and next work

The owner merged PR #40 and selected [BCF — lookup correctness and visual parity](docs/plans/20261021-001-business-compact-lookup-and-visual-parity.md) as the **sole active roadmap**. Read [BCF design](docs/BUSINESS_COMPACT_REFINEMENT.md), [acceptance ledger](docs/BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [three unchanged reference images](docs/design-references/business-compact/README.md).

**BCF-T00 is COMPLETE as a test-only RED checkpoint** for `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`: [15 intended native-key failures with clean ownership/drain](docs/evidence/bcf/20261021-t00-native-typing.md), test source `dbd079825f00fcf4b96294c90918cbaf712f7606`, unchanged product/vendor. **T01 binding repair is COMPLETE** with [172 focused hosted Windows passes](docs/evidence/bcf/20261021-t01-lookup-binding.md). **T02 is COMPLETE / lookup gate PASS** on frozen `67fff29`: [688 automated invocations across native100/125 Light/Dark, four separate all-three human reports and two clean owned package sessions](docs/evidence/bcf/20261021-t02-lookup-gate.md). A01–A13 are reconciled within that candidate input scope. Next is T03 shared shell/theme; T03–T06 remain NOT STARTED. The T02 prerequisite is satisfied; full BCF visual/Owner and release acceptance remain ungranted. Subsequent work is T04 Products, T05 receipt detail and T06 frozen comparison/Owner acceptance.

The visual phase explicitly includes Product catalog content/inspector and a guarded List/Detail receipt presentation. It expands the old BCI catalog exclusion and permanently visible split-list choice. Preserve established English UI; the three designs set composition and hierarchy, not new fields or localization.

BCI-T00–T06 remain COMPLETE / OWNER ACCEPTED in their [final signed scope](docs/BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21), now merged by PR #40. Its exact app/test/package revisions, failed trials, counts and accepted limitations stay in the original ledger. No historical PASS or GUI-P5 Down exception satisfies a new BCF obligation.

RESP, L2DB and GUI-P5 are also completed baselines. The unimplemented REL/P6 and .NET 8 reactivation plans remain deleted. [Release prerequisites](docs/DEFERRED_WORK.md#release-prerequisites) remain unaccepted/unscheduled constraints, not another work queue. [The execution index](docs/plans/README.md) controls order.

## Current source facts

- Product and all ten active test projects target `net48`; shipped packages are Windows x86 Desktop.App and separate admin DbMigrator. AppCore/net8.0-windows remains PARKED / NOT SHIPPED.
- WinForms invokes Desktop.Application and Server.Application through the in-process Local adapter. Preserve transport-neutral DTOs, Clean Architecture and explicit UoW repository resolution.
- Catalog/Inventory persistence uses LINQ to DB; other SQL consumers use typed native SqlClient. One explicit UoW owns connection/transaction, and DbUp owns schema scripts. No provider migration is planned.
- Permissions use code-defined Active keys, current identity checks and a configured database-bound superuser. Unknown/deprecated keys deny before superuser; direct Deny wins for ordinary identities. Preserve password hashing/salt and legacy algorithm handling.
- Draft/Post, opaque rowversion, append-only ledger and balances remain unchanged. Post effects are atomic; independent SQL assertions are required for affected posting/concurrency workflows.
- Desktop performs read-only schema preflight for scripts 001–009. The admin migrator admits Empty/Compatible, not unsupported old-prefix auto upgrade.

## UI baseline and defect boundary

| Component | Pin |
|---|---|
| MyDmsVn.BootstrapSourceGrid | `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` |
| Bootstrap5WinFormUI nested vendor | `aba102e33c48937fd92468791c287afda0a59e77` |
| SourceGrid nested vendor | `f4e457b43582bf01892f50bdc74aa480531e5944` |

`GoodsReceiptControl` already hosts inline Product/Quantity/UnitCost through `GoodsReceiptLinesGridAdapter`; the external line panel is removed. App-owned sessions, strict numeric parsing, incremental row projection, writable accessibility, compact navigation/layout and native numeric alignment are implemented. Preserve editor ownership, raw input, historical selections and stable row identity.

The reported list/header typing defects predate PR #40 and are retained in T00 evidence. T01 removes unconditional same-identity `SelectValue` and the Text-only restoration from ordinary binding. All three controls now use pinned DataSource reconciliation; explicit selection occurs only when the committed identity differs. Canceled preceding completion and shared warehouse notifications preserve native text/caret/range. `AccessibleLookupBox` still adds accessibility Help, not keyboard handling.

The shared warehouse lookup VM retains the filter and header's distinct roles. T01 preserves active-only optional filter choices and historical header selections, guards locked header callbacks/searches, cancels header queries at admitted document boundaries, and resolves pending header edits before Save/Post decisions. Filter commit reserves command admission before yielding for native Tab traversal; a newer command cannot be canceled by its deferred list load. Inline Product remains a regression reference. No vendor/API/schema/Application scope changed. `.Text = "gui"` or UIA SetValue alone does not establish per-key correctness; T02 verifies the native100/125 Light/Dark and attended package input boundary on its frozen candidate.

Keep `RequiresPostOutcomeReload` authoritative until successful reload; denied reload never unlocks it. Guards must resolve active inline buffers before relying on VM.IsDirty. The proposed `ReceiptWorkspaceMode`, `TryShowReceiptList()` and `ReceiptEditIntent.ReturnToList` do not exist yet: T05 must admit Back before focus-loss validation, reject dirty/numeric/recovery navigation without losing input, and explicitly cancel admitted query-only presentation. Reuse the same control/editor/adapter and preserve list state; actual document/session changes retain their explicit invalidation/disposal boundaries.

`CatalogControl` is shared by Products, Warehouses, Employees and Customers. T04 refines Products composition while preserving existing fields, commands, query/paging, activation and CatalogChangedMessage behavior for all kinds. Use supported vendor APIs and app-owned seams; any proven missing vendor seam requires a narrow compatible upstream change and traceable pin evidence.

## Execution and evidence rules

Read [AGENTS](AGENTS.md), [active index](docs/plans/README.md), the task/spec, [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md) and [tests/README](tests/README.md). Use focused direct project tests during tasks. T02 is the completed input gate on its recorded candidate; T06 is the broader frozen Session/Desktop/package/visual milestone. No SQL/GUI execution for Markdown-only changes.

Interactive Windows/net48, serialized STA, WinFormsTestGuard, finite waits, bounded GUI-owned subprocesses and verified disposal are required for GUI evidence. Keep native100/native125 and hosted/package results distinct. Test-session injection does not prove actual Login. Record actual client/work-area geometry and distinguish PrintWindow artifacts from physical display observations.

SQL requires explicitly designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master, sequential owners and positive identity/token checks before cleanup. Preserve reusable fixtures, fresh exceptions, worker drain and terminal quarantine. Do not restore the removed runner, gate manifest, approval-file or prefix-only drop flow.

Report exact source/test/package revisions, actual discovery/execution, environment, sanitized evidence and unresolved resources. Do not carry old test totals forward as expected results. BCF remains unaccepted until its new obligations and all three visual comparisons are reconciled with explicit Owner review; release acceptance remains separate.
