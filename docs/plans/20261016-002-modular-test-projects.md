# TESTGATE — Modular Desktop and SQL test projects (T04–T05)

**Status:** Proposed / NOT IMPLEMENTED · **Date:** 2026-10-16 · **Scope:** cross-phase test project partitioning; local-only execution. This plan owns **TESTGATE-T04** and **TESTGATE-T05**, extracted without renumbering from [TESTGATE T01–T03](20261016-001-task-gates-and-modular-tests.md). Neither extraction nor green gates are claimed by creating this document.

## Authority and prerequisites

Read `AGENTS.md`, [Codex test policy](../CODEX_TEST_EXECUTION_POLICY.md), [test gate matrix](../TEST_GATE_MATRIX.md), [test strategy](../TEST_STRATEGY.md), [tests README](../../tests/README.md), [proposed ADR 0006](../adr/0006-task-scoped-test-execution-and-module-projects.md), and accepted [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md).

- **T01** provides the L1/L2/L3/L4 contract. The baseline plan records its policy/docs as completed, but this is not evidence of new tools.
- **T02** is the prerequisite for the proposed strict **one-pass** unfiltered SQL acceptance. The currently documented strict SQL path is the multi-pass `baseline` benchmark; never invent `pr-acceptance` flags or substitute bare `dotnet test`.
- **T03** provides a versioned, independently verified *logical-case identity*, checked-in manifest, `Task`/`Final` gate orchestration, safe cross-project SQL process coordination and source/discovery/TRX reconciliation. **T04 may inventory existing projects before T03 completes, but MUST NOT migrate cases without a working reviewed identity/reconciliation gate. T05 MUST NOT migrate SQL cases until T02 and T03's guarded cross-project path is implemented and verified.** An absent prerequisite means `BLOCKED`, not a relaxed test.
- **T04 and T05 are separate reviewable code PR(s)**. A move from one assembly to another is a change to test execution ownership, not permission to shrink coverage. A follow-up modularization PR does not reopen SQLPERF-T04 fixture reuse or authorize SQL/GUI parallelism.
- **Desktop outer watchdog prerequisite:** Before GUI-bearing Desktop baseline/discovery, T04-A must identify and verify an existing bounded test launcher or implement/review one. It must enforce a whole-process-tree deadline, terminate/drain descendants on timeout, preserve per-TFM exit status and complete TRX evidence, and reject incomplete output or cleanup failures. This documentation PR does not establish an executable launcher. Until it is verified, Desktop `dotnet test` invocations (including `--list-tests`) are **BLOCKED**, not permitted as gate commands. T03's SQL process guard is not implicitly a GUI guard.

**No CI rollout:** all L1/L2 and applicable final L3 gates are local on a reviewed developer/Codex machine. Do not add or modify CI workflows or duplicate locally completed full SQL acceptance in CI. CI adoption requires a separate request. Retain raw TRX, approval and ownership journals locally; publish only reviewed sanitized aggregate evidence.

## Gate policy — inherited unchanged from T01–T03

| Level | When | Contract for this plan | Invalid shortcut |
|---|---|---|---|
| **L1** | On edits/fixes | Smallest affected method/class; pure tests where possible; real SQL only through approved guarded focused runner | Unfiltered SQL or whole solution automatically after edits |
| **L2** | Once a task slice is ready | Actual source-reviewed project/TFM/selector, module/category, adversarial cases, relevant build, watchdog/cleanup, independent inventory check and explicit result | Empty selection, tests run only on one required TFM, filter alone as completeness proof |
| **L3** | One **planned** local final invocation **per code-bearing PR** at a frozen gate-affecting revision | Entire required affected Release regression; SQL **strict unfiltered all-project/two-TFM** acceptance whenever required; source/discovery/TRX, zero mandatory skips, approval/ownership reconciliation | Focused category pass, partial case inventory or stale evidence |
| **L4** | Separate explicit trigger | Manual GUI walkthrough, specialized SQL recovery, stress or benchmark A/B only where required | Automatically repeating performance benchmark for a normal refactor |

