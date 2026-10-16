# Test Gate Matrix — current tooling and P4.1 example

**Status:** Human-readable guidance, 2026-10-16. The v1 manifest and local `Invoke-TestGate.ps1` Task/Final runner are implemented and TESTGATE-T03 Final L3 is verified at its recorded frozen revision. T04/T05 split projects remain proposed. See [TESTGATE T01–T03](plans/20261016-001-task-gates-and-modular-tests.md), [T04/T05](plans/20261016-002-modular-test-projects.md), and [Codex policy](CODEX_TEST_EXECUTION_POLICY.md).

**NET8-PAUSE accepted matrix:** explicit `-Frameworks`/`--frameworks` selection is implemented for focused, strict PR and benchmark paths. PR #32 merged at `8e310778`; current `main` binds SQL and Desktop owners to active `net48`, preserves historical net8 inventory, and records runner projects as tool-only `net8.0`. The owner waived the same-protocol duration comparison at `09d00bfa` because no comparable baseline exists; it remains unmeasured and no speedup is claimed. A net8-only runner never counts as SQL/product coverage.

## Tool inventory (actually available)

| Scope | Command / project | Acceptance meaning |
|---|---|---|
| Pure Application/contract | `dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~<reviewed type>"` | Focused L1/L2 only |
| Desktop/STA | reviewed focused `net48` selectors; full D2 only through `Invoke-GuiTestAcceptance.ps1` | Focused L1/L2 or bounded D2 as applicable |
| SQL category | `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` with all mandatory parameters; use the runnable PowerShell example below and a reviewed category | Focused L1/L2 only; full category may still be broad |
| SQL PR acceptance | `Invoke-SqlPrAcceptance.ps1 -Frameworks @('net48')` | Lower-level strict one-pass unfiltered candidate path; manifest Final owns acceptance |
| SQL performance baseline | `Invoke-SqlIntegrationBenchmark.ps1 -Frameworks <reviewed-array>` | Specialized multi-pass L4 measurement path |
| Manifest task/final runner | `Invoke-TestGate.ps1` + `tests/test-gates.v1.json` | Exact Task selection; candidate Final binds clean revision to active net48 SQL |
| Desktop GUI D2 | `Invoke-GuiTestAcceptance.ps1` | Clean-revision unfiltered net48 source/discovery/TRX with GUI lease and process-tree watchdog |

### Existing, runnable SQL L2 example (PowerShell from repository root)

Prerequisites: Windows/.NET SDK and both SQL test TFMs available, `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` set to the **approved non-production** instance with effective database `master`, and `MYDMSVN_SQL_TEST_APPROVAL_FILE` pointing to a valid operator-maintained local file. Never print these values or run destructive tests against an unapproved instance. The wrapper invokes its runner with `dotnet run --no-build`, so build the **runner** in Release first; also build the actual SQL test project for the relevant TFMs. Use a new artifact root per run.

```powershell
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release
dotnet build tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release
tests/tools/Invoke-FocusedSqlIntegrationTests.ps1 `
  -RepositoryRoot (Get-Location) `
  -TestProject tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -ArtifactRoot .sqlperf/focused-security-l2-001 `
  -ApprovalFile $env:MYDMSVN_SQL_TEST_APPROVAL_FILE `
  -Category SecurityProvisioning `
  -Frameworks @('net48')
```

This is a **category-filtered L1/L2 check only**, not L3 acceptance. Verify current selectors/category against source and discovery; choose a different unique artifact directory for each invocation. Do not use `--no-build` after changing runner code without rebuilding it. Raw generated artifacts remain local; publish only the sanitized summary described in [Codex policy](CODEX_TEST_EXECUTION_POLICY.md).

### Manifest-owned TESTGATE commands

Build the runner/tests for the exact revision first. Task mode needs no SQL approval and accepts no project/TFM/filter override:

```powershell
tests/tools/Invoke-TestGate.ps1 -Mode Task `
  -PlanId '20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8' `
  -TaskId 'NET8-PAUSE-T03' -ArtifactRoot '.sqlperf/net8-pause-t03-task'
```

Final mode additionally requires `-ApprovalFile`; the wrapper rejects a dirty tree, binds evidence to `HEAD`, serializes access to the approved instance, then runs the manifest order. Exit codes are `0` complete, `2` resource/preflight denied, `3` incomplete, and `64` usage error.

Replace `<reviewed type>` with a type actually verified in the current source. Never copy a placeholder into an acceptance report as an executed command. Always build/restore the exact applicable revision/config/TFM before using `--no-build` or `--no-restore`.

## Task-scoped guidance for future P4.1 changes

**Historical worked example only:** P4.1 was owner-accepted and is not being reopened. For a future change to that feature, resolve the **actual approved active project × TFM** first; the original two-target evidence and example commands are historical, not instructions to run a target removed by NET8-PAUSE. Current `main` uses the accepted active `net48` matrix; branches created before PR #32 merged follow their checked-in targets until reviewed reactivation. Restored modern-target tests are required when net8 is reactivated.

| Task | First run / task L2 | Explicit SQL escalation | Full suite |
|---|---|---|---|
| T01 | Application request actor + pure session state tests | SecurityProvisioning if SQL validator touched; add affected category for actual change | Never at task boundary |
| T02 | Login VM deterministic tests + affected Desktop STA on **all approved active GUI TFMs** (`net48` on current `main`; checked-in targets on pre-merge branches) | None if only UI/VM behavior changed | Never at task boundary |
| T03 | Host startup/fail-closed/permission UI + STA | SecurityProvisioning for SQL-backed account validation/auth admission | Never at task boundary |
| T04 | Fake-clock expiry, pending/admitted actor races, logout/relogin, stale UI | SecurityProvisioning; SqlConcurrency only if real SQL session locking exercised | Never at task boundary |
| T05 | Missing focused checks + packaged/manual walkthroughs for shipped hosts | Final **unfiltered SQL on all approved active TFMs** (`net48` on current `main`; checked-in targets on pre-merge branches) only after gate inputs freeze; require guarded GUI/Release evidence for active hosts | L3 once planned; repeat only when invalidated |

A SQL category is broader than the future requested `module + category` selector; do not pretend the current focused tool supports that combination. When a task requires genuinely cross-module validation, add those categories/checks rather than narrowing the gate merely to save time.

## Minimum gate record

Record task ID, source/TFM(s), selection rationale, exact command, stable test inventory, SQL approval **result** (not approval identifiers or secret values), pass/fail/skip totals, timeout/cleanup, sanitized evidence reference, revision and any blocked/manual work. Retain raw artifacts locally; do not publish unreviewed TRX, approval files, journals or ZIP bundles. For L3 require unfiltered current-revision discovery/TRX reconciliation, zero mandatory skips, approved instance and complete owned-database cleanup. A code change after L3 invalidates affected evidence.
