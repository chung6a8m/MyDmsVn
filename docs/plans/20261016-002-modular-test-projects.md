# TESTGATE — Modular Desktop and SQL test projects (T04–T05)

**Status:** Proposed / NOT IMPLEMENTED · **Date:** 2026-10-16 · **Scope:** cross-phase test project partitioning; local-only execution. This plan owns **TESTGATE-T04** and **TESTGATE-T05**, extracted without renumbering from [TESTGATE T01–T03](20261016-001-task-gates-and-modular-tests.md). Neither extraction nor green gates are claimed by creating this document.

## Prerequisite and exact active vs historical framework baseline

This plan executes **after** [NET8-PAUSE](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) has passed its complete, reviewed acceptance envelope. It is **not yet implemented**; do not begin T04/T05 on an assumed net48-only branch. At T04-A/T05-A, freeze the actual implementation revision and produce a signed **active project × TFM matrix** and historical ledger:

| Snapshot | Application / SQL tests | WinForms GUI tests | Tool-only runtimes | Use during modularization |
|---|---|---|---|---|
| **Before NET8-PAUSE (historical)** | `net48;net8.0` | `net48;net8.0-windows` | Runner/Architecture `net8.0` | Preserve previously verified records, source/expanded-case identities and any auditable TRX; **not** an executable pre-move requirement after targets are retired |
| **After accepted NET8-PAUSE, before T04/T05 moves (active)** | **`net48`** | **`net48`** | Necessary runner/Architecture checks on verified `net8.0` tool graph | **Mandatory NEW pre-move D2/SQL baseline on net48 only**, at the frozen *post-pause/pre-move* revision |
| **After T04/T05 moves (active)** | **`net48`** on every declared old/new test assembly | **`net48`** on each declared GUI project | Actual supported tool-only target | Unfiltered post-move project × active TFM matrix and one guarded SQL acceptance per active TFM |
| **Later re-activation** | `net48;net8.0` | `net48;net8.0-windows` | Verified tool graph | Separately approve and re-run restored targets; explicit old→new framework logical-key mappings if needed, never claim parked PASS |

**Hard rule:** the pre-move baseline for a modularization PR must be collected at its **post-pause, pre-extraction** source/test/manifest revision on **every *active* TFM**, not both historical frameworks that no longer compile. Historical net8 results are retained as a separate *PARKED/NOT RUN* compatibility ledger; they cannot substitute for or invalidate a freshly proven net48 baseline. If the owner restores net8 before modularization, recompute the actual active matrix and test all declared restored TFMs. Never suppress a declared active TFM to make a gate pass.

NET8-PAUSE must also have resolved the net8-only runner/Architecture references and recorded the active SQL allowlist, logical identities and strict one-pass Final transition. T04/T05 must **bind**, not guess, their approved Task/Final/project matrix at each implementation revision. If any mandatory active project, GUI watchdog or SQL runner capability is absent, mark **BLOCKED**. Preserve case-level source → expanded discovery → executed TRX, SQL approval, serial lease and positive database ownership cleanup for all required active runs.

## Authority and prerequisites

Read `AGENTS.md`, [Codex test policy](../CODEX_TEST_EXECUTION_POLICY.md), [test gate matrix](../TEST_GATE_MATRIX.md), [test strategy](../TEST_STRATEGY.md), [tests README](../../tests/README.md), [proposed ADR 0006](../adr/0006-task-scoped-test-execution-and-module-projects.md), and accepted [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md).

- **T01–T03 are verified complete** and define the current Task/Final contract and historical baseline. Do not reimplement or rerun these tasks merely because this plan is active.
- **T02 already implemented** one-pass strict `pr-acceptance`; `baseline` remains a separate multi-pass performance mode. The active-TFM extension in NET8-PAUSE must be implemented and verified before this plan runs.
- **T03 is implemented** with versioned logical-case identities, manifest-owned Task/Final, global SQL serialization and case-level reconciliation. It does **not** yet make arbitrary GUI projects safe to run or allow multiple `SqlPrAcceptance` gates in one Final. Verify those capabilities independently before moving GUI/SQL cases; missing support is **BLOCKED**.
- **T04 and T05 are separate reviewable code PR(s)**. A move from one assembly to another is a change to test execution ownership, not permission to shrink coverage. A follow-up modularization PR does not reopen SQLPERF-T04 fixture reuse or authorize SQL/GUI parallelism.
- **Desktop outer watchdog prerequisite:** Before GUI-bearing Desktop baseline/discovery, T04-A must identify and verify an existing bounded test launcher or implement/review one. It must enforce a whole-process-tree deadline, terminate/drain descendants on timeout, preserve per-TFM exit status and complete TRX evidence, and reject incomplete output or cleanup failures. This documentation PR does not establish an executable launcher. Until it is verified, Desktop `dotnet test` invocations (including `--list-tests`) are **BLOCKED**, not permitted as gate commands. T03's SQL process guard is not implicitly a GUI guard.
- **T04 pre-move executed baseline is a hard dependency:** before T04-B relocates any test, capture a **fresh, complete, unfiltered executed D2 TRX** for the original Desktop assembly on **each actual active TFM** (normally `net48` after NET8-PAUSE), with verified GUI watchdog, frozen revision and case-level reconciliation. Parked net8 TRX is preserved as historical evidence, not re-executed.

