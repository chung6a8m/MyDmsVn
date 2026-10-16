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

**After implementation, illustrative focused command (exists today):**

```powershell
tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf/focused-net48 `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category SecurityProvisioning -Frameworks @('net48')
```

**Proposed future strict command (NOT runnable until corresponding code/task passes):**

```powershell
tests/tools/Invoke-SqlPrAcceptance.ps1 `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf/pr-net48 `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Frameworks @('net48')
```

The existing TESTGATE manifest-owned `-Mode Final` must remain the authoritative acceptance entry point for its reviewed task/project matrix; no undocumented override through `-Frameworks` on that wrapper. The sample strict command above documents the **lower-level** tool contract, not a substitute for the manifest-governed Final gate.

## 4. Reviewable task sequence and verification

### NET8-PAUSE-T01 — Baseline/authority and target/dependency audit

- [ ] Freeze the starting revision; enumerate *all* source, test, vendor and host `.csproj` target declarations, `.sln` configuration entries, package assets, conditional `#if NET8_0` uses, app outputs and dependency edges. Record the baseline active/historical matrix and prior dual-TFM test inventory without pretending to re-run it.
- [ ] Check which net8-only tools can compile with retargeted references. Resolve the compatibility checkpoint above before code edits; explicitly approve any narrow exception. Include reversibility and the P7 block in an ADR or owner-approved decision record.
- [ ] Verify T01–T03 Task/Final gate implementation, immutable SQL logical IDs and approval/cleanup safeguards at the actual revision. Do not reimplement these features.

**T01 L2 gate:** static project graph / manifest validation; no SQL test run. Evidence: signed-off target table, dependency diagnosis, agreed exception list, before inventory counts by project × TFM.

### NET8-PAUSE-T02 — Minimal, reversible project and solution changes

- [ ] For genuine dual-target application/test projects, change `TargetFrameworks` to `TargetFramework` `net48` (WinForms: drop `net8.0-windows`). Keep source and framework-specific code for restoration; do not delete package/API branches or alter business rules.
- [ ] Keep the legacy host. Park the **net8-only AppCore** out of the active build/package path using explicit documented solution/release configuration while preserving the project/source. Verify no active net48 project improperly references it.
- [ ] Fix only independently identified net8-only tool dependencies without weakening inventory verification. Keep `netstandard2.0` harness. Update project/sln/publish scripts **atomically**, including vendor references/locked restore as needed. Do not hide a broken project behind a silent build exclusion.
- [ ] Confirm `dotnet restore --locked-mode` and Release build of each active project, with the net8 tool host/tests compiled separately if retained; record parked builds as **out of scope**, not passed.

**T02 L2 gate:** Windows locked restore + focused Release builds, compilation/reference-boundary checks. No SQL DDL required. Failure of an essential runner/host compilation is BLOCKED.

### NET8-PAUSE-T03 — Parameterize strict SQL / TESTGATE target selection

- [ ] Implement and test framework arrays, validation and forwarding through the strict PR path, benchmark, CLI and manifest/orchestrator. The strict path must still be **unfiltered**, one pass per active TFM, with watchdog/approval/ownership and reconciliation. Preserve framework flags for future reactivation.
- [ ] Update runner fixture build and gate manifest using a reviewed **migration of execution scope only**. Save historical logical-case TFM information and baseline evidence; prevent test case count shrinking undetected and reject zero selected logical cases.
- [ ] Add negative tests: `net9.0`, duplicate/empty framework list, unsupported declared TFM, stale manifest, missing active SQL owner, mismatch between CLI and Task/Final, net8-only runner accidentally counting as SQL coverage, lost approval, timeout and cleanup failure. Test `net48` success and `net8.0` reactivation selection when compatible binaries are available.
- [ ] Keep the global exclusive SQL lease for all process/TFM combinations, including later restored net8 runs. Never open a full-suite shortcut via a command-line bypass.

**T03 L2 gate:** runner unit tests on their actual runnable framework plus manifest negative tests. Use focused approved SQL only for newly changed SQL lifecycle behavior; defer full SQL until T05.

### NET8-PAUSE-T04 — Align documentation, packaging and future plans