**Freeze/invalidation:** “one final invocation” is a planned boundary, not a promise to ignore failure. A failed L3 gate or later relevant change to source, tests/theory rows, mappings, SQL scripts, inventory, runner/harness, project/build configuration or gate inputs invalidates the affected evidence; repair using focused tests and rerun the complete applicable gate.

**Per-task accounting required:** record task ID, code revision, affected dependencies/modules, old/new project path and TFM, exact reviewed selector and expected discovered logical cases, build/run command actually used, SQL category or `none` with rationale, approval/watchdog/cleanup, discovered/executed/passed/failed/skipped (mandatory SQL skips = 0), before/after mapping, artifacts, `PASS / FAIL / BLOCKED / NOT RUN` and any remaining manual gate. `--list-tests`, a zero exit code or equal totals alone never proves complete Fact/Theory execution.

## Proposed target boundaries (not existing projects)

| Target concept | Responsibility | Runtime/isolation |
|---|---|---|
| `MyDmsVn.Desktop.Session.Tests` | Pure session, dispatcher, ViewModel and contract logic | No UI handles, no SQL setup, normal parallelism only after shared-state audit |
| `MyDmsVn.Desktop.WinForms.Tests` | STA and control/message-loop tests, focus/input and global WinForms state | Keep serialized, deterministic disposal and headless failure capture; `net48` + `net8.0-windows` |
| `MyDmsVn.SqlTests.Foundation` | DAL, UnitOfWork, shared SQL contracts | Real SQL; categorization by behavior |
| `MyDmsVn.SqlTests.Catalog` | Master-data CRUD, mapping and lookups | Predominantly `FastSQL` |
| `MyDmsVn.SqlTests.Security` | Permissions, identity, provisioning and login-related SQL | `SecurityProvisioning` and applicable categories |
| `MyDmsVn.SqlTests.Inventory` | Receipt, posting, transactions and races | `FastSQL` plus `SqlConcurrency` when actually exercised |
| `MyDmsVn.SqlTests.Migrations` | DbUp, journal, upgrade/failure and drift | `MigrationSchema` and required cross-category checks |

Names are **illustrative only**. Confirm boundaries from the real source and maintain a documented cross-module regression home for tests spanning multiple domains. A project is a build/execution boundary, **module** is a business label, **category** describes SQL behavior; neither a project name nor category permits parallel execution. Do not create empty target projects merely to satisfy the picture.

## Identity, inventory and ownership invariants

