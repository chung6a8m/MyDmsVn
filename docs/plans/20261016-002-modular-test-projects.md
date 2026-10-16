# TESTGATE — Modular Desktop and SQL test projects (T04–T05)

**Status:** IN PROGRESS; NET8-PAUSE PR #32 is merged (`8e310778`); duration comparison was waived, not measured (`09d00bfa`). T04-A is complete at `c7bc4c8`; T04-B/C/D and T05-A/B/C/D remain open · **Date:** 2026-10-16 · **Scope:** cross-phase test project partitioning; local-only execution.

## Prerequisite and exact active vs historical framework baseline

This plan executes **after** [NET8-PAUSE](20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) has passed its reviewed acceptance envelope and merged. That prerequisite is satisfied on current `main` by PR #32 (`8e310778`); the owner waived the duration comparison in `09d00bfa` because comparable baseline evidence is unavailable. Do not treat the waiver as a measured speedup. At T04-A/T05-A, freeze the actual implementation revision and produce a **reviewed active project × TFM matrix** and historical ledger:

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
- **T02 already implemented** one-pass strict `pr-acceptance`; NET8-PAUSE-T03 added explicit framework arrays and the candidate profile. NET8-PAUSE T05 runtime/build/package/GUI/restoration gates pass at `6b0ce337`; PR #32 merged at `8e310778`. The owner waived the same-protocol duration comparison in `09d00bfa`; it remains unmeasured.
- **T03 is implemented** with versioned logical-case identities, manifest-owned Task/Final, global SQL serialization and case-level reconciliation. It does **not** yet make arbitrary GUI projects safe to run or allow multiple `SqlPrAcceptance` gates in one Final. Verify those capabilities independently before moving GUI/SQL cases; missing support is **BLOCKED**.
- **T04 and T05 are separate reviewable code PR(s)**. A move from one assembly to another is a change to test execution ownership, not permission to shrink coverage. A follow-up modularization PR does not reopen SQLPERF-T04 fixture reuse or authorize SQL/GUI parallelism.
- **Desktop outer watchdog prerequisite:** NET8-PAUSE-T03 implemented and negatively verified `Invoke-GuiTestAcceptance.ps1` for the original active-net48 Desktop project; NET8-PAUSE-T05 executed it at the accepted frozen revision. T04-A revalidated and strengthened its evidence reporting at `c7bc4c8`. T04-C/D must bind it to every new GUI-capable project; direct GUI `dotnet test` / `--list-tests` remains an invalid gate substitute.
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

- [x] **T04-A — Fresh active pre-move D2 and GUI watchdog.** Reused and strengthened the independently verified NET8-PAUSE-T03 GUI watchdog with a local case-level source/discovery/TRX inventory artifact. At frozen post-pause/pre-move revision `c7bc4c8794bf2d3b0ac11ea92a0db78273e8e819`, original Desktop.Tests × `net48` D2 passed: 230 source methods, 231 discovered cases, 231 TRX results (231 passed, 0 failed/skipped), watchdog and GUI lease clean. The 31-class local classification contains 126 GUI-capable methods, 91 pure Session/VM/dispatcher candidates, and 13 other non-GUI methods retained pending owner assignment. Raw inventory, classification, reconciliation CSV and TRX are local under `.sqlperf`; the sanitized aggregate is recorded in the runner output. GUI watchdog focused tests passed 13/13 on tool-only `net8.0`, including timeout, missing TRX, dialog, orphan and lease rejection cases. Keep pre-pause net8 records in the historical ledger as **PARKED**, not a second current execution gate. D0 discovery/focused tests never replace D2.
- [x] **T04-B — Extract verified pure cases.** Consumed the complete current active `net48` T04-A D2 baseline at `c7bc4c8`. Moved the 15 audited pure Session/VM/dispatcher source files (91 existing Fact cases) without changing their namespaces or method identities into `tests/MyDmsVn.Desktop.Session.Tests`, an active `net48` project with no WinForms, Server.Infrastructure, or SQL harness references. The original Desktop.Tests assembly retains the GUI/resource-bound cases and its serialization guard. Added one project-contract Fact in the new pure project. The manifest owns all 92 Session methods, including two reviewed incomplete-fingerprint exemptions bound to their current source fingerprints. Manifest-owned Task `TESTGATE-T04-B` passed source/discovery/TRX reconciliation: 92/92 passed, zero failed/skipped, watchdog and ownership clean; private TRX and sanitized output are under `.sqlperf/modular-t04-b-task-125102935b0b`. No parked-TFM execution was claimed.
- [ ] **T04-C — Project/runner binding.** Update `.sln`, references, resource ownership, source inventories and versioned Task/Final manifests for **all currently declared active project × TFM combinations**. Ensure no duplicate assembly compilation/test ownership, stale theory rows, extra UI startup in pure tests or unintended SQL dependency. The full GUI watchdog still owns every GUI-capable old/new assembly; Task `DotnetTest` must never be used to evade a GUI-resource guard.
- [ ] **T04-D — Post-move evidence and Final.** Run focused D1 then full **unfiltered post-move D2** for every active old/new project × TFM and independently reconcile each net48 source/discovery/TRX logical case with T04-A. Review remapped method/namespace identities, category and ownership. Verify GUI negative cases (modal/unhandled `DataError`, timeout/child leak, disposal failure) and preservation of current behaviors. Execute a *single planned* strict **active-TFM** SQL L3 (`net48` on current `main`), plus affected non-SQL/architecture/GUI full Release gates at frozen final revision. Restored net8 tests remain separate reinstatement work.

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

