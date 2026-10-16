# TESTGATE follow-up — Temporary removal of the .NET 8 application/test TFM

**Plan ID:** `20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8`  
**Status:** PROPOSED / NOT IMPLEMENTED. This documentation PR does **not** change any `.csproj`, `.sln`, runner, or manifest.  
**Sequence:** after [TESTGATE-T01–T03](20261016-001-task-gates-and-modular-tests.md), before [TESTGATE-T04/T05 modularization](20261016-002-modular-test-projects.md).  
**Decision (2026-10-16):** temporarily suspend the second `net8.0` TFM in dual-target non-WinForms projects, and `net8.0-windows` in dual-target WinForms projects, to reduce Codex feedback time; keep a reversible path back to both targets.

## 1. Motivation, authority and non-negotiable safety

The current required strict SQL full suite runs sequentially for `net48` and `net8.0`, with expensive database provisioning on each. TESTGATE Task/Final orchestration already avoids repeated full SQL gates during L1/L2. This plan adds a **temporary target-scope reduction**, not a new migration framework, a SQL-test bypass or a permanent deprecation of .NET 8.

- Preserve all SQL logical test cases, reviewed categories, isolation/approval, process-tree watchdog, inventory/discovery/TRX reconciliation, owned database cleanup, zero mandatory skips and one **unfiltered** full SQL pass per *active supported SQL TFM* at the frozen Final revision. Never substitute a filtered SQL category for Final.
- Keep the existing `-Frameworks` / `--frameworks` option on focused tools; **introduce and test an explicit framework list** on strict SQL PR acceptance / baseline / TESTGATE Final paths that currently assume both. Parameter compatibility must allow `net48` and `net8.0` when that project actually supports them. Do not hard-code only `net48`.
- Never run tests targeting an undeclared TFM, silently skip an unavailable mandatory TFM, or relabel historical dual-TFM evidence as a new single-TFM result. A target-scope change must be reviewed, recorded and reconciled before it becomes effective.
- **No CI changes** or repeated benchmark runs as a routine code-edit gate. SQL tests must use an approved disposable non-production SQL Server; approvals/connection strings and raw evidence stay out of PRs.
- This plan is *not permission* to remove `net8.0` everywhere without looking at dependencies: several tools/hosts are **single-target `net8.0` projects**, and `netstandard2.0` is a compatibility target, not a .NET 8 target.

## 2. Actual current target/dependency inventory (at planning time)

| Project group | Current TFM(s) | Desired temporary active behavior |
|---|---|---|
| Contracts, SharedKernel, Domain, Server.Application/Infrastructure, DbMigrator, Desktop.Application/Infrastructure.Local | `net48;net8.0` | **`net48` only** in the active application path, once consumers can still build |
| Desktop.WinForms shared UI and Desktop.Tests | `net48;net8.0-windows` | **`net48` only**; retain WinForms/STA guards |
| Server.Application.Tests, Server.Infrastructure.IntegrationTests, SqlTestHarness.Tests | `net48;net8.0` | **`net48` only**; retain every mandatory SQL logical case |
| Desktop.App | `net48` | Keep `net48` |
| Desktop.AppCore | `net8.0-windows` **only** | Cannot remove its sole TFM; **park/exclude from the active build/release matrix** until .NET 8 is restored, without deleting source, or choose an explicitly documented compatibility solution |
| Architecture.Tests, SqlTestRunner, SqlTestRunner.Tests | `net8.0` **only** | **Retain `net8.0` for the test-tool host** if needed; account for net8-only project references to retargeted libraries and the test fixture build; cannot quietly allow a broken solution |
| SqlTestHarness | `netstandard2.0` | Keep `netstandard2.0` (not a removable `net8.0` TFM) |
| Vendor Bootstrap/SourceGrid projects | Verify actual `.csproj` targets | Never rewrite vendored source automatically. Check dependency compatibility for active `net48` build |
| P7 Server.Api | Proposed `net8.0`, **not yet created** | P7 net8 implementation is blocked until target restoration or an explicit replacement architecture decision |

`global.json` may still require the .NET 8 SDK for building `net48` projects and tooling. Do **not** downgrade the SDK merely because runtime target `net8.0` is paused.

**Critical compatibility checkpoint:** `Architecture.Tests` and `SqlTestRunner.Tests` are net8-only projects that reference application/test projects. A net8-only consumer normally cannot reference a net48-only library. TFM changes **MUST NOT** land until the implementer has selected and verified a coherent approach: split a netstandard-compatible abstraction/fixture, retarget the test tool where technically supported, or retain a *narrow, documented net8 compatibility build* for genuinely required tool dependencies. If an exception would defeat the requested removal or reintroduce a dual SQL run, mark the task **BLOCKED** and surface the tradeoff; do not silently leave the old matrix in place.

