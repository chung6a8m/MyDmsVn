# WEB5 B04-A — protected hosted SQL baseline

**2026-11-02: implementation in progress; hosted deliberate RED verified on21e8685; complete GREEN repeats still pending after guard correction. B04-A is not yet PASS. B04-B remains PLANNED / NOT RUN; B04 is incomplete.** [Owning plan](../../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04) · [runbook/trust and cleanup design](../../development/WEB5_SQL_CI_BASELINE.md).

## Candidate and scope

Backend worktree branch `codex/web5-b04a-sql-ci-foundation`, starting clean main `fc36c879fdd2d46ee77d136bdb6f9e70f7191b64`. Existing WEB4 test source is separately pinned to that same SHA. B01 PR #88 is unmerged and not imported. No production code, migration, permission keys, dependency lock or canonical 48-blob corpus changes. No frontend, CEX, B02, IIS or production work.

## Local control validation

- PowerShell 7 evidence guard: 13 focused checks passed. A deliberate wrong-value RED recognition case first failed (expected 250 incorrectly matched 25); the matcher was narrowed to complete numeric lines, then all 13 passed. This tests the guard, not SQL behavior.
- Windows PowerShell 5.1 parsed all three scripts without syntax errors.
- Local Inventory discovery only: 7 expanded cases from the exact baseline. No local SQL bodies executed for B04-A.
- Hosted runner inventory initially showed 0 self-hosted runners and no configured environments. Selected hosted ephemeral private LocalDB, avoiding persistent/shared SQL credentials.

## Hosted evidence

Environment configured and read back: custom deployment policy ID65189351, tags web5-sql-baseline-* only, 0 secrets. actionlint 1.7.7 passed. Independent review identified interrupted-check quarantine loss; persisted Checking/Completing phases now close that boundary. Actual hosted runs remain pending. Do not infer a SQL PASS from source counts, pure guard tests or this document.

## Retained setup failures (not the deliberate SQL RED)

- [Run41403872600](https://github.com/chung6a8m/MyDmsVn/actions/runs/41403872600), control `f6893632a5c0744e88f2cb903f22a9141e6913ec`: allocation stopped before SQL tests; no safe instance artifact existed. No PASS/clean-teardown claim.
- [Run41404071074](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404071074), control `80cf4c3530e1b78dd0feefb062f40f84482c0175`: bounded diagnostics identified version discovery. SqlLocalDB emits a product label plus parenthesized version. Numeric extraction fixed it; local read-only installed-version inspection reproduced that format. No database was allocated by these two runs.
- [Run41404293028](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404293028), control `b84f65ac0a0907325dfb83657b858c11fae72e62`: created private instance, then failed before opening the master connection because PowerShell 5.1 adapts SqlConnectionStringBuilder as IDictionary (`DataSource` is not the canonical `Data Source` key). State stayed Quarantined; Complete refused removal, and the job failed. No user tests ran, no clean teardown claimed. The isolated VM was the containment boundary; no unowned cleanup attempted.
- Control `21e8685b52b54e4603457c1a53e19f0d731de919` uses canonical builder keys. The exact Framework builder/connection constructor was verified locally without opening SQL. Owner commands also now run from the baseline directory so global.json controls the SDK. A second source review found 0 remaining P0/P1/P2.

## Deliberate SQL oracle RED (verified)

[Run41404563067](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404563067), job125143549499, tag `web5-sql-baseline-red-20261102-04`, control `21e8685b52b54e4603457c1a53e19f0d731de919`: actual SQL test discovered 1 / executed 1 / passed 0 / failed 1 / skipped 0. The guard recognized precisely Assert.Equal expected 25 / actual 24 (`oracleRedObserved=true`); workflow conclusion FAILURE. This is the required deliberate oracle RED, distinct from the setup failures above. Security/Migrations are intentionally not selected in RED mode, not skipped required cases of a GREEN run.

Windows Server 2022 hosted image `20261026.347.1`, SQL Express LocalDB `15.0.4382.1`, 64-bit, model compatibility 140, ownership verified. Final phase Removed, user databases 0, active owners 0, quarantine false. [Sanitized instance proof](b04a/red-41404563067/instance.json) and [actual result counts](b04a/red-41404563067/inventory.json) retained; original artifact13122324079 ZIP SHA256 `7a665aa97444b03d9058e1129870893cd261cf7521abe18415cb0c4ae85b3ba0` verified on download. No raw logs/TRX/connection/token retained.

Existing secret-free CI at that control SHA: [push41404552356](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404552356) and [PR41404558404](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404558404) both SUCCESS. Final documentation HEAD CI will be checked separately.

## Discovery identity correction

[First GREEN attempt41404828973](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404828973), same control21e8685: Security79/79 and Inventory7/7 PASS with clean inter-owner checks; Migrations evidence guard rejected duplicate display names before counting TRX. Overall FAILURE and terminal Quarantined; no GREEN/clean-final-teardown claim. Recorded zero resource counts are the last successful inter-owner observation, not a fresh quarantine cleanup proof.

Local discovery (no test bodies) reproduced17 Migration cases but16 unique labels: two InlineData SQL strings are truncated identically by xUnit. Added a duplicate-label/distinct-ID regression which first failed, then changed the guard to compare name multisets and require unique actual TRX testId/executionId GUIDs plus exact definition/execution mapping. Duplicate case IDs still fail. All14 pure guard checks passed. Independent review checked the pinned xunit2.9.3/adapter3.1.5 ID derivation and found no remaining P0/P1/P2; full serialized arguments determine identity, not display text.

An additional local metadata-only probe explicitly cleared the SQL connection environment:12 cases SKIPPED (theory-level skip collapses row enumeration),0 SQL bodies executed. This was not a gate PASS and was not used for runtime counts. Actual hosted Migration17-case reconciliation and two complete GREEN runs remain required.