## Current command catalog and planned project-gate boundary (Windows)

**Existing commands below are only valid for projects/TFMs actually declared at the checked-in revision.** The repository includes the verified `Invoke-GuiTestAcceptance.ps1` watchdog for Desktop D2. The T05 multi-project SQL Final does **not** exist yet. Current `main` has the accepted matrix (`net48` for active SQL/Desktop; tool-only `net8.0`). Do not execute an undeclared TFM or silently substitute current `TESTGATE-T03` Final for modularized projects.

**R — Existing runner unit/negative tests, tool-host only `net8.0` (no SQL DDL):**

```powershell
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0
```

After pause, this command is valid **only if** NET8-PAUSE resolved the runner.Tests project references and its net48-only fixture prerequisite. Running runner unit tests does **not** establish a modern application/SQL TFM test.

**H — SQL harness unit tests, active `net48` (no SQL DDL):**

```powershell
dotnet test tests/MyDmsVn.SqlTestHarness.Tests/MyDmsVn.SqlTestHarness.Tests.csproj -c Release -f net48
```

**S — Existing focused SQL, one reviewed category on original current owner (not multi-module orchestration):**

```powershell
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release -f net8.0
dotnet build tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net48
$category = 'FastSQL' # Select the actual affected, source-reviewed category.
$artifactRoot = '.sqlperf/testgate-modular-l2-' + [guid]::NewGuid().ToString('N').Substring(0,12)
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot $artifactRoot `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category $category -Frameworks @('net48')
```

This call requires a valid `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` pointing effectively to approved non-production `master`, an operator-managed `MYDMSVN_SQL_TEST_APPROVAL_FILE`, preflight, unique artifact root, bounded process-tree watchdog, database ownership/cleanup, discovered/executed TRX verification and zero skipped mandatory SQL cases. It is **L2 only** and **only while the original project owns those cases**. After extraction, bind each moved module project to a **tested multi-project runner/manifest**; do not invent `-Module`/`-Project` selectors or point the original script at a new unverified assembly.

**D-discover/D0/D1/D2 — GUI watchdog contract and current CLI:**

```text
Original project before T04-B:
  tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj
  active TFM on current main: net48
  historical parked TFM: net8.0-windows (NOT RUN)
D-discover: source + independent expanded Fact/Theory case discovery
D0: optional focused, bounded execution (never equals full baseline)
D1: reviewed nonempty moved-case selectors per actual active old/new project
D2: complete unfiltered bounded execution with unique per-project/TFM TRX
  PRE-move original project's net48 D2 before T04-B
  POST-move every actual old/new Desktop project × current active TFM
  real exit, timeout/child drain, serial STA, dialog/DataError/disposal guards
