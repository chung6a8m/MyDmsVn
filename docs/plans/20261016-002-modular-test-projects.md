# TESTGATE — Modular Desktop and SQL test projects (T04–T05)

**Status:** Proposed / NOT IMPLEMENTED · **Date:** 2026-10-16 · **Scope:** cross-phase test project partitioning; local-only execution. This plan owns **TESTGATE-T04** and **TESTGATE-T05**, extracted without renumbering from [TESTGATE T01–T03](20261016-001-task-gates-and-modular-tests.md). Neither extraction nor green gates are claimed by creating this document.

## Authority and prerequisites

Read `AGENTS.md`, [Codex test policy](../CODEX_TEST_EXECUTION_POLICY.md), [test gate matrix](../TEST_GATE_MATRIX.md), [test strategy](../TEST_STRATEGY.md), [tests README](../../tests/README.md), [proposed ADR 0006](../adr/0006-task-scoped-test-execution-and-module-projects.md), and accepted [ADR 0005](../adr/0005-test-isolation-and-bounded-parallelism.md).

- **T01** provides the L1/L2/L3/L4 contract. The baseline plan records its policy/docs as completed, but this is not evidence of new tools.
- **T02** is the prerequisite for the proposed strict **one-pass** unfiltered SQL acceptance. The currently documented strict SQL path is the multi-pass `baseline` benchmark; never invent `pr-acceptance` flags or substitute bare `dotnet test`.
- **T03** provides a versioned, independently verified *logical-case identity*, checked-in manifest, `Task`/`Final` gate orchestration, safe cross-project SQL process coordination and source/discovery/TRX reconciliation. **T04 may inventory existing projects before T03 completes, but MUST NOT migrate cases without a working reviewed identity/reconciliation gate. T05 MUST NOT migrate SQL cases until T02 and T03's guarded cross-project path is implemented and verified.** An absent prerequisite means `BLOCKED`, not a relaxed test.
- **T04 and T05 are separate reviewable code PR(s)**. A move from one assembly to another is a change to test execution ownership, not permission to shrink coverage. A follow-up modularization PR does not reopen SQLPERF-T04 fixture reuse or authorize SQL/GUI parallelism.

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

- [ ] **T04-A — Baseline inventory.** Record every `MyDmsVn.Desktop.Tests` source method and expanded Fact/Theory row **on both `net48` and `net8.0-windows`**; inspect dependencies/transitive UI startup, global state, STA attributes, shared fixtures, test resources and exception/UI dialog handlers. Classify `pure` or `GUI` with rationale. Freeze source/discovery/TRX identities and exemptions for the pre-move revision.
- [ ] **T04-B — Extraction.** Move *only* proven pure session/dispatcher/VM tests to a real non-UI test project without `UseWindowsForms`/SQL initialization where possible. Keep all control/message-loop/global input/state tests in a serialized GUI project; retain STA, explicit dispose, modal-dialog suppression, `DataGridView.DataError`/unhandled exception capture and bounded cancellation.
- [ ] **T04-C — Project/runner updates.** Update solution, references, discoverability, test resources and manifests atomically. No duplicate test compilation or two assemblies claiming the same logical case. Verify both supported Desktop TFMs and preserve current serial fallback unless independent resource-safety evidence justifies a narrower guard.
- [ ] **T04-D — Reconciliation and acceptance.** Independently compare **all** old→new logical keys and source/discovery/executed TRX rows per TFM; review intentional method/namespace changes, categorization and dropped cases as hard failures. Verify GUI exception/STA/timeout/teardown and session/VM behavior; save sanitized evidence and perform applicable final L3 once at frozen revision.

### T04 L2 gate matrix

