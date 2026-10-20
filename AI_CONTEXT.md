# MyDmsVn — Current context for Codex

**Branch:** `v1a-pro` · **Updated:** 2026-10-20 · **Source baseline assessed:** `f37038b250821a538b9cdc10a6d6d8b4bc1f386f` (PR #38 merge). This is a current-context handoff, not a runtime acceptance record.

## Owner decision and next work

The owner accepted the Business Compact / inline Goods Receipt direction, requested the necessary documents, made it the active roadmap, and requested removal of all older unimplemented plans with a PR into `v1a-pro`.

[BCI — Business Compact and inline Goods Receipt](docs/plans/20261020-001-business-compact-inline-goods-receipt.md) is the **sole active plan**. Read [the design](docs/BUSINESS_COMPACT_UI.md) and [acceptance matrix](docs/BUSINESS_COMPACT_ACCEPTANCE.md). BCI-T00 native Windows proof is complete in the retained test-host scope; see [evidence and implementation decision](docs/BUSINESS_COMPACT_ACCEPTANCE.md#81-bci-t00-native-editor-proof--2026-10-20). BCI-T01 lossless scalar parser is complete; see [scalar evidence](docs/BUSINESS_COMPACT_ACCEPTANCE.md#82-bci-t01-lossless-scalar-conversion--2026-10-20). BCI-T02–T06 are **NOT STARTED**; next is T02 stable projection and cell sessions. Stock editors remain the initial path with an app-owned editable accessibility bridge; exact control accessibility/alignment seams are specified but not implemented. No shipping product code or vendor pin changes in T00.

The unimplemented REL/P6 plan and deferred .NET 8 reactivation procedure are deleted. The old P7/net8 checklists are retired. Production prerequisites remain unaccepted/unscheduled in [scope boundaries](docs/DEFERRED_WORK.md#release-prerequisites), not a second work queue. The completed RESP, L2DB and GUI-P5 plans/evidence remain historical baselines. [The execution index](docs/plans/README.md) controls order.

## Current source facts

- Product and all ten active test projects target `net48`. Windows x86 Desktop.App and separate admin DbMigrator are the shipped package forms. AppCore/net8.0-windows remains PARKED / NOT SHIPPED; a modern SDK does not change product targets.
- Local WinForms invokes Desktop.Application and Server.Application through the in-process Local adapter. Public contracts remain transport-neutral. Clean Architecture and explicit UoW repository resolution are fixed boundaries.
- Catalog/Inventory persistence is LINQ to DB; other SQL consumers use typed native SqlClient. One explicit UoW owns connection/transaction. DbUp owns schema scripts. No provider fallback or old RepoDb/Dapper reintroduction.
- Permissions are code-defined Active keys with current identity checks, deliberate assignments and a single configured database-bound superuser. Unknown/deprecated deny before superuser; direct Deny wins for ordinary identities. Preserve hashing/salt and legacy algorithm handling.
- Draft/Post, rowversion, append-only ledger and balances remain unchanged. Post effects are atomic and retry/idempotency behavior needs real SQL proof when affected.
- Runtime accepts exact schema scripts 001–009; desktop performs read-only preflight, never admin migration. The separate migrator admits Empty/Compatible, not an unsupported old-prefix auto upgrade.

## Pinned UI graph and actual receipt behavior

| Component | Pin |
|---|---|
| MyDmsVn.BootstrapSourceGrid | `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` |
| Bootstrap5WinFormUI nested vendor | `aba102e33c48937fd92468791c287afda0a59e77` |
| SourceGrid nested vendor | `f4e457b43582bf01892f50bdc74aa480531e5944` |

`GoodsReceiptControl` still creates read-only line cells and external Product/Quantity/UnitCost editors. `ApplyState` rebuilds all cells. `AccessibleActionGrid` exposes read-only cells plus Open row actions; `AccessibleLookupBox` distinguishes pending/committed selection; `AccessibleNumericBox` bridges typed numeric value to accessibility. None establishes editable cell accessibility automatically.

BCI must preserve pending query text during option rebinding and intentional query cancellation on context changes. Query text is not a committed product. Keep historical selections, bounded async search, document/row generations, session disposal and late-result rejection.

`RequiresPostOutcomeReload` records uncertain Post outcome and remembers the receipt ID until a successful read resolves it. It blocks mutation/navigation; a denied reload does not unlock it. The line grid is currently enabled for viewing Posted receipts: future edit-start/commit must use full canEdit, not only grid.Enabled. Save/Post/navigation must account for active inline buffers before relying on VM.IsDirty.

Use existing `BootstrapLookupBoxEditor` and `BootstrapFormattedTextBoxEditor` first. Numeric parsing can stay app-owned with lossless input and commit validation; public alignment/editor factory/sidebar appearance gaps are conditional integration decisions, not permission for blanket vendor upgrades. T01 adds internal ReceiptNumericInput/ReceiptNumericField with scalar tests, not yet wired to GoodsReceiptControl. Remaining proposed adapter/grid names are future app code, not APIs already shipped by the vendor.

## Accepted baseline, with provenance

- RESP: PR #36 `1e24df4`; [fixture/reset/ownership evidence](docs/RESPAWN_VERIFICATION.md#pr-36-review-follow-up).
- L2DB: PR #37 `42d94ba`, T00–T07 complete in scope; [correctness/package/limited Local smoke](docs/L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke). T07's 581 non-GUI passes and 139 GUI NOT RUN are historical, not current BCI counts.
- GUI-P5: [final T06 acceptance](docs/GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20) completes T00–T06 within Owner-approved scope. Product/package `d5207b2ce1eae09e9317625b3d7d486c994f09fe`; Desktop test source `12e6a20a7af8e5c0f6bf820f172580f01e44181b`; Session build `cb86fbc`. Recorded Session 108/108, Desktop 329/329 at each measured 100%/125%, separate packaged 18/18: 784 passing invocations / 437 distinct obligations. They prove the existing external-editor workflow.
- T04 has 73 accepted observations. T05 retains 44 PASS / 1 PARTIAL, with Warehouse 125% Dark Down NOT TESTED explicitly accepted; 150% is outside that matrix. Never turn the exception into PASS or transfer it automatically to a new inline interaction.
- No `src` or vendor change separates accepted product `d5207b2` from the assessed `f37038b` baseline. This source comparison is not a newly executed test run. Historical failures, snapshots and cleanup remain in their canonical ledgers; their intermediate next-task instructions are superseded by the current index.

## Execution and evidence rules

Read [AGENTS](AGENTS.md), [the active index](docs/plans/README.md), the task/spec, [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md) and [tests/README](tests/README.md). Use focused tests and direct projects. No full SQL/GUI run for Markdown-only work.

Interactive Windows/net48, serialized STA, WinFormsTestGuard, bounded owned subprocesses and verified disposal are required for the corresponding GUI evidence. A test-session injection does not prove real Login. Preserve the existing GuiOwnedProcess/GuiPackageProbe path and finite budgets.

SQL requires explicit designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master, sequential owners and positive identity/token checks before cleanup. Preserve reusable fixtures, fresh exceptions, worker drain and terminal quarantine. Do not restore the deleted runner, gate manifest, approval-file or prefix-only drop flow.

Report exact source/test/package revisions, actual commands/outcomes, discovery reconciliation, environment and unresolved resources. New BCI obligations remain NOT RUN until observed. Retired release tasks remain unaccepted; no production certification follows from this handoff or UI plan.