## 3. Tooling contract: parameterized target selection, never irreversible shortcuts

Preserve/implement one canonical, explicit, validated **active framework list** owned by the reviewed gate configuration (for this interval: SQL `['net48']`; desktop GUI `['net48']`; tool-host `['net8.0']` where indispensable). The list of historical supported TFMs is stored separately for restoration; tool-host `net8.0` must **not** be mistaken for the SQL under-test target.

| Tool / artifact | Required action |
|---|---|
| `Invoke-FocusedSqlIntegrationTests.ps1` | **Keep `-Frameworks`** accepting `net48`, `net8.0`; temporarily default to the approved active SQL list or require it explicitly; reject unavailable targets early |
| `Invoke-SqlPrAcceptance.ps1`, `Invoke-SqlIntegrationBenchmark.ps1` | **Add `-Frameworks [string[]]`** with validation and forwarding; strict PR path still one unfiltered pass per requested active TFM; benchmark still multi-pass and opt-in |
| `Invoke-TestGate.ps1` | Preserve Task/Final mode, manifest-owned project/TFM selection and no user override that bypasses authority; configure active SQL TFM via *reviewed manifest/profile* while retaining per-gate framework declarations |
| `MyDmsVn.SqlTestRunner` CLI | Add/retain `--frameworks` in `pr-acceptance`, `baseline` and focused execution, with positive allowlist, explicit order, no duplicates/empty values, and check against built project TFMs; runner executable's own `net8.0` is independent |
| `tests/test-gates.v1.json` | Keep logical case IDs/fingerprints, framework-aware inventories and full historical mapping. Change only *execution-scoped* framework declarations with explicit reviewed migration, not a blind global deletion of `net8.0` strings |
| `SqlTestRunner.Tests.csproj` fixture target | Currently builds integration test fixture on **both** TFMs before runner tests. Make this follow verified active SQL targets without losing the ability to rebuild both later |
| TRX/manifest reconciliation | Detect unknown TFM, omitted active project, duplicate/empty selection, stale binary, net8-only tool mistaken for app coverage, missing logical cases or skipped SQL rows; failed/partial cleanup is always a failure |

### 3A. Concrete Task/Final registration and acceptance envelope (MUST implement in T03)

The current `tests/test-gates.v1.json` registers **only** `planId=20261016-001, taskId=TESTGATE-T03`. It has **no NET8-PAUSE gate**. `TestGateManifestValidator.Select` therefore rejects a NET8-PAUSE Task/Final request. Moreover, the **current** `ValidateFinalSqlContract` and `TestGateCoordinator.RunSqlGateAsync` explicitly demand `[net48, net8.0]`: merely editing `frameworks` in JSON will make Final fail. `Task` presently rejects any gate with SQL or GUI resources, and `Final` cannot run GUI-bearing `DotnetTest` gates. These are implementation requirements, not existing capabilities.

**Stable identity:** use the full filename stem as `planId`:
`20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8`.
The distinct `taskId`s are `NET8-PAUSE-T01` through `NET8-PAUSE-T05`; do **not** reuse `20261016-001 / TESTGATE-T03`, because that would select unrelated, historical tests. Introduce the following *proposed* manifest entries **only after** verifying the code and real selectors, with all v1-required fields (`project`, `frameworks`, `module`, `classification`, `selection`, `buildPrerequisites`, `resources`, `strictMode`, both timeouts, evidence). The names below are contracts, **not yet registered**.

