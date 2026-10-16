# TESTGATE — Modular Desktop and SQL test projects (T04–T05)

**Status:** Proposed / NOT IMPLEMENTED · **Date:** 2026-10-16 · **Scope:** cross-phase test project partitioning; local-only execution. This plan owns **TESTGATE-T04** and **TESTGATE-T05**, extracted without renumbering from [TESTGATE T01–T03](20261016-001-task-gates-and-modular-tests.md). Neither extraction nor green gates are claimed by creating this document.

## Roadmap dependency: temporary second-TFM pause first

This proposed T04/T05 implementation follows [NET8-PAUSE](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) in the ordered active roadmap. NET8-PAUSE has **not yet run**, so today's checked-in dual-TFM prerequisites still stand. At actual T04/T05 execution, first bind the reviewed **active project × TFM** inventory/Task/Final manifest; if NET8-PAUSE was accepted, run `net48` for active SQL/WinForms paths and explicitly classify parked `net8.0` / `net8.0-windows` cases as historical/temporarily out of release scope, **never as PASS or deleted logical cases**. The pre-move two-TFM baseline language below describes the historical original project and becomes a restoration requirement when both targets return; do not run a TFM not declared by the implementation revision. Update T04-A/T05-A pre-move inventories and exact pass gates by a reviewed baseline transition before moving any cases, preserving source/discovery/TRX bijection, SQL instance approval, watchdog and owned-DB cleanup. If baseline provenance cannot be established, mark BLOCKED, not green.

## Authority and prerequisites

Read `AGENTS.md`, [Codex test policy](../CODEX_TEST_EXECUTION_POLICY.md), [test gate matrix](../TEST_GATE_MATRIX.md), [test strategy](../TEST_STRATEGY.md), [tests README](../../tests/README.md), [proposed ADR 0006](../adr/0006-task-scoped-test-execution-and-module-projects.md), and accepted [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md).

- **T01** provides the L1/L2/L3/L4 contract. The baseline plan records its policy/docs as completed, but this is not evidence of new tools.
- **T02** is the prerequisite for the proposed strict **one-pass** unfiltered SQL acceptance. The currently documented strict SQL path is the multi-pass `baseline` benchmark; never invent `pr-acceptance` flags or substitute bare `dotnet test`.
- **T03** provides a versioned, independently verified *logical-case identity*, checked-in manifest, `Task`/`Final` gate orchestration, safe cross-project SQL process coordination and source/discovery/TRX reconciliation. **T04 may inventory existing projects before T03 completes, but MUST NOT migrate cases without a working reviewed identity/reconciliation gate. T05 MUST NOT migrate SQL cases until T02 and T03's guarded cross-project path is implemented and verified.** An absent prerequisite means `BLOCKED`, not a relaxed test.
- **T04 and T05 are separate reviewable code PR(s)**. A move from one assembly to another is a change to test execution ownership, not permission to shrink coverage. A follow-up modularization PR does not reopen SQLPERF-T04 fixture reuse or authorize SQL/GUI parallelism.
- **Desktop outer watchdog prerequisite:** Before GUI-bearing Desktop baseline/discovery, T04-A must identify and verify an existing bounded test launcher or implement/review one. It must enforce a whole-process-tree deadline, terminate/drain descendants on timeout, preserve per-TFM exit status and complete TRX evidence, and reject incomplete output or cleanup failures. This documentation PR does not establish an executable launcher. Until it is verified, Desktop `dotnet test` invocations (including `--list-tests`) are **BLOCKED**, not permitted as gate commands. T03's SQL process guard is not implicitly a GUI guard.
- **T04 pre-move executed baseline is a hard dependency:** before T04-B relocates any test, T04-A must capture or verify the **complete unfiltered executed TRX case inventory** from the original Desktop assembly on both original TFMs (`net48`, `net8.0-windows`), using that verified watchdog and frozen pre-move revision. Focused D0 results and discovery-only output cannot replace this gate; see the T04-A evidence contract below.

**No CI rollout:** all L1/L2 and applicable final L3 gates are local on a reviewed developer/Codex machine. Do not add or modify CI workflows or duplicate locally completed full SQL acceptance in CI. CI adoption requires a separate request. Retain raw TRX, approval and ownership journals locally; publish only reviewed sanitized aggregate evidence.