**No CI rollout:** all L1/L2 and applicable final L3 gates are local on a reviewed developer/Codex machine. Do not add or modify CI workflows or duplicate locally completed full SQL acceptance in CI. CI adoption requires a separate request. Retain raw TRX, approval and ownership journals locally; publish only reviewed sanitized aggregate evidence.

## Gate policy — inherited unchanged from T01–T03

| Level | When | Contract for this plan | Invalid shortcut |
|---|---|---|---|
| **L1** | On edits/fixes | Smallest affected method/class; pure tests where possible; real SQL only through approved guarded focused runner | Unfiltered SQL or whole solution automatically after edits |
| **L2** | Once a task slice is ready | Actual source-reviewed project/TFM/selector, module/category, adversarial cases, relevant build, watchdog/cleanup, independent inventory check and explicit result | Empty selection, tests run only on one required TFM, filter alone as completeness proof |
| **L3** | One **planned** local final acceptance envelope **per code-bearing PR** at frozen revision | Affected Release regressions plus **strict unfiltered all-SQL-project acceptance on every actual active SQL TFM** (`net48` after approved pause; dual-target only after restoration). Desktop D2 is separately whole-tree watchdog bounded and required where GUI projects are affected. Reconcile source/discovery/TRX, zero mandatory skips, approval and cleanup | Focused category pass, stale evidence or historical parked net8 counted as a pass |
| **L4** | Separate explicit trigger | Manual GUI walkthrough, specialized SQL recovery, stress or benchmark A/B only where required | Automatically repeating performance benchmark for a normal refactor |

**Freeze/invalidation:** “one final invocation” is a planned boundary, not a promise to ignore failure. A failed L3 gate or later relevant change to source, tests/theory rows, mappings, SQL scripts, inventory, runner/harness, project/build configuration or gate inputs invalidates the affected evidence; repair using focused tests and rerun the complete applicable gate.

**Per-task accounting required:** record task ID, code revision, affected dependencies/modules, old/new project path and TFM, exact reviewed selector and expected discovered logical cases, build/run command actually used, SQL category or `none` with rationale, approval/watchdog/cleanup, discovered/executed/passed/failed/skipped (mandatory SQL skips = 0), before/after mapping, artifacts, `PASS / FAIL / BLOCKED / NOT RUN` and any remaining manual gate. `--list-tests`, a zero exit code or equal totals alone never proves complete Fact/Theory execution.

## Proposed target boundaries (not existing projects)

| Target concept | Responsibility | **Active after NET8-PAUSE**; reinstatement contract |
|---|---|---|
| `MyDmsVn.Desktop.Session.Tests` | Pure session/dispatcher/VM/contract, no UI handle and no SQL setup | `net48`, then reviewed `net8.0` when restored |
| `MyDmsVn.Desktop.WinForms.Tests` | STA/control/message loop/global input, serial and watchdog bounded | `net48`, later `net8.0-windows` only when restored |
| `MyDmsVn.SqlTests.Foundation` | DAL, UoW, real SQL | `net48` / category appropriate to behavior |
| `MyDmsVn.SqlTests.Catalog` | CRUD, master-data, lookups | `net48` / `FastSQL` where applicable |
| `MyDmsVn.SqlTests.Security` | Identity/provisioning/permissions | `net48` / `SecurityProvisioning` and other real categories |
| `MyDmsVn.SqlTests.Inventory` | Posting, transaction and races | `net48` / `FastSQL` or `SqlConcurrency` by behavior |
| `MyDmsVn.SqlTests.Migrations` | DbUp/journal/upgrade/drift | `net48` / `MigrationSchema` |