| `taskId` / proposed `gateId` | Mode; responsible project × proposed active TFM | Exact selection type; required proof |
|---|---|---|
| T01 / `net8-pause-t01-audit` | Task, runner tests `tests/tools/MyDmsVn.SqlTestRunner.Tests/*.csproj` × `net8.0` (tool-only) | Reviewed `TestProjectBuildContractTests`, `TestGateManifestTests` and `TestGateRepositoryContractTests` classes, plus separate static project graph / baseline audit. No SQL or GUI resources |
| T02 / `net8-pause-t02-build` | Task, runner tests × `net8.0` (only after tool dependencies compile) | Reviewed `TestProjectBuildContractTests` + actual architecture reference checks; separate locked restore/Release build matrix for active `net48` and tool-only `net8.0`. If tool project cannot compile, **BLOCKED** |
| T03 / `net8-pause-t03-runner` | Task, runner tests × `net8.0` | Reviewed `TestGateManifestTests`, `TestGateCoordinatorTestsFinal`, `PrAcceptanceRunTests`, `BenchmarkRunCoordinatorTests`, `TestGateRepositoryContractTests`; add negative framework/ownership/approval/timeout assertions before binding selectors |
| T04 / `net8-pause-t04-docs` | Task, runner tests × `net8.0` (if runnable) | `TestGateRepositoryContractTests` / `TestProjectBuildContractTests` scoped checks **plus separate** documentation, links and packaging audit; **no GUI test** in ordinary Task |
| T05 / `net8-pause-t05-task` | Task, runner tests × tool-only `net8.0` | Whole reviewed runner/manifest regression before freezing inputs; no SQL/GUI resource in Task |
| T05 / `net8-pause-t05-final-runner` | Final, runner tests × tool-only `net8.0` | Unfiltered `DotnetTest` (no SQL/GUI resource); build fixture uses active SQL `net48` only |
| T05 / `net8-pause-t05-final-app` | Final, `tests/MyDmsVn.Server.Application.Tests/*.csproj` × `net48` | Unfiltered `DotnetTest`; no SQL/GUI resource; source/discovery/TRX |
| T05 / `net8-pause-t05-final-harness` | Final, `tests/MyDmsVn.SqlTestHarness.Tests/*.csproj` × `net48` | Unfiltered `DotnetTest`; no SQL/GUI resource |
| T05 / `net8-pause-t05-final-architecture` | Final, `tests/MyDmsVn.Architecture.Tests/*.csproj` × **actual compatible target** (currently tool-only `net8.0`) | Unfiltered `DotnetTest` only after T01/T02 resolve `net8.0` test → `net48` library incompatibility. If replaced, bind verified equivalent architecture coverage; do not silently omit |
| T05 / `net8-pause-t05-final-sql` | Final, `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/*.csproj` × **`net48`** | **Exactly one** strict `SqlPrAcceptance` gate, unfiltered, `RequiredExclusive` SQL, `Gui=None`; source/discovery/TRX/ownership/watchdog/approval/cleanup |
| T05 / **GUI D2 (external, NOT manifest `DotnetTest`)** | Packaged/Desktop `tests/MyDmsVn.Desktop.Tests/*.csproj` × `net48` | Independently verified **GUI-aware process-tree watchdog**, STA/serial execution, full unfiltered case-level TRX; bind its sanitized report to the same frozen revision. If no verified GUI launcher, **BLOCKED**, never omit from acceptance |

Full paths above use `*.csproj` to identify the **existing concrete project directories**, not as executable glob arguments: the actual manifest must store the exact existing path (e.g. `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj`). For Task selectors, verify test classes and expanded cases at the implementation revision; no unexpected empty selection is PASS. The repository's current `net8.0` runner.Tests prebuild also forces **both** SQL fixture TFMs; fix that build prerequisite before any after-pause Task/Final result can be trusted.

**Validator/coordinator change:** replace the two hard-coded dual-SQL-TFM checks with a fail-closed comparison of `gate.Frameworks` to a versioned, **reviewed active SQL framework list**, selected for the exact manifest revision (during pause `[net48]`; on restoration `[net48, net8.0]`). Validate nonempty, allowed/order-stable, distinct, declared/buildable and consistent across CLI, manifest, logical-case selected inventory and runner result. Unknown or manually overridden mismatched target fails. Ensure other programs cannot invoke `pr-acceptance` or `baseline` with a conflicting TFM list that masquerades as manifest Final.

**Final envelope, not a fake single command:** at the frozen commit, run (a) manifest-owned Final covering all the listed **non-GUI** required test projects and the entire active unfiltered SQL inventory **and** (b) the separately guarded full Desktop D2 GUI gate on every active GUI TFM, plus Release builds/packages of both active host and essential tool projects. The acceptance report must **join their revisions, project × TFM lists, identities and outcomes**; Final is **NOT complete** when any required external GUI, build, architecture or packaging gate is missing. Never label the manifest Final by itself as proof that a GUI-capable test ran.

Preserve all historical net8 logical identities in an explicitly versioned **historical/inactive inventory**, not in the **active execution set**; test the active `net48` case-by-case bijection against a reviewed pre-change `net48` baseline and reject any dropped test/row or duplicate owner. Historical `net8.0`/`net8.0-windows` cases are **PARKED / NOT RUN**, never selected, counted as PASS or deleted to shrink the expected total. Reactivation demands a reviewed inventory transition and a new strict full dual-TFM acceptance. The current v1 manifest has no `activeFrameworks` setting: explicitly design/test the versioned schema or a compatible, validated manifest sidecar before changing the default; do not insert an unrecognized JSON property into the strict loader.

**Required adversarial tests:** no matching `planId/taskId`, missing Task/Final entry, duplicate SQL owner, duplicate/empty/unsupported/out-of-order TFM, project graph incompatible with requested TFM, unregistered current logical case, parked case accidentally counted active, stale/inexact inventory or evidence SHA, runner tool TFM counted as SQL TFM, CLI/manifest disagreement, missing approved instance, timed-out/orphaned testhost, partial TRX, missing GUI proof and failed/ambiguous database cleanup. Preserve exact failure statuses and prevent a green report from a shorter-than-expected gate list.