| Slice | Affected tests / TFM / selection | Adversarial coverage and completeness check | Evidence / exit |
|---|---|---|---|
| **T04-A** | **D0**: existing Desktop project, full reviewed source/test inventory and dual-TFM discovery; focused `LoginViewModelTests`, `LoginFormTests`, `DesktopSessionExpiryTests`, `UiDispatcherTests` as initial behavioral checks | Find GUI-dependent helpers and Theory rows **beyond** the four example classes; compare source → independently discovered expanded cases → TRX; flag nondeterministic identity | Frozen per-TFM case inventory, checked classification/STA ownership, no missing rows |
| **T04-B** | **D1**: focused old + newly created **actual** pure/GUI project tests, on all applicable verified TFMs; identify changed test classes explicitly | UI startup leaking into pure tests, parallel shared state, modal dialog/DataError, STA and dispose failures, timeouts, duplicate/missing cases | Non-empty focused selection; old→new mapping, failures closed, bounded cleanup |
| **T04-C/D** | **D2**: complete test inventory across **remaining old + all created** Desktop projects on `net48` and `net8.0-windows`, with no skipped applicable cases | Case-key bijection, unchanged test assertions, TRX statuses, unexpected new UI assembly dependencies, test runner/watchdog failure, no new SQL coupling | Both runtimes pass, identities reconcile and exact local evidence recorded; missing project/TFM/tool = `BLOCKED` |

**T04 SQL escalation:** default is **none** if only pure/GUI tests and their project files move. If the move alters SQL-backed identity/adapter behavior, SQL harness or cross-module security dependencies, run **S** (`SecurityProvisioning` and any other actually affected category) on **both** SQL TFMs. SQL tests must use the approved guarded runner; plain `dotnet test` is not an SQL substitute.

**T04 L3:** after T04-A/B/C/D L2 is green and revision frozen, run full relevant Desktop Release acceptance on both `net48` and `net8.0-windows`, plus all other release/SQL gates actually affected by the PR under the repo's acceptance contract. Do **not** assume SQL L3 was run if it was not required/performed; document the rationale. Missing Windows GUI or bounded runner = `BLOCKED`, not green. L4 interactive GUI walkthrough only if the applicable phase explicitly requires it.

## TESTGATE-T05 — Incremental SQL test-assembly partitioning

**Scope:** test ownership, harness, category/exemption inventory, cross-module regression and runner wiring; no production migration/SQL or business behavior changes. **Hard prerequisites:** approved live instance + current operator approval when executing SQL; T02 strict functional L3; T03 manifest, stable logical IDs, guarded multi-project selection, cross-process SQL serialization and independent reconciliation all implemented and reviewed.

### T05 implementation checklist

- [ ] **T05-A — Baseline and gate verification.** Freeze original SQL **and** reviewed non-SQL source, discovery/expanded Theory, classification/exemption fingerprints, migration audit calls and execution ownership on `net48` and `net8.0`. Demonstrate that the T02/T03 gate can inventory every future eligible project and fails closed on unknown/empty project/category.
- [ ] **T05-B — Common harness and first coherent module.** Reuse the SQL approval, effective `master` preflight, disposable owned database, durable journal, process-tree watchdog, timeout and cleanup guards. Extract one proven module at a time; preserve embedded SQL resources, migrations, assembly metadata, fixtures and tests. Never clone/bypass SQL lifecycle logic.
- [ ] **T05-C — Gradual expansion.** Classify Catalog/Foundation (`FastSQL`), Security (`SecurityProvisioning`), Inventory (`FastSQL`/`SqlConcurrency`), Migrations (`MigrationSchema`) **by actual behavior**. Keep cross-module posting/permissions/transactions/upgrade/race/recovery cases in an explicit owned home. Rebuild per-assembly classification/exemption inventories and independent global aggregation after every move.
- [ ] **T05-D — Fail-closed proof and final acceptance.** Add negative tests for missing/duplicated/ambiguous/renamed assemblies/methods/Theory rows, unreviewed category or exemption drift, unstable identities, missing TFM, approval loss, timeout/child leak, crash/incomplete journal, ambiguous database ownership and incomplete cleanups. Prove serialized SQL execution **across processes and TFMs**, not merely xUnit collection guards. Independently reconcile the full pre/post logical-case bijection per TFM, then run one planned strict unfiltered global SQL L3 at frozen PR revision.