New project names are **illustrative**, not currently executable paths. Confirm source/TFM/dependencies, classify actual methods, and name one physical owner for cross-module test cases. A project is an execution boundary; neither project/category implies parallel safety.

### T04 Desktop project × active TFM and historical mapping

| Responsibility | Historical before pause | Active before/after T04 extraction | When net8 is restored |
|---|---|---|---|
| Original `tests/MyDmsVn.Desktop.Tests` | `net48;net8.0-windows` | Original **`net48`** pre-move; remaining GUI cases still `net48` | Re-enable/test `net8.0-windows` and audit source/expanded case identities |
| New GUI project | Not created | **`net48`** if created | Re-enable `net8.0-windows` explicitly and prove control/STA compatibility |
| New pure Session/VM project | Not created | **`net48`**, with no transitive GUI startup | Re-enable `net8.0` only after references/build/tests are independently verified |

The **active** logical-case identity contains TFM. A move from old to new *physical* project on **`net48`** is reconciled by exact case identity or an explicitly reviewed mapping for legitimate method/signature changes. The historical `net8.0-windows → net8.0` pure-test mapping is **deferred** until restoration, because neither endpoint is active during the pause. Preserve the baseline net8 method/row ledger and proposed physical ownership, but do not require impossible same-revision net8 execution during T04. When restored, prove both endpoint cases (expanded Theory rows included) against actual executables and record reviewed old→new mappings rather than simply comparing counts.

## Identity, inventory and ownership invariants