**Existing focused SQL L2 command — runnable with the current wrapper (not L3):**

Run on Windows **from the repository root**. Prerequisites: a disposable non-production SQL Server explicitly approved for testing, `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective database `master`, a valid local `MYDMSVN_SQL_TEST_APPROVAL_FILE`, and the installed SDK/framework targeting packs. Do **not** print either secret/approval file. The focused wrapper invokes the net8-only *tool-host* runner with `--no-build`, so build both the runner and the **SQL test fixture for the selected `net48` TFM** first. The existing `-Frameworks` parameter already supports this explicit selection; it does **not** alter the repository's current dual-TFM Final obligations.

```powershell
$repoRoot = (Get-Location).Path
$sqlProject = 'tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj'
$runnerProject = 'tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj'
if (-not $env:MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING -or
    -not $env:MYDMSVN_SQL_TEST_APPROVAL_FILE) {
  throw 'An approved non-production SQL connection and approval file are required.'
}
dotnet build $runnerProject -c Release -f net8.0
if ($LASTEXITCODE -ne 0) { throw 'SQL test runner build failed.' }
dotnet build $sqlProject -c Release -f net48
if ($LASTEXITCODE -ne 0) { throw 'SQL test fixture build failed.' }
& ./tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot $repoRoot `
  -TestProject $sqlProject `
  -ArtifactRoot (Join-Path $repoRoot ('.sqlperf/focused-net48-' + [guid]::NewGuid().ToString('N'))) `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category SecurityProvisioning `
  -Frameworks @('net48')
if ($LASTEXITCODE -ne 0) { throw 'Focused SQL L2 failed.' }
```

**Proposed future strict command (NOT runnable until NET8-PAUSE-T03 implements and verifies the parameterized strict wrapper):**

Invoke from a **clean, committed, frozen candidate revision** after locked restore and Release build of its **actual verified** SQL test project and runner host; the strict wrapper also invokes the runner with `--no-build`. Use a unique artifact root and the same approved-instance safeguards. This command is a **lower-level example only**, not a substitute for manifest-owned NET8-PAUSE-T05 Final + external GUI D2/Release evidence.

```powershell
$repoRoot = (Get-Location).Path
$sqlProject = 'tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj'
& ./tests/tools/Invoke-SqlPrAcceptance.ps1 `
  -RepositoryRoot $repoRoot `
  -TestProject $sqlProject `
  -ArtifactRoot (Join-Path $repoRoot ('.sqlperf/pr-net48-' + [guid]::NewGuid().ToString('N'))) `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Frameworks @('net48')
```

The existing TESTGATE manifest-owned `-Mode Final` must remain the authoritative acceptance entry point for its reviewed task/project matrix; no undocumented override through `-Frameworks` on that wrapper. The sample strict command above documents the **lower-level** tool contract, not a substitute for the manifest-governed Final gate.

## 4. Reviewable task sequence and verification

### NET8-PAUSE-T01 — Baseline/authority and target/dependency audit

- [ ] Freeze the starting revision **before changing targets**; enumerate all source/test/vendor/host `.csproj` target declarations, `.sln`, lock/assets, conditional `#if NET8_0`, app outputs and dependency edges. Record the dual-target baseline and independently auditable prior logical-case/test evidence; **obtain/review the owner-approved candidate-only transition scope at T01** (projects, parked AppCore, tool exceptions, historical/active lists). This is the prerequisite for Stage B's narrow cutover acceptance; do not claim historic net8 cases ran on the net48-only candidate revision.
- [ ] Check which net8-only tools can compile with retargeted references. Resolve the compatibility checkpoint above before code edits; explicitly approve any narrow exception. Include reversibility and the P7 block in an ADR or owner-approved decision record.
- [ ] Verify T01–T03 Task/Final gate implementation, immutable SQL logical IDs and approval/cleanup safeguards at the actual revision. Do not reimplement these features.

**T01 L2 gate:** static project graph / manifest validation; no SQL test run. Evidence: signed-off target table, dependency diagnosis, agreed exception list, before inventory counts by project × TFM.

### NET8-PAUSE-T02 — Minimal, reversible project and solution changes

- [ ] **Only after approved T01 candidate scope binding**, for genuine dual-target application/test projects change `TargetFrameworks` to `TargetFramework` `net48` (WinForms: drop `net8.0-windows`). Use **Stage B active-target test rules** for this migration branch; keep source and framework-specific code for restoration, do not delete package/API branches or alter business rules.
- [ ] Keep the legacy host. Park the **net8-only AppCore** out of the active build/package path using explicit documented solution/release configuration while preserving the project/source. Verify no active net48 project improperly references it.
- [ ] Fix only independently identified net8-only tool dependencies without weakening inventory verification. Keep `netstandard2.0` harness. Update project/sln/publish scripts **atomically**, including vendor references/locked restore as needed. Do not hide a broken project behind a silent build exclusion.
- [ ] Confirm `dotnet restore --locked-mode` and Release build of each active project, with the net8 tool host/tests compiled separately if retained; record parked builds as **out of scope**, not passed.

