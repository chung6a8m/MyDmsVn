# Test Gate Matrix — current tooling and P4.1 example

**Status:** Human-readable guidance only, 2026-10-16. The proposed gate manifest and `Invoke-TestGate.ps1` DO NOT EXIST yet. See [TESTGATE plan](plans/20261016-001-task-gates-and-modular-tests.md), [Codex policy](CODEX_TEST_EXECUTION_POLICY.md), and [P4.1](plans/20261013-002-desktop-login-session.md). This document does not re-open any already verified P4.1 gate.

## Tool inventory (actually available)

| Scope | Command / project | Acceptance meaning |
|---|---|---|
| Pure Application/contract | `dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~<reviewed type>"` (and `net48` where relevant) | Focused L1/L2 only |
| Desktop/STA | `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~<reviewed type>"` (also `net8.0-windows`) | Focused L1/L2 only; respect serialized GUI |
| SQL category | `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` with `-Category SecurityProvisioning` or another reviewed category, `-Frameworks net48,net8.0`, `-TestProject` the existing integration project, `-ApprovalFile` the approved local file and a unique `-ArtifactRoot` | Focused L1/L2 only; full category may still be broad |
| SQL full regression | `tests/tools/Invoke-SqlIntegrationBenchmark.ps1` (existing multi-pass `baseline`) | Existing strict L3 evidence path, **expensive**; not a single-pass gate |
| Future task/final runner | `Invoke-TestGate.ps1`, manifest, `pr-acceptance` | **Proposed, not executable** |

Replace `<reviewed type>` with a type actually verified in the current source. Never copy a placeholder into an acceptance report as an executed command. Always build/restore the exact applicable revision/config/TFM before using `--no-build` or `--no-restore`.

## Task-scoped guidance for future P4.1 changes

| Task | First run / task L2 | Explicit SQL escalation | Full suite |
|---|---|---|---|
| T01 | Application request actor + pure session state tests | SecurityProvisioning if SQL validator touched; add affected category for actual change | Never at task boundary |
| T02 | Login VM deterministic tests + affected Desktop STA across both GUI TFMs | None if only UI/VM behavior changed | Never at task boundary |
| T03 | Host startup/fail-closed/permission UI + STA | SecurityProvisioning for SQL-backed account validation/auth admission | Never at task boundary |
| T04 | Fake-clock expiry, pending/admitted actor races, logout/relogin, stale UI | SecurityProvisioning; SqlConcurrency only if real SQL session locking exercised | Never at task boundary |
| T05 | Missing focused checks + packaged/manual walkthroughs | Final **unfiltered** approved SQL suite, both TFMs, only after code/tests frozen | L3 once planned; repeat only when invalidated |

A SQL category is broader than the future requested `module + category` selector; do not pretend the current focused tool supports that combination. When a task requires genuinely cross-module validation, add those categories/checks rather than narrowing the gate merely to save time.

## Minimum gate record

Record task ID, source/TFM(s), selection rationale, exact command, stable test inventory, SQL approval (without secret values), pass/fail/skip totals, timeout/cleanup, artifact reference, revision and any blocked/manual work. For L3 require unfiltered current-revision discovery/TRX reconciliation, zero mandatory skips, approved instance and complete owned-database cleanup. A code change after L3 invalidates affected evidence.