Apply the versioned logical-case identity from [T03](20261016-001-task-gates-and-modular-tests.md#logical-test-case-identity-and-migration-reconciliation-t03-foundation). Key is independent of assembly and TRX GUID: `(TFM, normalized method signature, stable Fact/Theory row key, reviewed category/non-SQL classification)`. Expanded Theory rows need deterministic canonical type-tagged argument fingerprints or reviewer-assigned opaque stable IDs; display names, positions, raw arguments and generated GUIDs are not durable keys.

For **each TFM separately**, capture (1) source method inventory and exemptions/classification, (2) independently discovered expanded Fact/Theory logical cases, (3) executed TRX cases with status, before and after moving. Require a bijection by logical key, an explicit old→new physical project mapping and reviewed mapping when a logical key intentionally changes. Reject collisions, missing/duplicate/ambiguous cases, unsupported/nondeterministic row IDs, changed categories, unexpected skips, missing ownership or case-count-only parity. Preserve original private evidence across PRs so a before/after claim can be independently checked against the pre-move revision.

## TESTGATE-T04 — Separate pure Desktop session/VM and GUI tests

**Scope:** Desktop test-project structure and test infrastructure, not production authentication/session behavior. Prerequisites: stable T03 logical identity/reconciliation for moved cases, supported Windows targeting packs and evidence-capable bounded GUI test execution.

### T04 implementation checklist

- [ ] **T04-A — Baseline inventory and bounded execution.** First verify a Desktop outer watchdog that times out/terminates the **entire test process tree**, records real exit codes, checks complete TRX and cleanup, and fails closed on orphaned processes. Without it, executable Desktop discovery and baseline gates are **BLOCKED**. Record every `MyDmsVn.Desktop.Tests` source method and expanded Fact/Theory row **on both `net48` and `net8.0-windows`**; inspect dependencies/transitive UI startup, global state, STA attributes, shared fixtures, test resources and exception/UI dialog handlers. Classify `pure` or `GUI` with rationale. Freeze source/discovery/TRX identities and exemptions for the pre-move revision.
- [ ] **T04-B — Extraction.** Move *only* proven pure session/dispatcher/VM tests to a real non-UI test project without `UseWindowsForms`/SQL initialization where possible. Keep all control/message-loop/global input/state tests in a serialized GUI project; retain STA, explicit dispose, modal-dialog suppression, `DataGridView.DataError`/unhandled exception capture and bounded cancellation.
- [ ] **T04-C — Project/runner updates.** Update solution, references, discoverability, test resources and manifests atomically. No duplicate test compilation or two assemblies claiming the same logical case. Retain the verified outer watchdog for all GUI-bearing old/new projects and test timeout, descendant termination, partial/missing TRX and orphan cleanup failures. Verify both supported Desktop TFMs and preserve current serial fallback unless independent resource-safety evidence justifies a narrower guard.
- [ ] **T04-D — Reconciliation and acceptance.** Independently compare **all** old→new logical keys and source/discovery/executed TRX rows per TFM; review intentional method/namespace changes, categorization and dropped cases as hard failures. Verify GUI exception/STA/timeout/teardown and session/VM behavior; save sanitized evidence and perform applicable final L3 once at frozen revision.

### T04 L2 gate matrix

| Slice | Affected tests / TFM / selection | Adversarial coverage and completeness check | Evidence / exit |
|---|---|---|---|
| **T04-A** | **D0**: existing Desktop project, full reviewed source/test inventory and dual-TFM discovery; focused `LoginViewModelTests`, `LoginFormTests`, `DesktopSessionExpiryTests`, `UiDispatcherTests` as initial checks, **only via a verified bounded watchdog** | Find GUI-dependent helpers and Theory rows **beyond** these classes; compare source → independent expanded discovery → TRX; reject unstable keys, timeout/child leaks | Frozen per-TFM inventory and STA ownership; no missing cases; **BLOCKED** without watchdog |
| **T04-B** | **D1**: focused old + newly created **actual** pure/GUI project tests, on all applicable verified TFMs; identify changed test classes explicitly | UI startup leaking into pure tests, parallel shared state, modal dialog/DataError, STA and dispose failures, timeouts, duplicate/missing cases | Non-empty focused selection; old→new mapping, failures closed, bounded cleanup |
| **T04-C/D** | **D2**: watchdog-bounded complete inventory across **remaining old + all created** Desktop projects on `net48` and `net8.0-windows`, with no skipped applicable cases | Case-key bijection, unchanged assertions, TRX statuses, new UI dependencies, timeout/process-tree termination and cleanup failure, no new SQL coupling | Both runtimes pass and reconcile, or **BLOCKED** for missing project/TFM/watchdog; strict dual-TFM SQL L3 remains mandatory for this code PR |

**T04 L2 SQL escalation:** default is **none** when only pure/GUI tests and project files move; this applies **only to focused task-level SQL checks**. If SQL-backed identity/adapter behavior, SQL harness or security dependencies change, run guarded **S** (`SecurityProvisioning` plus actually affected categories) on **both SQL TFMs**. Plain `dotnet test` is not a substitute. **L2 `none` does not waive L3.**

**T04 L3 (mandatory for a code-bearing PR):** at the frozen gate-affecting revision, run full applicable Desktop Release acceptance on both `net48` and `net8.0-windows` **and strict unfiltered SQL acceptance sequentially on `net48` and `net8.0`**, *even when L2 SQL escalation was `none`*, per `docs/CODEX_TEST_EXECUTION_POLICY.md`. Preserve independent source/discovery/expanded Theory/TRX reconciliation, zero mandatory SQL skips, SQL approval, process watchdog and owned-database cleanup. Use the verified existing strict SQL runner or T02's mode only after it is implemented and reviewed. If supported Windows GUI, bounded Desktop runner, SQL instance/approval or full strict two-TFM SQL evidence is unavailable, record **BLOCKED / NOT VERIFIED**, never READY. L4 interactive GUI walkthrough applies only if separately required by the phase.

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

**D0 / D1 / D2 — Desktop selector and coverage catalog (NOT executable gate commands):**

**BLOCKED until a reviewed, verified GUI outer watchdog exists.** Direct `dotnet test` commands, including `--list-tests`, must **not** be used as T04 gates on a GUI-capable assembly: unmanaged hangs/modal dialogs can outlive `StaTest`'s cooperative timeout. T04-A must first establish and negatively test a runner that:

- Uses a finite deadline on **the whole `dotnet test` process tree** for each project/TFM; on timeout forcibly terminates/drains all descendants and detects orphans.
- Collects actual exit code, separately named TRX for each project/TFM, expanded-case discovery and independent reconciliation; rejects timeouts, absent/partial TRX, mandatory skips, unexpectedly empty selectors and cleanup errors.
- Preserves GUI serialization across old/new project runners, STA/control disposal, dialog/DataError exception capture, and a documented local evidence record; executes both supported TFMs serially when GUI state may conflict.

The following are **reference inputs** for a future tested watchdog launcher, not runnable PowerShell:

```text
Original project: tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj
TFMs: net48, net8.0-windows (each separately)
D0 initial selectors:
  FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginViewModelTests
  FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginFormTests
  FullyQualifiedName~MyDmsVn.Desktop.Tests.DesktopSessionExpiryTests
  FullyQualifiedName~MyDmsVn.Desktop.Tests.UiDispatcherTests
D1: --list-tests (discovery only; not execution)
D2: unfiltered Release execution with unique TRX per TFM
After extraction: enumerate remaining original + verified new projects/TFMs
```

Until such a wrapper and its failure-path tests exist, record D0/D1/D2 execution as **BLOCKED / NOT RUN**. Do not fall back to unguarded `dotnet test` or claim Desktop readiness; the strict SQL dual-TFM L3 remains separately mandatory for the code-bearing T04 PR.

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

- **T04 exit:** all original behavior preserved by per-TFM one-to-one source/discovery/TRX identity reconciliation; pure tests free of unintended GUI/SQL dependencies; serialized STA GUI tests and full affected Desktop Release gates pass on both Desktop TFMs.
- **T05 exit:** every original mandatory SQL case runs exactly once in intended category across **all** eligible assemblies, both SQL TFMs; verified guards, full failure recovery/ownership reconciliation and strict unfiltered final acceptance. Do not equate test project reorganization with performance optimization.
- **Out of scope:** changing production schema/DbUp/RepoDb/Dapper or auth/permission semantics, loosening SQL approval or per-test isolation, disabling GUI serialization, enabling unchecked SQL parallelism, new CI workflows, and reopening completed SQLPERF work.
- **Blockers:** absent T02/T03 implementation, missing Windows/TFM/GUI watchdog, missing approval/instance/permissions, empty/unreviewed selectors, unowned cases, or incomplete cleanup are `BLOCKED / NOT VERIFIED` with explanation. Do not quietly skip a required gate.
- **Docs-only extraction evidence:** filenames, links, command examples and status text can be statically inspected. No SQL/GUI tests are claimed executed by writing this plan.

### Report template for each T04/T05 implementation PR

```markdown
### TESTGATE modularization execution
- Task / slice / reviewed revision and gate-affecting inputs:
- Existing and new project paths, modules/categories, per-project TFMs:
- Before/after source and independently discovered Fact/Theory logical-case keys (sanitized counts + mapping location):
- L1 focused selections, command, result:
- L2 affected projects, selectors, negative cases, commands and pass/fail/skip/blocked:
- SQL approval/preflight; watchdog; process lock; durable ownership and complete cleanup (sanitized):
- L3 final affected Release gates; strict all-project dual-TFM SQL if required; evidence invalidation/repeats:
- L4 explicitly triggered manual/performance work, if any:
- Original artifacts retained privately; sanitization review of published aggregate:
- Ready / NOT VERIFIED / BLOCKED:
```