**T02 L2 gate:** Windows locked restore + focused Release builds, compilation/reference-boundary checks. No SQL DDL required. Failure of an essential runner/host compilation is BLOCKED.

### NET8-PAUSE-T03 — Parameterize strict SQL / TESTGATE target selection

- [ ] Implement and test framework arrays, validation and forwarding through the strict PR path, benchmark, CLI and manifest/orchestrator. The strict path must still be **unfiltered**, one pass per active TFM, with watchdog/approval/ownership and reconciliation. Preserve framework flags for future reactivation.
- [ ] Update runner fixture build and gate manifest using a reviewed **migration of execution scope only**. Save historical logical-case TFM information and baseline evidence; prevent test case count shrinking undetected and reject zero selected logical cases.
- [ ] Add negative tests: `net9.0`, duplicate/empty framework list, unsupported declared TFM, stale manifest, missing active SQL owner, mismatch between CLI and Task/Final, net8-only runner accidentally counting as SQL coverage, lost approval, timeout and cleanup failure. Test `net48` success and `net8.0` reactivation selection when compatible binaries are available.
- [ ] **Implement and negatively test a minimal GUI-aware outer watchdog within this NET8-PAUSE implementation (before T05):** whole `dotnet test` process-tree deadline and kill/drain/orphan detection on Windows, per active Desktop project/TFM unique TRX and independently expanded Fact/Theory source/discovery/execution reconciliation, STA/global UI serialization and modal/`DataError`/unhandled-exception capture. Provide a reviewed entry point for **unfiltered `net48` Desktop D2**; no direct GUI `dotnet test` as an acceptance substitute. **This task owns the initial launcher so that the later T04-A can reuse/extend it; do not defer its implementation to T04-A and thereby deadlock the roadmap.**
- [ ] Keep the global exclusive SQL lease for all process/TFM combinations, including later restored net8 runs. Never open a full-suite shortcut via a command-line bypass.

**T03 L2 gate:** runner unit tests on their actual runnable framework plus manifest negative tests **and GUI-watchdog failure-path unit/integration probes** (forced timeout, orphan, partial/missing TRX, unexpected dialog, locked GUI resource). Run no unguarded GUI baseline or SQL; use focused approved SQL only for newly changed SQL lifecycle behavior and defer unfiltered full SQL until T05. The GUI launcher must be verified **before** T05's required D2.

### NET8-PAUSE-T04 — Align documentation, packaging and future plans

- [ ] Update `AGENTS.md`, `README.md`, `docs/TECH_STACK.md`, `docs/PROJECT_STRUCTURE.md`, `docs/TEST_STRATEGY.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md`, `docs/TEST_GATE_MATRIX.md`, `tests/README.md`, release scripts/runbooks and plan examples to distinguish **active** from **historical** TFM requirements.
- [ ] Update T04/T05 modular plan, SEC-REBUILD and remaining P5/P6 tasks: use the actual *active* test matrix **only after** this plan is implemented; do not erase historical dual-TFM baseline/case keys. Mark parked modern GUI/runtime artifacts as NOT RUN, not PASS.
- [ ] Make P7's `net8.0` server/API explicitly dependent on restoring .NET 8 (or a separately approved alternate framework plan). Local v1 release claims must match only shipped/verifiable targets, with an explicit owner decision about temporarily excluding AppCore.
- [ ] Record an explicit, reviewed `reactivate-net8` procedure, not an assumed one-line toggle.

**T04 L2 gate:** static docs/link/reference consistency, build/package configuration inspection and negative manifest checks. Documentation-only changes do not require SQL re-execution by themselves.

### NET8-PAUSE-T05 — Frozen one-pass acceptance and restore drill