```

T04-A independently revalidated and strengthened the NET8-PAUSE-T03 GUI-aware whole-process-tree launcher, including its negative paths. A direct `dotnet test` on an assembly with GUI-capable cases, even with `--list-tests`, cannot certify no modal hang or leaked descendants. For new projects, extend/rebind this watchdog; do not bypass it with the SQL runner's process guard.

**T05 module Final — future verified binding, NOT an existing command:** T03 currently permits exactly one `SqlPrAcceptance` gate, which is insufficient for the split modules. T05-B must implement an audited **all SQL projects × active TFM** strict execution and atomic coverage/ownership reconciliation within the tested Task/Final manifest contract. Record exact `planId`, `taskId`, `gateId`, project paths, source/expanded logical cases, resource lease and CLI once it exists; no speculative `Invoke-TestGate.ps1` mode or fabricated `pr-acceptance` project array is authorized.

## Exit, evidence and scope safeguards

- **T04 exit:** one complete **active `net48`** original-project D2 TRX/source/discovery baseline at frozen *post-pause/pre-move* revision, followed by complete current active project × TFM D1/D2 after extraction; zero unaccounted active Fact/Theory row, correct old→new physical ownership (and reviewed same-TFM identity mappings), pure tests without GUI/SQL startup, verified serial/STA GUI whole-tree watchdog and final full applicable Release plus strict unfiltered **active-net48** SQL acceptance. Historic net8 remains PARKED; no impossible same-revision dual-target demand.
- **T05 exit:** exactly one owner and one executed result for every originally mandatory **active net48** SQL logical case, across all new/remaining SQL assemblies; tested all-project final runner, all categories and source/discovery/TRX checked globally, cross-process serial lease, real SQL approval, complete watchdog/ownership/cleanup and strict **unfiltered net48** acceptance at frozen revision. Historical net8 logical cases remain retained for later restoration; do not treat test project reorganization as performance proof.
- **Blocking conditions:** stale pre-move net48 baseline, no verified GUI watchdog, missing cross-project SQL gate, missing required supported Windows/SQL/TFM/approval resource, empty selector, missing logical case, incomplete cleanup or any unjustified bypass means **BLOCKED/NOT VERIFIED**. Do not label old dual-TFM historical artifacts as current green evidence.
- **Out of scope:** production schema/DbUp/RepoDb/Dapper behavior changes, altering security/permission semantics, skipping SQL approval/isolation, parallelizing GUI input/SQL without adversarial isolation proof, new CI workflows or reopening completed SQLPERF measurement.
- **Docs-only evidence:** validate links and actual project matrices statically; this plan is not proof of an executed SQL/GUI test. Full tests belong to the separate implementation PR at its frozen code revision.

### Required T04/T05 implementation report

```markdown
### TESTGATE modularization execution (one report per implementation PR)
- Task/slice, frozen baseline SHA and current final SHA:
- Active test targets after approved NET8-PAUSE: net48 app/SQL/GUI;
  tool-only net8.0 exception, actual architecture coverage and parked historical
  net8.0/net8.0-windows inventories (PARKED / NOT RUN):
- Original project + original PRE-move active-net48 source/discovery/TRX case-level
  inventory at its exact frozen post-pause/pre-move revision; GUI D2 watchdog:
- POST-move old/new actual project × active TFM matrix, expanded Fact/Theory
  old→new identity/owner bijection; no missing/duplicate/ambiguous cases:
- D1 focused selectors, D2 full project results for every active GUI/pure test
  project; real exit/TRX/STA/modal/whole-tree timeout/cleanup:
- T05 protected module/category ownership, cross-project SQL runner negative tests,
  approval/preflight, exclusive lease, ownership and database teardown:
- L3 unfiltered all SQL project(s) net48: PASS/FAIL/BLOCKED/NOT RUN, exact
  source/discovered/executed/passed/failed/skipped cases and results:
- L3 required non-SQL project/architecture/GUI Release results and same-revision
  evidence; stale inputs after Final cause affected rerun:
- Historical net8 outcomes retained but NOT RUN under pause; restoration plan and
  new dual-target acceptance prerequisite:
- Reviewed local-only sanitized output; do not publish raw TRX, SQL approval or
  ownership journals:
- Ready / BLOCKED / NOT VERIFIED:
```