## Gate policy — inherited unchanged from T01–T03

| Level | When | Contract for this plan | Invalid shortcut |
|---|---|---|---|
| **L1** | On edits/fixes | Smallest affected method/class; pure tests where possible; real SQL only through approved guarded focused runner | Unfiltered SQL or whole solution automatically after edits |
| **L2** | Once a task slice is ready | Actual source-reviewed project/TFM/selector, module/category, adversarial cases, relevant build, watchdog/cleanup, independent inventory check and explicit result | Empty selection, tests run only on one required TFM, filter alone as completeness proof |
| **L3** | One **planned** local final invocation **per code-bearing PR** at a frozen gate-affecting revision | Entire required affected Release regression; for **both T04 and T05 code-bearing PRs**, mandatory SQL **strict unfiltered all-project/two-TFM (`net48`, `net8.0`)** acceptance; source/discovery/TRX, zero mandatory skips, approval/ownership reconciliation | Focused category pass, partial case inventory or stale evidence |
| **L4** | Separate explicit trigger | Manual GUI walkthrough, specialized SQL recovery, stress or benchmark A/B only where required | Automatically repeating performance benchmark for a normal refactor |

**Freeze/invalidation:** “one final invocation” is a planned boundary, not a promise to ignore failure. A failed L3 gate or later relevant change to source, tests/theory rows, mappings, SQL scripts, inventory, runner/harness, project/build configuration or gate inputs invalidates the affected evidence; repair using focused tests and rerun the complete applicable gate.

**Per-task accounting required:** record task ID, code revision, affected dependencies/modules, old/new project path and TFM, exact reviewed selector and expected discovered logical cases, build/run command actually used, SQL category or `none` with rationale, approval/watchdog/cleanup, discovered/executed/passed/failed/skipped (mandatory SQL skips = 0), before/after mapping, artifacts, `PASS / FAIL / BLOCKED / NOT RUN` and any remaining manual gate. `--list-tests`, a zero exit code or equal totals alone never proves complete Fact/Theory execution.

## Proposed target boundaries (not existing projects)

| Target concept | Responsibility | Runtime/isolation |
|---|---|---|
| `MyDmsVn.Desktop.Session.Tests` | Pure session, dispatcher, ViewModel and contract logic | No UI handles or SQL setup; proposed `net48;net8.0` if actual project references support both; normal parallelism only after shared-state audit |
| `MyDmsVn.Desktop.WinForms.Tests` | STA and control/message-loop tests, focus/input and global WinForms state | Keep serialized, deterministic disposal and headless failure capture; `net48` + `net8.0-windows` |
| `MyDmsVn.SqlTests.Foundation` | DAL, UnitOfWork, shared SQL contracts | Real SQL; categorization by behavior |
| `MyDmsVn.SqlTests.Catalog` | Master-data CRUD, mapping and lookups | Predominantly `FastSQL` |
| `MyDmsVn.SqlTests.Security` | Permissions, identity, provisioning and login-related SQL | `SecurityProvisioning` and applicable categories |
| `MyDmsVn.SqlTests.Inventory` | Receipt, posting, transactions and races | `FastSQL` plus `SqlConcurrency` when actually exercised |
| `MyDmsVn.SqlTests.Migrations` | DbUp, journal, upgrade/failure and drift | `MigrationSchema` and required cross-category checks |

Names are **illustrative only**. Confirm boundaries from the real source and maintain a documented cross-module regression home for tests spanning multiple domains. A project is a build/execution boundary, **module** is a business label, **category** describes SQL behavior; neither a project name nor category permits parallel execution. Do not create empty target projects merely to satisfy the picture.

### T04 Desktop project × TFM contract (verify actual `.csproj` after extraction)

| Project / responsibility | Before move (verified current) | Proposed after move; subject to real project build |
|---|---|---|
| Existing `MyDmsVn.Desktop.Tests` | `net48;net8.0-windows`, `UseWindowsForms=true` | Any remaining GUI-bearing cases must run on **every actually declared TFM**, normally `net48;net8.0-windows` |
| GUI-only `MyDmsVn.Desktop.WinForms.Tests` | Not created | `net48;net8.0-windows`, STA/serialized |
| Pure `MyDmsVn.Desktop.Session.Tests` | Not created | Prefer `net48;net8.0`, **without** `UseWindowsForms` or transitive GUI/SQL startup; verify actual references/targets before choosing |