- [ ] Freeze gate-affecting inputs (projects, build scripts, runner, manifests, tests/theory, SQL/migrations), recording the exact **Stage B candidate transition authorization** and reviewed active TFM list. Run affected focused L2, then **one planned** locally approved manifest-governed Final covering **all required non-GUI projects** and the **entire unfiltered net48 SQL test inventory exactly once**; separately execute the full bounded GUI D2 and Release/build/package checks, **all at the same revision**. Require zero skips and source/discovery/TRX bijection, process exit, watchdog, approved SQL owner journal and cleanup. A successful old `TESTGATE-T03` Final alone is **not** NET8-PAUSE acceptance.
- [ ] Run approved non-SQL Application/Desktop/Architecture/runner gates on their **actual active TFMs**, bounded STA GUI checks and build/package checks. Record parked modern-host evidence as NOT RUN with the accepted temporary scope.
- [ ] Drill reactivation in an isolated branch/worktree: restore `net48;net8.0`, `net48;net8.0-windows`, tool/manifest framework arrays and modern-host build; prove source/discovery logical keys for `net8.0` still map correctly. Do not claim dual-TFM release quality without executing its full gate.
- [ ] Compare duration (same environment and protocol, with setup/outliers explained); **do not claim a fixed 50% speedup**. Publish reviewed sanitized aggregates only; no raw TRX/SQL identifiers/approval journals.
- [ ] If the final revision changes gate inputs after passing, rerun the relevant mandatory Final gate; do not reuse stale acceptance artifacts. SQL/Windows unavailable is BLOCKED/NOT RUN.

**T05 exit:** reduced-TFM execution is effective only after the **manifest Final + external GUI D2 + build/architecture/package envelope** above is verified at the same frozen final revision and the restoration steps are tested. A failed, omitted or unavailable required gate remains BLOCKED and is never waived solely to improve speed.

## 5. Two-stage cutover acceptance, activation, rollback and re-enable conditions

### Stage A — Baseline on `main` and every unrelated PR (before merge)

- The checked-in release/SQL test contract remains **dual-target** (`net48` + `net8.0`, GUI `net48` + `net8.0-windows`) until an implementation PR has passed every stage below and is merged. Other PRs **cannot** use NET8-PAUSE's exception or claim that the draft plan changed support.
- **NET8-PAUSE-T01 before any target edit:** freeze the original dual-TFM source/manifest/project graph; inventory expected logical cases and review historical evidence for **both** active TFMs. Confirm the owner's temporary target-removal decision and record an explicit, versioned **candidate scope transition** (exact changed projects, parked AppCore, approved net8-only tool exceptions, current/recovery framework sets and fallback). Existing prior dual-TFM evidence remains historical, never relabeled as execution on the candidate revision. If baseline identity, environment or exceptions cannot be independently established, T01 is **BLOCKED**; do not shrink the gate just to obtain PASS.

### Stage B — Candidate-only transition authorization (the *same* NET8-PAUSE implementation PR)

- **Narrow, explicitly approved exception to the ordinary dual-TFM PR gate:** *only* the code-bearing PR implementing this particular NET8-PAUSE plan, after passing T01 and recording the owner-approved target transition, may evaluate its **post-T02/T03 candidate revision** against the proposed **active** project × TFM matrix. Removing the second TFM at T02 would make a dual-TFM Final technically impossible; the candidate therefore does **not** require an additional current-revision `net8.0` SQL/GUI pass after that target is removed. This is a targeted acceptance-policy change for this migration PR, **not** general permission to omit a currently declared, mandatory TFM.
- T02 changes actual `.csproj`/solution/packaging targets; T03 implements tested **framework-selectable** strict runner, manifest/profile, case inventory and the GUI watchdog; T04 reconciles docs; T05 executes the candidate **manifest-owned strict unfiltered `net48` SQL Final once at the frozen code revision**, plus **all required non-SQL project/tool-only architecture tests, bounded full `net48` GUI D2, Release/build/package checks and restoration drill** as one acceptance envelope. The prior `TESTGATE-T03` dual-Final is **not** evidence for this candidate. Never call an unimplemented selector/mode or use unguarded SQL/GUI execution.
- Prove a complete **active `net48` source → independent expanded discovery → executed TRX bijection**, unfiltered mandatory cases with zero skips, approved non-production `master` preflight, serialized SQL lease, bounded process-tree watchdog, durable owned database journal and positive cleanup. For removed TFMs use `PARKED / NOT RUN` and a separately preserved, independently auditable historical logical-case inventory; no PASS, silent deletion, extra filtered-only shortcut or count-only comparison.
- This Stage B exception must be bound to the **specific approved plan ID, branch/PR identity, reviewed before/after TFM manifest profile, affected-file scope, and exact tested commit SHA**. It cannot be reused by subsequent feature PRs or applied to unrelated production changes (security, schema/business logic, concurrency). A failed or unavailable active gate leaves the migration **BLOCKED / NOT VERIFIED**, not exempt; restore and re-run affected gates after any gate-affecting fix. The scoped transition is not considered production support or a passed release until T05 evidence and the PR are approved/merged.

### Stage C — After accepted merge and any later reactivation

- After the completed transition merges, the approved active application/SQL/GUI target is `net48`; indispensable verified `net8.0` **tool-only** projects are recorded separately and cannot count as app coverage. Every subsequent PR must execute all **then-active** mandatory gates, with the same unfiltered SQL safety constraints. The dual-target baseline remains historical only.
- Re-enabling `net8.0` / `net8.0-windows` requires a separate reviewed source/manifest/host transition and **fresh unfiltered full dual-TFM acceptance and GUI/release evidence** at the restoration revision. Do not silently reinterpret historic dual-TFM successes as current.