- [ ] Update `AGENTS.md`, `README.md`, `docs/TECH_STACK.md`, `docs/PROJECT_STRUCTURE.md`, `docs/TEST_STRATEGY.md`, `docs/CODEX_TEST_EXECUTION_POLICY.md`, `docs/TEST_GATE_MATRIX.md`, `tests/README.md`, release scripts/runbooks and plan examples to distinguish **active** from **historical** TFM requirements.
- [ ] Update T04/T05 modular plan, SEC-REBUILD and remaining P5/P6 tasks: use the actual *active* test matrix **only after** this plan is implemented; do not erase historical dual-TFM baseline/case keys. Mark parked modern GUI/runtime artifacts as NOT RUN, not PASS.
- [ ] Make P7's `net8.0` server/API explicitly dependent on restoring .NET 8 (or a separately approved alternate framework plan). Local v1 release claims must match only shipped/verifiable targets, with an explicit owner decision about temporarily excluding AppCore.
- [ ] Record an explicit, reviewed `reactivate-net8` procedure, not an assumed one-line toggle.

**T04 L2 gate:** static docs/link/reference consistency, build/package configuration inspection and negative manifest checks. Documentation-only changes do not require SQL re-execution by themselves.

### NET8-PAUSE-T05 — Frozen one-pass acceptance and restore drill

- [ ] Freeze gate-affecting inputs (projects, build scripts, runner, manifests, tests/theory, SQL/migrations). Run affected focused L2, then **one planned** locally approved manifest-governed Final gate: all mandatory active projects and **entire unfiltered net48 SQL test inventory exactly once**, zero skips; preserve TRX/discovery/source bijection, process exit, watchdog, SQL owner journal and cleanup.
- [ ] Run approved non-SQL Application/Desktop/Architecture/runner gates on their **actual active TFMs**, bounded STA GUI checks and build/package checks. Record parked modern-host evidence as NOT RUN with the accepted temporary scope.
- [ ] Drill reactivation in an isolated branch/worktree: restore `net48;net8.0`, `net48;net8.0-windows`, tool/manifest framework arrays and modern-host build; prove source/discovery logical keys for `net8.0` still map correctly. Do not claim dual-TFM release quality without executing its full gate.
- [ ] Compare duration (same environment and protocol, with setup/outliers explained); **do not claim a fixed 50% speedup**. Publish reviewed sanitized aggregates only; no raw TRX/SQL identifiers/approval journals.
- [ ] If the final revision changes gate inputs after passing, rerun the relevant mandatory Final gate; do not reuse stale acceptance artifacts. SQL/Windows unavailable is BLOCKED/NOT RUN.

**T05 exit:** reduced-TFM execution is effective only after the complete scoped gate is verified at the final relevant revision and the restoration steps are tested. A failed gate is never waived solely to improve speed.

## 5. Activation, rollback and re-enable conditions

- **Not yet active:** until an implementation PR passes T01–T05, all present dual-TFM requirements in the existing TESTGATE and other plans remain mandatory. This plan alone neither edits `.csproj` nor authorizes anyone to mark a missing framework PASS.
- **Temporary active window:** once implemented and accepted, Codex uses net48 for application/SQL/WinForms behavior; net8-only tooling remains a *tooling-only* exception if proven needed. Historical dual-TFM verified evidence is retained separately; .NET 8 UI packaging is out of release scope unless it is restored and verified.
- **Mandatory restoration before P7:** the initial P7 `Server.Api net8.0` and modern WinForms parity work cannot start as a claimed supported runtime until target graph/manifest/runtime/tool gates are re-enabled and verified. Also reactivate earlier if a feature specifically requires `net8.0` or the product owner explicitly requests dual-target shipping.
- **Rollback trigger:** incompatible tool/reference graph, loss of strict SQL inventory guarantees, unbounded test process, unexplained behavior drift, unacceptable release target coverage or a failing reduced-TFM L3. Restore baseline target declarations/configuration and re-run affected exact gates; retain evidence of the aborted attempt.
- **Guardrail:** active-target reduction changes what is presently required, **not** the definition or correctness of any test. No auto-green or suppression by removing rows or classifiers from test inventory.

## 6. Task gate summary / handoff

| Task | L1/L2 feedback | SQL scope | Mandatory exit |
|---|---|---|---|
| T01 | Static project graph and baseline audit | None | Approved exception/compatibility decision |
| T02 | Targeted locked restore/Release build | None | Coherent build + parked host documented |
| T03 | Runner/manifest unit and negative tests | Focused only if SQL lifecycle changed | Framework-selectable strict gate without bypass |
| T04 | Documentation, packaging and link checks | None unless code also changed | No contradictory active/historical target claims |
| T05 | Frozen actual-project regression and restore drill | **One** strict unfiltered active `net48` acceptance | Exact logical inventory, watchdog, ownership, approval and cleanup green |

**Implementation handoff:** report changed project matrix, all exact commands, elapsed times, source/discovery/TRX counts, framework/host exclusions, test status `PASS/FAIL/BLOCKED/NOT RUN`, cleanup and restoration proof. Keep this file active until acceptance has evidence, then archive it with its final status.
