# Test Gate Matrix — current tooling and P4.1 example

**Status:** Human-readable guidance, 2026-10-16. The v1 manifest and local `Invoke-TestGate.ps1` Task/Final runner are implemented and TESTGATE-T03 Final L3 is verified at its recorded frozen revision. T04/T05 split projects remain proposed. See [TESTGATE T01–T03](plans/20261016-001-task-gates-and-modular-tests.md), [T04/T05](plans/20261016-002-modular-test-projects.md), and [Codex policy](CODEX_TEST_EXECUTION_POLICY.md).

**Upcoming (not yet implemented):** [NET8-PAUSE](plans/20261016-001-task-gates-and-modular-tests-post-with-temporary-remove-net8.md) requires adding explicit `-Frameworks`/`--frameworks` selection to strict SQL acceptance and benchmark tooling without removing the existing focused `-Frameworks` parameter. None of these **new strict parameters** exists yet. **The tool inventory and runnable commands below describe the current pre-pause dual-TFM repository only.** `main`/unrelated PRs keep that dual matrix until the dedicated NET8-PAUSE implementation merges; only its owner-approved **Stage B candidate**, after T01/T02/T03, may use the verified full **active `net48` Final** plus bounded GUI/Release gates. Later work binds to the merged active project × TFM manifest (net8 PARKED/NOT RUN until restored). A net8-only runner's own TFM must never count as a second SQL test pass.

## Tool inventory (actually available)

| Scope | Command / project | Acceptance meaning |
|---|---|---|
| Pure Application/contract | `dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~<reviewed type>"` (and `net48` where relevant) | Focused L1/L2 only |
| Desktop/STA | `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~<reviewed type>"` (also `net8.0-windows`) | Focused L1/L2 only; respect serialized GUI |
| SQL category | `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` with all mandatory parameters; use the runnable PowerShell example below and a reviewed category | Focused L1/L2 only; full category may still be broad |
| SQL PR acceptance | `tests/tools/Invoke-SqlPrAcceptance.ps1` (`pr-acceptance`) | Strict one-pass sequential unfiltered L3 gate on both SQL TFMs |
| SQL performance baseline | `tests/tools/Invoke-SqlIntegrationBenchmark.ps1` (`baseline`) | Specialized multi-pass L4 measurement path |
| Manifest task/final runner | `tests/tools/Invoke-TestGate.ps1` + `tests/test-gates.v1.json` | Exact local Task selection; Final clean-revision runner with serialized strict dual-TFM SQL and sanitized aggregate evidence |

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
  -Frameworks @('net48', 'net8.0')
```

This is a **category-filtered L1/L2 check only**, not L3 acceptance. Verify current selectors/category against source and discovery; choose a different unique artifact directory for each invocation. Do not use `--no-build` after changing runner code without rebuilding it. Raw generated artifacts remain local; publish only the sanitized summary described in [Codex policy](CODEX_TEST_EXECUTION_POLICY.md).

### Manifest-owned TESTGATE commands

Build the runner/tests for the exact revision first. Task mode needs no SQL approval and accepts no project/TFM/filter override:

```powershell
tests/tools/Invoke-TestGate.ps1 -Mode Task -PlanId '20261016-001' -TaskId 'TESTGATE-T03' -ArtifactRoot '.sqlperf/testgate-t03-task'
```

Final mode additionally requires `-ApprovalFile`; the wrapper rejects a dirty tree, binds evidence to `HEAD`, serializes access to the approved instance, then runs the manifest order. Exit codes are `0` complete, `2` resource/preflight denied, `3` incomplete, and `64` usage error.

Replace `<reviewed type>` with a type actually verified in the current source. Never copy a placeholder into an acceptance report as an executed command. Always build/restore the exact applicable revision/config/TFM before using `--no-build` or `--no-restore`.

## Task-scoped guidance for future P4.1 changes

**Historical worked example only:** P4.1 was owner-accepted and is not being reopened. For a future change to that feature, resolve the **actual approved active project × TFM** first; the original two-target evidence and example commands are historical, not instructions to run a target removed by NET8-PAUSE. The current dual-target matrix remains mandatory on `main`/unrelated PRs until the pause implementation merges; only its authorized Stage B candidate uses active `net48`, with restored modern-target tests required when net8 is reactivated.

| Task | First run / task L2 | Explicit SQL escalation | Full suite |
|---|---|---|---|
| T01 | Application request actor + pure session state tests | SecurityProvisioning if SQL validator touched; add affected category for actual change | Never at task boundary |
| T02 | Login VM deterministic tests + affected Desktop STA on **all approved active GUI TFMs** (pre-pause `net48` + `net8.0-windows`; Stage B/paused `net48`) | None if only UI/VM behavior changed | Never at task boundary |
| T03 | Host startup/fail-closed/permission UI + STA | SecurityProvisioning for SQL-backed account validation/auth admission | Never at task boundary |
| T04 | Fake-clock expiry, pending/admitted actor races, logout/relogin, stale UI | SecurityProvisioning; SqlConcurrency only if real SQL session locking exercised | Never at task boundary |
| T05 | Missing focused checks + packaged/manual walkthroughs for shipped hosts | Final **unfiltered SQL on all approved active TFMs** (pre-pause dual; authorized Stage B/paused `net48`) only after gate inputs freeze; require guarded GUI/Release evidence for active hosts | L3 once planned; repeat only when invalidated |

A SQL category is broader than the future requested `module + category` selector; do not pretend the current focused tool supports that combination. When a task requires genuinely cross-module validation, add those categories/checks rather than narrowing the gate merely to save time.

## Minimum gate record

Record task ID, source/TFM(s), selection rationale, exact command, stable test inventory, SQL approval **result** (not approval identifiers or secret values), pass/fail/skip totals, timeout/cleanup, sanitized evidence reference, revision and any blocked/manual work. Retain raw artifacts locally; do not publish unreviewed TRX, approval files, journals or ZIP bundles. For L3 require unfiltered current-revision discovery/TRX reconciliation, zero mandatory skips, approved instance and complete owned-database cleanup. A code change after L3 invalidates affected evidence.