Current source anchor: `src/MyDmsVn.Desktop.Application` targets `net48;net8.0`, while the original `tests/MyDmsVn.Desktop.Tests` and `src/MyDmsVn.Desktop.WinForms` target `net48;net8.0-windows`. These are **different target sets**, not interchangeable test filters. Build and execute the complete **reviewed project × actual TFM** matrix derived from each checked-in `.csproj` at the implementation revision. Do not call `-f net8.0-windows` for a pure project that only targets `net8.0`, nor silently omit its `net8.0` cases. Preserve each original `net48` and `net8.0-windows` behavior or require an explicit reviewed old→new coverage mapping for exceptions.

Because T03's durable logical-case key **includes TFM**, moving a genuinely pure case from the original `net8.0-windows` to `net8.0` **changes the logical key** even when its method and Theory row are unchanged. Require a reviewer-approved `(old project, net8.0-windows, old key) → (new pure project, net8.0, new key)` mapping, with proof that the case has no Windows-specific dependency and preserves the intended assertion/coverage. Independently reconcile source → expanded discovery → executed TRX for **both endpoints** of that mapping; equal counts or implicit same-key matching cannot substitute for this proof. Any unsupported or missing TFM, unexpected category change, duplicate or unmapped row is **BLOCKED**.

## Identity, inventory and ownership invariants