Apply the versioned logical-case identity from [T03](20261016-001-task-gates-and-modular-tests.md#logical-test-case-identity-and-migration-reconciliation-t03-foundation). Key is independent of assembly and TRX GUID: `(TFM, normalized method signature, stable Fact/Theory row key, reviewed category/non-SQL classification)`. Expanded Theory rows need deterministic canonical type-tagged argument fingerprints or reviewer-assigned opaque stable IDs; display names, positions, raw arguments and generated GUIDs are not durable keys.

For **each TFM separately**, capture (1) source method inventory and exemptions/classification, (2) independently discovered expanded Fact/Theory logical cases, (3) executed TRX cases with status, before and after moving. Require a bijection by logical key, an explicit old→new physical project mapping and reviewed mapping when a logical key intentionally changes. Reject collisions, missing/duplicate/ambiguous cases, unsupported/nondeterministic row IDs, changed categories, unexpected skips, missing ownership or case-count-only parity. Preserve original private evidence across PRs so a before/after claim can be independently checked against the pre-move revision.

## TESTGATE-T04 — Separate pure Desktop session/VM and GUI tests

**Scope:** Desktop test-project structure and test infrastructure, not production authentication/session behavior. Prerequisites: stable T03 logical identity/reconciliation for moved cases, supported Windows targeting packs and evidence-capable bounded GUI test execution.

### T04 implementation checklist

- [ ] **T04-A — Fresh active pre-move D2 and GUI watchdog.** Before relocating any test, bind the **actual post-NET8-PAUSE, pre-move** `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` on **`net48`** (or all TFMs actually reactivated before the move). Implement/verify a GUI-aware outer process-tree watchdog: finite whole-tree deadline, forced descendant termination/drain, orphan detection, unique per-project/TFM TRX, source → independent expanded Fact/Theory discovery → executed result bijection, deterministic STA/exception/cleanup. Freeze revision and record every active method/row and its pure-vs-GUI classification; perform one **unfiltered executed D2** baseline **on each active Desktop TFM**. Keep pre-pause net8 records in the historical ledger as **PARKED**, not a second current execution gate. D0 discovery/focused tests never replace D2.
- [ ] **T04-B — Extract verified pure cases.** Require the complete current active **net48** D2 baseline from T04-A before moving anything; stale old net8 evidence cannot satisfy it. Move only audited pure session/VM/dispatcher cases into an actual `net48` no-GUI/no-SQL test project. Preserve identical assertions/Fact/Theory rows, or use independently reviewed old→new method/signature mappings **on net48**. Keep WinForms/control/global-input tests in a serialized GUI project with STA and disposal/exception guards. No parked-TFM executable is required for this step.
- [ ] **T04-C — Project/runner binding.** Update `.sln`, references, resource ownership, source inventories and versioned Task/Final manifests for **all currently declared active project × TFM combinations**. Ensure no duplicate assembly compilation/test ownership, stale theory rows, extra UI startup in pure tests or unintended SQL dependency. The full GUI watchdog still owns every GUI-capable old/new assembly; Task `DotnetTest` must never be used to evade a GUI-resource guard.
- [ ] **T04-D — Post-move evidence and Final.** Run focused D1 then full **unfiltered post-move D2** for every active old/new project × TFM and independently reconcile each net48 source/discovery/TRX logical case with T04-A. Review remapped method/namespace identities, category and ownership. Verify GUI negative cases (modal/unhandled `DataError`, timeout/child leak, disposal failure) and preservation of current behaviors. Execute a *single planned* strict **active-TFM** SQL L3 (`net48` during pause), plus affected non-SQL/architecture/GUI full Release gates at frozen final revision. Restored net8 tests remain separate reinstatement work.

### T04 per-task L2 gate matrix (current post-pause active targets)

| Slice | Project × **active TFM** / selection | Negative/adversarial tests and proof | Hard exit |
|---|---|---|---|
| **T04-A** | Original Desktop.Tests **`net48`**; watchdog-bounded **D-discover then unfiltered D2**, not only class filters | Source/expanded discovery/executed TRX case-level bijection, STA/GUI classification, real exit and whole-tree watchdog/cleanup; record frozen revision | Full current **net48** original-project D2 baseline required before T04-B; archived net8 entries remain PARKED |
| **T04-B** | Actual original + new pure/GUI projects **`net48`**; D-discover and nonempty reviewed D1 classes after move | GUI initialization in pure assembly, missing/duplicate row, global-state race, dialog leak, incorrect split/method mapping | Focused case ownership matches pre-move baseline; no `net8.0-windows → net8.0` execution requirement while both are parked |
| **T04-C/D** | All actual old/new Desktop project × **`net48`** unfiltered D2 plus focused/architecture tests on **their own declared TFM** | Missing owners/mappings, unexpected skips, timeout/incomplete TRX, late UI exception, resource leak, changed behavior | Every active project/TFM passes and reconciles; verified bounded GUI watchdog; **strict SQL `net48` L3** once frozen |

**Required one-time original D2:** At T04-A's frozen *post-pause/pre-extraction* revision run the *unchanged original Desktop assembly* through the verified GUI outer watchdog. Retain auditable local source/expanded discovery/TRX with full identifiers, real exit code, STA/cleanup and process-tree termination proof. Historical D2 may replace this step **only** when it is on this exact original `net48` gate-affecting revision with independently retained and reconciled per-case TRX and the verified watchdog. An old revision's dual-TFM count or an executed pre-pause net8 TRX cannot qualify. If current baseline cannot be acquired, block extraction instead of trying the removed TFM.

**T04 L2 SQL escalation:** none for a proven test-only GUI/pure split; use the approved current active `SecurityProvisioning` (and actual affected categories) on `net48` only if SQL identity/adapter/harness behavior changes. A category is not a substitute for Final.

**T04 L3:** full affected Desktop **D2 on all actually active TFM(s)** under the outer GUI watchdog; required Release Application/Architecture/runner tests on their actual active/tool-only TFM; and strict **unfiltered** all-applicable SQL `net48` once when frozen. Complete approval, discovery/TRX/ownership/cleanup. If any later gate-affecting change occurs, invalidate/re-run affected complete gates. Never claim that historical net8 tests were executed, and never require a retired framework during the accepted temporary pause.

## TESTGATE-T05 — Incremental SQL test-assembly partitioning

**Scope:** test ownership, harness, category/exemption inventory, cross-module regression and runner wiring; no production migration/SQL or business behavior changes. **Hard prerequisites:** approved live instance + current operator approval when executing SQL; T02 strict functional L3; T03 manifest, stable logical IDs, guarded multi-project selection, cross-process SQL serialization and independent reconciliation all implemented and reviewed.

### T05 implementation checklist

- [ ] **T05-A — Current active pre-move SQL baseline.** Freeze the *post-pause/pre-extraction* original SQL assembly, `net48` logical source/expanded Theory/executed TRX case identities, real SQL category/exemption fingerprints, migration history and process ownership. Execute **one strict unfiltered `net48` pre-move pass** with approved non-production instance, watchdog and complete owned database cleanup **before T05-B**. Reuse historical evidence only if **same frozen revision + same active TFM + same audited source/discovery/TRX/runner inputs** and full raw local artifacts still prove every case. Earlier `net8.0` evidence stays historical/PARKED and is not a gate for T05-B. Also verify current single-assembly manifest rejects unknown/empty selection.
- [ ] **T05-B — Actual guarded multi-project contract and first module.** Existing T03 `ValidateFinalSqlContract` currently requires **exactly one SQL acceptance gate**. First implement and negatively test **all-project, unfiltered SQL Final aggregation** under one approved global SQL lease across actual modules/assemblies for **each active TFM**. Bind unique gate/project/category ownership, per-assembly source/discovery/TRX/approval/timeout/owned-db reports and global logical-case bijection; no separate unguarded invocations or cloned approval/cleanup logic. Extract the first coherent module *only after* the verified contract and T05-A evidence exist. Retain migrations/resources/harness fixtures and explicit cross-module case home.
- [ ] **T05-C — Incremental module extraction.** Move Catalog/Foundation (`FastSQL`), Security (`SecurityProvisioning`), Inventory (`FastSQL`/`SqlConcurrency`), and Migrations (`MigrationSchema`) by actual tested responsibility; keep cross-module posting/permissions/transaction/upgrade/race cases explicitly owned. After each move, use **focused active `net48` category × actual project** and current manifest-owned inventory validation, not full SQL regression. Reject duplicate physical owners, lost Theory rows, unexpected exemptions or missing module entries.
- [ ] **T05-D — Adversarial guards and final acceptance.** Negative-test missing/duplicate/new/renamed case/project, wrong category/TFM, stale fingerprints/approval, timeout/orphan, crash/journal corruption, ambiguous/leaked database, cross-process SQL lease failure and partial TRX. Independently reconcile **the entire old current net48 logical inventory to all new SQL assemblies** (approved explicit mappings for intentional signature changes); one planned **strict unfiltered all-project `net48` SQL L3** at frozen final revision plus affected non-SQL/GUI Release gates. Historical net8 remains parked until reactivation.

### T05 per-task L2 gate matrix (current post-pause active targets)

| Slice | Project/TFM/selection | Guarded SQL category / adversarial contract | Hard exit |
|---|---|---|---|
| **T05-A** | Existing runner tests **tool-only `net8.0`** (only if build graph resolved), harness tests **active `net48`**, full original SQL **`net48`** | Inventory/source/discovery/TRX of every active `net48` original case, unknown/duplicate/empty categories; approval, global lease, watchdog, disposal | One complete **net48** executed baseline at frozen revision; no pre-move net8 requirement |
| **T05-B** | Actual newly created module SQL project × **`net48`**, plus old owner while it still owns selected cases; R/H on their actual TFMs | Selected category `FastSQL`, `SecurityProvisioning`, `SqlConcurrency` or `MigrationSchema` as applicable; multi-project runner's negative owner/duplicate/missing tests | Verified per-project + **global net48** bijection; no bare SQL `dotnet test` or unreviewed multi-project manifest |
| **T05-C** | All actually affected old/new modules × **`net48`**, nonempty source-reviewed category/class selection | Posting rollback, permission deny, migration drift/recovery, multi-session contention where relevant; migration/exemption fingerprint audits | After each move all active cases have exactly one owner; only focused L2, not full-suite |
| **T05-D** | Actual all-SQL-project inventory **`net48`**, R/H tests on their actual TFMs | Missing approval, cross-process lock bypass, stale/partial TRX, timeout/orphans, ownership ambiguity/cleanup fail | Frozen global inventory with negative tests; one **unfiltered active-net48** SQL Final, no skipped mandatory cases |

**T05-A executed pre-move evidence:** Source → independently expanded Fact/Theory discovery → executed TRX must be reconciled **per active TFM**, with `net48` the only active SQL target while paused. Preserve full local TRX, revisions, mappings, approval/preflight/watchdog, zero mandatory SQL skips and ownership cleanup. Do not require a pre-move `net8.0` binary after removing that TFM; preserve its previous results as a separate historical restoration baseline. If the net48 pre-move revision differs in gate-affecting inputs, obtain one new approved strict pass instead of claiming an earlier stale result qualifies. This is a one-time T05 extraction gate, not a new full SQL suite after every module move.

**T05 final L3:** one **strict unfiltered** all-SQL-project pass over the **actual active project × TFM matrix** (`net48` during accepted pause), serialized under global SQL lease. Existing single-project T03 Final does **not** provide this multi-project acceptance by itself. Demand independently reconciled case-level source/discovery/executed TRX, zero mandatory skips, real process exit, approval, watchdog and complete owned-db cleanup for each assembly and globally. Historical `net8.0` remains NOT RUN. After later target restoration, re-run both SQL TFMs and explicit restored logical-case mapping/compatibility tests. Benchmark A/B is optional L4 and does not substitute for functional Final.

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