### Scope and rollback rules

- **Not yet active for `main` or unrelated PRs:** all dual-TFM requirements remain mandatory until the approved NET8-PAUSE implementation passes T01–T05 **and merges**. **Stage B is the explicit candidate-branch-only acceptance exception** permitting that same migration PR to execute its complete active `net48` Final after the corresponding T02/T03 targets/runner are changed. This docs-only PR changes no `.csproj` or test tooling.
- **Temporary active window:** once implemented and accepted, Codex uses net48 for application/SQL/WinForms behavior; net8-only tooling remains a *tooling-only* exception if proven needed. Historical dual-TFM verified evidence is retained separately; .NET 8 UI packaging is out of release scope unless it is restored and verified.
- **Mandatory restoration before P7:** the initial P7 `Server.Api net8.0` and modern WinForms parity work cannot start as a claimed supported runtime until target graph/manifest/runtime/tool gates are re-enabled and verified. Also reactivate earlier if a feature specifically requires `net8.0` or the product owner explicitly requests dual-target shipping.
- **Rollback trigger:** incompatible tool/reference graph, loss of strict SQL inventory guarantees, unbounded test process, unexplained behavior drift, unacceptable release target coverage or a failing reduced-TFM L3. Restore baseline target declarations/configuration and re-run affected exact gates; retain evidence of the aborted attempt.
- **Guardrail:** active-target reduction changes what is presently required, **not** the definition or correctness of any test. No auto-green or suppression by removing rows or classifiers from test inventory.

## 6. Explicit per-task L2 execution matrix (bind exact source cases before work)

**Current runnable commands vs proposed commands:** Project paths and test classes below were located in the planning-revision repo. T01/T02 preflight can run **static** and **pure, focused** checks directly before the new manifest entries exist; the proposed Task/Final calls are **NOT runnable today**. After T03, every Task must be registered and independently reconciled with its exact case set in the manifest; do not report older ad-hoc `dotnet test` as a manifest Task. In all rows, an unavailable selected test, absent SQL approval, unsupported TFM, changed input after verification or missing watchdog is `BLOCKED/NOT RUN` rather than `PASS`.