Apply the versioned logical-case identity from [T03](20261016-001-task-gates-and-modular-tests.md#logical-test-case-identity-and-migration-reconciliation-t03-foundation). Key is independent of assembly and TRX GUID: `(TFM, normalized method signature, stable Fact/Theory row key, reviewed category/non-SQL classification)`. Expanded Theory rows need deterministic canonical type-tagged argument fingerprints or reviewer-assigned opaque stable IDs; display names, positions, raw arguments and generated GUIDs are not durable keys.

For **each TFM separately**, capture (1) source method inventory and exemptions/classification, (2) independently discovered expanded Fact/Theory logical cases, (3) executed TRX cases with status, before and after moving. Require a bijection by logical key, an explicit old→new physical project mapping and reviewed mapping when a logical key intentionally changes. Reject collisions, missing/duplicate/ambiguous cases, unsupported/nondeterministic row IDs, changed categories, unexpected skips, missing ownership or case-count-only parity. Preserve original private evidence across PRs so a before/after claim can be independently checked against the pre-move revision.

## TESTGATE-T04 — Separate pure Desktop session/VM and GUI tests

**Scope:** Desktop test-project structure and test infrastructure, not production authentication/session behavior. Prerequisites: stable T03 logical identity/reconciliation for moved cases, supported Windows targeting packs and evidence-capable bounded GUI test execution.

### T04 implementation checklist

- [ ] **T04-A — Executed pre-move baseline and bounded execution.** First verify a Desktop outer watchdog that times out/terminates the **entire test process tree**, records real exit codes, checks complete TRX and cleanup, and fails closed on orphaned processes. Without it, executable Desktop discovery and baseline gates are **BLOCKED**. At a frozen **pre-move** gate-affecting revision, inventory every existing `MyDmsVn.Desktop.Tests` source method and expanded Fact/Theory row; inspect dependencies/transitive UI startup, global state, STA attributes, shared fixtures, test resources and exception/UI dialog handlers; classify `pure` or `GUI` with rationale. **Before T04-B**, run one watchdog-bounded **D2 unfiltered Release execution** of the entire original Desktop project on **both `net48` and `net8.0-windows`**, independently reconciling source → expanded discovery → executed TRX logical-case keys and statuses. Preserve full original local TRX, exit, watchdog/cleanup results, tested revision and inventory inputs; reuse earlier evidence only under the exact-match conditions below. D0 remains focused verification, **not** the complete executed baseline.
- [ ] **T04-B — Extraction (blocked until T04-A baseline passes).** Do not move any Desktop test until the **full pre-move source/discovery/executed-TRX D2 baseline** is verified for both original TFMs. Then move *only* proven pure session/dispatcher/VM tests to a real non-UI test project without `UseWindowsForms`/SQL initialization where possible. Choose the new pure project's actual `net48;net8.0` (or reviewer-justified alternative) targets from compatible references; document explicit `net8.0-windows → net8.0` logical-key/coverage mappings for any cases moved across TFMs. Keep all control/message-loop/global input/state tests in a serialized GUI project; retain STA, explicit dispose, modal-dialog suppression, `DataGridView.DataError`/unhandled exception capture and bounded cancellation.
- [ ] **T04-C — Project/runner updates.** Update solution, references, discoverability, test resources and manifests atomically. No duplicate test compilation or two assemblies claiming the same logical case. Check the **project × actual TFM** matrix from checked-in project files: GUI `net48;net8.0-windows`, pure `net48;net8.0` when supported. Retain the verified outer watchdog for all GUI-bearing old/new projects and test timeout, descendant termination, partial/missing TRX and orphan cleanup failures. Verify all declared target runtimes and preserve current serial fallback unless independent resource-safety evidence justifies a narrower guard.
- [ ] **T04-D — Reconciliation and acceptance.** Independently compare **all** old→new logical keys and source/discovery/executed TRX rows, using T04-A's **complete pre-move executed D2 baseline** and the full post-move D2 results per actual project/TFM; require explicit reviewed `net8.0-windows → net8.0` mappings when pure tests change TFM, not a same-TFM-only comparison. Review intentional method/namespace changes, categorization and missing/duplicated cases as hard failures. Verify GUI exception/STA/timeout/teardown and session/VM behavior; save sanitized evidence and perform mandatory Desktop and dual-TFM strict SQL L3 once at the frozen final revision.

### T04 L2 gate matrix

| Slice | Affected tests / TFM / selection | Adversarial coverage and completeness check | Evidence / exit |
|---|---|---|---|
| **T04-A** | **D-discover + D2 (mandatory pre-move):** independently inventory source and expanded cases, then run the **entire original Desktop project** unfiltered in Release on `net48` and `net8.0-windows` through the verified watchdog, at the frozen pre-move revision. **D0** is optional focused behavioral sampling only | Match **every** source/expanded Fact/Theory logical case with executed TRX outcome for each original TFM; reject missing/duplicate/ambiguous rows, unexpected skips, partial TRX, timeout, child leak, failed exit or cleanup. Check GUI classification/STA ownership | **Hard exit before T04-B:** full per-TFM original TRX + independent source/discovery/execution bijection and revision/runner evidence retained locally (or strictly matching historical evidence verified); otherwise **BLOCKED** even if D0 passes |
| **T04-B** | **Only after verified T04-A complete pre-move D2 baseline:** **D-discover + D1** independently discover and perform **bounded focused execution** in all affected old/new projects on each **actual declared TFM**; identify moved classes and expected logical cases | UI startup leaking into pure tests, parallel shared state, modal dialog/DataError, STA/dispose/timeout; check `net8.0-windows → net8.0` approved mappings, duplicate/missing rows | Non-empty focused selection; actual exit/TRX/watchdog/cleanup evidence; old→new key mappings including TFM changes or **BLOCKED** |
| **T04-C/D** | **D-discover + D2**: independent expanded discovery plus watchdog-bounded **unfiltered Release execution**, for **every remaining old/new project × its actual TFM** (GUI `net48;net8.0-windows`; pure `net48;net8.0` if supported), no skipped applicable cases | Per-endpoint source/discovery/TRX reconciliation and reviewed cross-TFM logical-key mappings; unchanged assertions, GUI watchdog/tree-kill/cleanup failure, no new SQL coupling | Every actual project/TFM passes with complete TRX and reconciles; missing target, key mapping or GUI watchdog = **BLOCKED**; strict dual-TFM SQL L3 also mandatory |

**T04-A complete executed pre-move evidence (hard exit gate before T04-B):** For the **frozen pre-move revision**, use the verified Desktop outer watchdog to execute **D2 once, unfiltered and in Release**, on the unchanged original `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` for **both `net48` and `net8.0-windows`** (serially while sharing GUI state). Reconcile **full source inventory → independent expanded Fact/Theory discovery → executed TRX logical keys/statuses** on each original TFM before relocating any test. Demand all applicable cases accounted for exactly once, no unexpected skips, successful real exits, complete separate TRX artifacts, bounded process-tree watchdog and clean termination/disposal; an empty/partial discovery, focused D0/TRX or green counts alone are insufficient. Retain the **original** locally auditable TRX, source/classification/exemption and logical-key inventory, exact tested revision and gate-affecting source/test/theory/manifest/runner/build inputs, the actual commands, watchdog/process cleanup evidence and reviewer-approved exceptions/mappings. Do not publish raw artifacts.

Historical pre-move D2 evidence may be reused **only if** its tested revision and all gate-affecting inputs exactly match the frozen pre-move snapshot, both original TFMs have complete locally retained TRX and independent case-level reconciliation is still possible. If any prerequisite, artifact, TFM, case or watchdog proof is absent or stale, perform a fresh approved bounded D2 run at the pre-move revision; otherwise mark **T04-A/T04-B BLOCKED**. This is a **one-time migration baseline**, distinct from the **post-move D2** and the **mandatory final L3** at the last gate-affecting revision: it does not authorize a full Desktop or SQL suite after every focused edit/task.

**T04 L2 SQL escalation:** default is **none** when only pure/GUI tests and project files move; this applies **only to focused task-level SQL checks**. If SQL-backed identity/adapter behavior, SQL harness or security dependencies change, run guarded **S** (`SecurityProvisioning` plus actually affected categories) on **both SQL TFMs**. Plain `dotnet test` is not a substitute. **L2 `none` does not waive L3.**

**T04 L3 (mandatory for a code-bearing PR):** at the frozen gate-affecting revision, run full applicable Desktop Release acceptance on **every reviewed project × its actual TFM** (original/GUI normally `net48;net8.0-windows`; pure `net48;net8.0` if supported), **and strict unfiltered SQL acceptance sequentially on `net48` and `net8.0`**, *even when L2 SQL escalation was `none`*, per `docs/CODEX_TEST_EXECUTION_POLICY.md`. Preserve independent source/discovery/expanded Theory/TRX reconciliation, zero mandatory SQL skips, SQL approval, process watchdog and owned-database cleanup. Use the verified existing strict SQL runner or T02's mode only after it is implemented and reviewed. If supported Windows GUI, bounded Desktop runner, SQL instance/approval or full strict two-TFM SQL evidence is unavailable, record **BLOCKED / NOT VERIFIED**, never READY. L4 interactive GUI walkthrough applies only if separately required by the phase.

## TESTGATE-T05 — Incremental SQL test-assembly partitioning

**Scope:** test ownership, harness, category/exemption inventory, cross-module regression and runner wiring; no production migration/SQL or business behavior changes. **Hard prerequisites:** approved live instance + current operator approval when executing SQL; T02 strict functional L3; T03 manifest, stable logical IDs, guarded multi-project selection, cross-process SQL serialization and independent reconciliation all implemented and reviewed.

### T05 implementation checklist

- [ ] **T05-A — Executed pre-move baseline and gate verification.** Freeze original SQL **and** reviewed non-SQL source, discovery/expanded Theory **and executed TRX logical-case keys**, classification/exemption fingerprints, migration audit calls and execution ownership on `net48` and `net8.0`. Before T05-B moves any SQL test, preserve strict unfiltered pre-move **executed TRX on both TFMs**, zero mandatory skips, approved SQL instance, watchdog and verified owned-database cleanup. Reuse historic evidence only when the exact frozen revision/inventory/gate inputs and complete local artifacts match; otherwise execute fresh approved strict baseline or mark **BLOCKED**. Prove T02/T03 inventory rejects unknown/empty project/category.
- [ ] **T05-B — Common harness and first coherent module.** Reuse the SQL approval, effective `master` preflight, disposable owned database, durable journal, process-tree watchdog, timeout and cleanup guards. Extract one proven module at a time; preserve embedded SQL resources, migrations, assembly metadata, fixtures and tests. Never clone/bypass SQL lifecycle logic.
- [ ] **T05-C — Gradual expansion.** Classify Catalog/Foundation (`FastSQL`), Security (`SecurityProvisioning`), Inventory (`FastSQL`/`SqlConcurrency`), Migrations (`MigrationSchema`) **by actual behavior**. Keep cross-module posting/permissions/transactions/upgrade/race/recovery cases in an explicit owned home. Rebuild per-assembly classification/exemption inventories and independent global aggregation after every move.
- [ ] **T05-D — Fail-closed proof and final acceptance.** Add negative tests for missing/duplicated/ambiguous/renamed assemblies/methods/Theory rows, unreviewed category or exemption drift, unstable identities, missing TFM, approval loss, timeout/child leak, crash/incomplete journal, ambiguous database ownership and incomplete cleanups. Prove serialized SQL execution **across processes and TFMs**, not merely xUnit collection guards. Independently reconcile the full pre/post logical-case bijection per TFM, then run one planned strict unfiltered global SQL L3 at frozen PR revision.

### T05 L2 gate matrix

| Slice | Affected tests / TFM / selection | Real-SQL category and required adversarial cases | Evidence / exit |
|---|---|---|---|
| **T05-A** | **R** entire SQL runner tests `net8.0`; **H** both TFMs if shared harness changes; source + independent expanded discovery **and executed pre-move TRX** of the original SQL project on `net48` and `net8.0` | Unknown/duplicate cases, row identity, stale exemptions; inventory-only checks need no SQL DDL, **but executed TRX requires approved real-SQL strict run** with watchdog, ownership and zero skips | Frozen source/discovery/execution one-to-one identity on each TFM, full original local TRX and strict evidence at the same revision; otherwise **BLOCKED before T05-B** |
| **T05-B** | **R/H** as affected; **S** on the original SQL project **only while it still owns the selected cases**; moved module via T03's **verified actual guarded** runner, never guessed paths | Per moved module `FastSQL` / `SecurityProvisioning` / `SqlConcurrency` / `MigrationSchema` as applicable; approval, ownership, rollback and fatal/timeout cleanup | Both TFMs, reviewed non-empty module + category selection; before/after logical-key bijection and physical ownership mapping |
| **T05-C** | **R/H** as affected, gated **each real affected module/category** on **all** old/new SQL assemblies; cross-module tests must be included | Inventory posting and transaction rollback, permission allow/deny/provisioning, schema drift and failed/aborted DbUp, real multi-session contention where affected; negative duplicate/reclassification tests | No missing project/category/theory row, no mandatory skips, complete audited cross-project selection |
| **T05-D** | **R/H** plus T03 guarded resource/concurrency negative tests; final L2 classification + inventory checks across all assemblies (not full unfiltered real-SQL acceptance yet) | Missing approval, cross-process lock bypass, stale/partial TRX, timeout/orphans/owned DB leaks, ambiguous cleanup; prove SQL resource serialization under reviewed capacity | Independently reconciled source/discovery/multiplicity/exemptions and exact gate inputs before frozen L3 |

**T05-A executed pre-move evidence (hard exit gate before T05-B):** At the frozen *pre-move* gate-affecting revision, independently reconcile **source inventory → expanded discovery → executed TRX logical keys/statuses** on each SQL TFM. Preserve the real strict unfiltered run's original local TRX, classifications/exemptions, project list, tested commit, approval/preflight, watchdog, zero mandatory skips and positive database-ownership/cleanup reconciliation for later comparison. Reuse older strict evidence **only** when its tested commit and all gate-affecting test/theory, migration, manifest, runner and build inputs match exactly and its raw artifacts are still independently auditable; matching counts or a shareable summary alone is insufficient. Otherwise obtain a fresh approved pre-move strict dual-TFM execution; unavailable SQL/approval means **BLOCKED**. This baseline is required once before migration, **not** a new full regression after each module move.

**T05 L3 (mandatory):** one planned **strict unfiltered** SQL acceptance on **all** original/remaining/new assemblies, on `net48` and `net8.0` **sequentially**, against an approved non-production instance, at the final gate-affecting revision. Independently reconcile source, expanded discovery and TRX against the global inventory, zero mandatory skips, real process exits, watchdog/approval/ownership journals and complete cleanup. Preserve all required Release project/architecture regressions. A filtered category, bare `dotnet test`, an unimplemented `pr-acceptance` mode or green aggregate counts alone are NOT acceptance. Report duration/resource deltas without claiming a speedup without controlled A/B evidence; benchmarking itself is conditional L4.

## Verified current command catalog (Windows, from repo root)

These are **pre-extraction commands for projects shown in the existing T01–T03 plan**, not proof that proposed new projects/modes exist. Review source/discovery at the actual revision, preserve timeouts/GUI watchdog, build the exact inputs and capture artifacts. When projects have been split, record their **real** paths and real supported TFMs from the checked-in solution/manifest; do not execute placeholders or keep filtering the now-empty old assembly.

**D-discover / D0 / D1 / D2 — Desktop selection and coverage catalog (NOT executable gate commands):**

**BLOCKED until a reviewed, verified GUI outer watchdog exists.** Direct `dotnet test` commands, including `--list-tests`, must **not** be used as T04 gates on a GUI-capable assembly: unmanaged hangs/modal dialogs can outlive `StaTest`'s cooperative timeout. T04-A must first establish and negatively test a runner that:

- Uses a finite deadline on **the whole `dotnet test` process tree** for each project/TFM; on timeout forcibly terminates/drains all descendants and detects orphans.
- Collects actual exit code, separately named TRX for each project/TFM, expanded-case discovery and independent reconciliation; rejects timeouts, absent/partial TRX, mandatory skips, unexpectedly empty selectors and cleanup errors.
- Preserves GUI serialization across old/new project runners, STA/control disposal, dialog/DataError exception capture, and a documented local evidence record; executes both supported TFMs serially when GUI state may conflict.

The following are **reference inputs** for a future tested watchdog launcher, not runnable PowerShell:

```text
Original GUI-capable project: tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj
Actual original TFMs: net48;net8.0-windows
D-discover: independent source/expanded Fact+Theory case discovery
  (a bounded --list-tests alone is not sufficient evidence of expanded rows)
D0: optional bounded focused Release execution on original project with unique TRX
  (supplementary only; never satisfies T04-A pre-move full executed baseline)
  Initial selectors:
    FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginViewModelTests
    FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginFormTests
    FullyQualifiedName~MyDmsVn.Desktop.Tests.DesktopSessionExpiryTests
    FullyQualifiedName~MyDmsVn.Desktop.Tests.UiDispatcherTests
D1: bounded focused Release execution of affected old + new projects
  using reviewed selectors; unique TRX/real exit/timeout/cleanup for each actual TFM
D2: bounded unfiltered Release execution with unique TRX and independent logical-case reconciliation
  T04-A pre-move HARD GATE: original project, both net48 and net8.0-windows, before T04-B
  T04-C/D post-move: every remaining old/new project × its actual declared TFM
After extraction (verify real paths/TFMs from .csproj, not guessed project names):
  Existing/GUI: net48;net8.0-windows where declared
  Pure: net48;net8.0 if compatible and declared
  Review explicit logical-key mapping for net8.0-windows -> net8.0 moves
```

Until such a wrapper and its failure-path tests exist, record **GUI-bearing D-discover/D0/D1/D2** as **BLOCKED / NOT RUN**. Pure-only checks may use a verified non-GUI path, but cannot stand in for the blocked GUI project gates. After implementation, **D-discover** verifies inventory, **D0/D1** execute focused tests, and **D2** runs the complete unfiltered original-project baseline **before T04-B**, then runs the complete all-project matrix **after the moves**. Do not fall back to unguarded GUI `dotnet test` or claim Desktop readiness; the strict SQL dual-TFM L3 remains separately mandatory for the code-bearing T04 PR.

**R — Existing SQL runner unit/negative suite (no SQL DDL):**

```powershell
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0
```

**H — Existing harness tests if the harness is affected (no SQL DDL):**

```powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net48
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net8.0
```

**S — Existing guarded, focused SQL category while original project owns its cases:**

Requires `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` to approved effective `master` on **non-production**, valid operator-managed `MYDMSVN_SQL_TEST_APPROVAL_FILE`, required SQL permissions, fresh artifact root and built runner/SQL projects. **One category only** (e.g. `FastSQL`), sequential both SQL TFMs; do not infer module selection from the name.

```powershell
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release
dotnet build tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release
$category = 'FastSQL' # Select one source-reviewed actually affected category.
$artifactRoot = '.sqlperf/testgate-modular-l2-' + [guid]::NewGuid().ToString('N')
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot $artifactRoot `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category $category `
  -Frameworks @('net48', 'net8.0')
```

**After T03 exists:** execute the checked-in, tested task manifest for the *actual moved* project/TFM/module/category and the current strict final runner. **Do not paste a speculative `Invoke-TestGate.ps1` or `pr-acceptance` CLI** before their parameters, tests and negative-case behavior have been reviewed. If cross-project execution cannot honor approval/inventory/watchdog/cleanup, mark T05 **BLOCKED**, not green.

## Exit, evidence and scope safeguards

- **T04 exit:** T04-A's **full executed pre-move D2 baseline** is verified for the original Desktop project on both original TFMs **before T04-B**; all original behavior preserved by that baseline ↔ post-move full source/discovery/TRX reconciliation per project/actual TFM, with explicit approved cross-TFM logical-key mappings if pure cases move from `net8.0-windows` to `net8.0`; pure tests free of unintended GUI/SQL dependencies; serialized STA GUI tests and full Release execution for **every actual Desktop project × TFM**; **strict unfiltered SQL L3 passes on both `net48`/`net8.0`** or the code-bearing PR stays **BLOCKED / NOT VERIFIED**.
- **T05 exit:** every original mandatory SQL case runs exactly once in intended category across **all** eligible assemblies, both SQL TFMs; verified guards, full failure recovery/ownership reconciliation and strict unfiltered final acceptance. Do not equate test project reorganization with performance optimization.
- **Out of scope:** changing production schema/DbUp/RepoDb/Dapper or auth/permission semantics, loosening SQL approval or per-test isolation, disabling GUI serialization, enabling unchecked SQL parallelism, new CI workflows, and reopening completed SQLPERF work.
- **Blockers:** absent T02/T03 implementation, missing Windows/TFM/GUI watchdog, missing approval/instance/permissions, empty/unreviewed selectors, unowned cases, or incomplete cleanup are `BLOCKED / NOT VERIFIED` with explanation. Do not quietly skip a required gate.
- **Docs-only extraction evidence:** filenames, links, command examples and status text can be statically inspected. No SQL/GUI tests are claimed executed by writing this plan.

### Report template for each T04/T05 implementation PR

```markdown
### TESTGATE modularization execution
- Task / slice / reviewed revision and gate-affecting inputs:
- Existing and new project paths, modules/categories; verified **project × actual TFM** matrix (GUI `net48;net8.0-windows`, pure `net48;net8.0` if declared):
- **T04-A pre-move full D2 baseline:** original project and both `net48`/`net8.0-windows`, frozen tested revision, full local TRX/source/discovery/executed logical-case reconciliation and watchdog/cleanup outcomes; reused historical evidence? exact input-match proof or `BLOCKED`:
- Before/after source and independently discovered/executed Fact/Theory logical-case keys (sanitized counts, approved cross-TFM old→new mapping and local evidence location):
- L1 focused selections and bounded commands/results (`D-discover` inventory vs `D0/D1` executed tests):
- L2 affected project × TFM matrix, `D1` focused and **both D2 runs** (pre-move original-project baseline and post-move all-project execution), adversarial checks, actual commands, pass/fail/skip/blocked, GUI watchdog status:
- SQL approval/preflight; watchdog; process lock; durable ownership and complete cleanup (sanitized):
- L3 affected Release gates for every actual Desktop project × TFM, frozen revision and invalidated/repeated gates:
- **L3 mandatory strict unfiltered all-project SQL `net48`:** PASS / FAIL / BLOCKED / NOT RUN, executed/discovered/skipped totals, watchdog/approval/ownership outcomes (sanitized):
- **L3 mandatory strict unfiltered all-project SQL `net8.0`:** PASS / FAIL / BLOCKED / NOT RUN, executed/discovered/skipped totals, watchdog/approval/ownership outcomes (sanitized):
- L4 explicitly triggered manual/performance work, if any:
- Original artifacts retained privately; sanitization review of published aggregate:
- Ready / NOT VERIFIED / BLOCKED:
```