### T05 L2 gate matrix

| Slice | Affected tests / TFM / selection | Real-SQL category and required adversarial cases | Evidence / exit |
|---|---|---|---|
| **T05-A** | **R** entire SQL runner tests `net8.0`; **H** both TFMs if shared harness changes; original SQL integration project inventory on `net48` + `net8.0`, source + independent discovery | Source/manifest discrepancies, duplicate/unknown cases, Theory row identity, stale exemptions, no SQL DDL needed merely to collect inventory | Baseline manifest, eligible project list, nonempty classification for each TFM and negative test inventory |
| **T05-B** | **R/H** as affected; **S** on the original SQL project **only while it still owns the selected cases**; moved module via T03's **verified actual guarded** runner, never guessed paths | Per moved module `FastSQL` / `SecurityProvisioning` / `SqlConcurrency` / `MigrationSchema` as applicable; approval, ownership, rollback and fatal/timeout cleanup | Both TFMs, reviewed non-empty module + category selection; before/after logical-key bijection and physical ownership mapping |
| **T05-C** | **R/H** as affected, gated **each real affected module/category** on **all** old/new SQL assemblies; cross-module tests must be included | Inventory posting and transaction rollback, permission allow/deny/provisioning, schema drift and failed/aborted DbUp, real multi-session contention where affected; negative duplicate/reclassification tests | No missing project/category/theory row, no mandatory skips, complete audited cross-project selection |
| **T05-D** | **R/H** plus T03 guarded resource/concurrency negative tests; final L2 classification + inventory checks across all assemblies (not full unfiltered real-SQL acceptance yet) | Missing approval, cross-process lock bypass, stale/partial TRX, timeout/orphans/owned DB leaks, ambiguous cleanup; prove SQL resource serialization under reviewed capacity | Independently reconciled source/discovery/multiplicity/exemptions and exact gate inputs before frozen L3 |

**T05 L3 (mandatory):** one planned **strict unfiltered** SQL acceptance on **all** original/remaining/new assemblies, on `net48` and `net8.0` **sequentially**, against an approved non-production instance, at the final gate-affecting revision. Independently reconcile source, expanded discovery and TRX against the global inventory, zero mandatory skips, real process exits, watchdog/approval/ownership journals and complete cleanup. Preserve all required Release project/architecture regressions. A filtered category, bare `dotnet test`, an unimplemented `pr-acceptance` mode or green aggregate counts alone are NOT acceptance. Report duration/resource deltas without claiming a speedup without controlled A/B evidence; benchmarking itself is conditional L4.

## Verified current command catalog (Windows, from repo root)

These are **pre-extraction commands for projects shown in the existing T01–T03 plan**, not proof that proposed new projects/modes exist. Review source/discovery at the actual revision, preserve timeouts/GUI watchdog, build the exact inputs and capture artifacts. When projects have been split, record their **real** paths and real supported TFMs from the checked-in solution/manifest; do not execute placeholders or keep filtering the now-empty old assembly.

**D0 — Existing Desktop focused behavioral sample (L1/L2 only):**

```powershell
$desktopTests = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$desktopFilter = 'FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginViewModelTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.LoginFormTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.DesktopSessionExpiryTests|FullyQualifiedName~MyDmsVn.Desktop.Tests.UiDispatcherTests'
dotnet test $desktopTests -c Release -f net48 --filter $desktopFilter
dotnet test $desktopTests -c Release -f net8.0-windows --filter $desktopFilter
```

**D1/D2 — Complete Desktop before-move project baseline** (run only in an environment with supported GUI/tooling and a bounded outer watchdog; after moves, replace with verified real project list; `--list-tests` is discovery only):

```powershell
$desktopTests = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
dotnet test $desktopTests -c Release -f net48 --list-tests
dotnet test $desktopTests -c Release -f net8.0-windows --list-tests
dotnet test $desktopTests -c Release -f net48
dotnet test $desktopTests -c Release -f net8.0-windows
```

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