| Task | Actual project × TFM, verified class selector / prerequisite | L2 command or required binding | SQL category, timeout and evidence |
|---|---|---|---|
| **T01** | `tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj` × **tool-only `net8.0`**; classes `TestProjectBuildContractTests`, `TestGateManifestTests`, `TestGateRepositoryContractTests`. Static inventory of all `src/tests/vendor` .csproj and `global.json`, plus historical inventory | Static commands **G0** below. Focused pure class test **U** below when current tool graph builds; source-review and record selection. Future Task `net8-pause-t01-audit` | `NonSql`, no SQL approval; **300s per selected process / 900s envelope** (through reviewed Task watchdog after T03). Record actual build and source/declared TFM matrix, before logical-case fingerprints, empty/unavailable tests as blocked |
| **T02** | `Server.Application.Tests` × `net48`; `SqlTestHarness.Tests` × `net48`; legacy `Desktop.App` × `net48`, shared GUI library × `net48`; runner.Tests / Architecture.Tests × tool-only `net8.0` if reference graph repaired. Class `TestProjectBuildContractTests` in runner.Tests | Locked restore **B** and focused, explicit `dotnet build <real.csproj> -c Release -f <declared TFM>`; future Task `net8-pause-t02-build`. **Do not** run AppCore/old net8 project or treat its exclusion as a pass | `NonSql`; 300s/process, 900s overall for Task; record each build exit and skipped/parked project separately. Any broken essential tool dependency blocks T03 |
| **T03** | `SqlTestRunner.Tests` × `net8.0`; reviewed `TestGateManifestTests`, `TestGateCoordinatorTestsFinal`, `PrAcceptanceRunTests`, `BenchmarkRunCoordinatorTests`, `TestGateRepositoryContractTests`; fixture builds **only active SQL `net48`** | Focused **U3** below for RED→GREEN plus newly implemented GUI-watchdog negative tests; then proposed manifest `Task` for `NET8-PAUSE-T03`; reject `Final` until SQL/GUI launcher and all entries pass | `NonSql` for pure negative cases; GUI watchdog required at T03 with **900s per GUI process-tree**; SQL `SecurityProvisioning`/**affected category** only for real SQL lifecycle changes, **1200s SQL pass / 7200s aggregate**, approval, owner/cleanup |
| **T04** | `TestGateRepositoryContractTests`/ `TestProjectBuildContractTests` × runner.Tests `net8.0` plus exact `src/tests` project inventories and packaging script `deployment/release/Publish-LocalRelease.ps1` | Static **G0** + actual link/target check; proposed manifest `Task` for `NET8-PAUSE-T04`. Review packaged `net48` host target and park modern host explicitly | `NonSql`; 300s/process, 900s Task; packaging/build evidence, docs-link check and negative manifest checks; **no** SQL rerun for docs-only edits |
| **T05** | `SqlTestRunner.Tests` × tool `net8.0`; `Server.Application.Tests`, `SqlTestHarness.Tests`, SQL project and Desktop.Tests × active `net48`; Architecture.Tests × **verified actual** compatible target | Proposed `Task` `NET8-PAUSE-T05` then **F** manifest Final + verified external **GUI-D2**. Full Release builds/package + isolated restore drill. **F is NOT runnable today** | Approved **unfiltered SQL `net48`** with process-tree watchdog, instance/ownership/cleanup, **1200s SQL process and 7200s Final budget**; GUI-D2 whole-tree **900s/project/TFM** or earlier approved tighter measured bound. Record actual executed/discovered/failed/skipped cases, artifact/revision, mandatory zero SQL skips and independent GUI evidence; missing gate = BLOCKED |

**G0 — runnable static preflight (no SQL or GUI):**

```powershell
git status --short
git rev-parse HEAD
git diff --check
Get-ChildItem src,tests,vendor -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName
Get-ChildItem tests -Recurse -Filter *.csproj | Select-String -Pattern 'TargetFramework'
```

**U — existing focused pure tests (only if the actual net8-only tool project still builds):**

```powershell
$runnerTests = 'tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj'
$selector = 'FullyQualifiedName~TestProjectBuildContractTests|FullyQualifiedName~TestGateManifestTests|FullyQualifiedName~TestGateRepositoryContractTests'
dotnet test $runnerTests -c Release -f net8.0 --filter $selector
```

**B — example of existing active build target; enumerate all affected projects and tool-only exceptions:**

```powershell
dotnet restore MyDmsVn.sln --locked-mode
dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -f net48 --no-restore
dotnet build tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-restore
```

**U3 — existing non-SQL runner tests (selectors refer to real classes; new assertions must first be added):**

```powershell
$selector = 'FullyQualifiedName~TestGateManifestTests|FullyQualifiedName~TestGateCoordinatorTestsFinal|FullyQualifiedName~PrAcceptanceRunTests|FullyQualifiedName~BenchmarkRunCoordinatorTests|FullyQualifiedName~TestGateRepositoryContractTests'
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --filter $selector
```

**F — proposed future manifest-owned Final (NOT runnable until T03 and all registrations pass):**

```powershell
$pausePlanId = '20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8'
$artifactRoot = '.sqlperf/net8-pause-final-' + [guid]::NewGuid().ToString('N').Substring(0,12)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Final -PlanId $pausePlanId -TaskId 'NET8-PAUSE-T05' `
  -ArtifactRoot $artifactRoot -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE
```

**GUI-D2:** no verified whole-process-tree Desktop launcher exists in the source at this planning revision. **NET8-PAUSE-T03 explicitly implements/tests the initial launcher** and T05 must bind its actual reviewed executable command after negative tests pass (unique `net48` TRX, complete source/discovery/theory-row mapping, real exit, STA/serial and child cleanup). **Do not** run direct `dotnet test` / `--list-tests` against the GUI-capable assembly as proof of readiness. If the launcher is missing or fails, T05 remains **BLOCKED**; T04-A may extend/revalidate but must not be the first implementation prerequisite. A 900s timeout is a *design upper bound*, not evidence of an implemented timer. The Task manifest cannot bypass this by tagging the GUI assembly `NonSql`.

## 7. Task gate summary / handoff

| Task | L1/L2 feedback | SQL scope | Mandatory exit |
|---|---|---|---|
| T01 | Static project graph and baseline audit | None | Approved exception/compatibility decision |
| T02 | Targeted locked restore/Release build | None | Coherent build + parked host documented |
| T03 | Runner/manifest unit and negative tests | Focused only if SQL lifecycle changed | Framework-selectable strict gate without bypass |
| T04 | Documentation, packaging and link checks | None unless code also changed | No contradictory active/historical target claims |
| T05 | Frozen actual-project regression and restore drill | **One** strict unfiltered active `net48` acceptance | Exact logical inventory, watchdog, ownership, approval and cleanup green |

**Implementation handoff:** report changed project matrix, all exact commands, elapsed times, source/discovery/TRX counts, framework/host exclusions, test status `PASS/FAIL/BLOCKED/NOT RUN`, cleanup and restoration proof. Keep this file active until acceptance has evidence, then archive it with its final status.
