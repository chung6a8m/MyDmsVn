# WEB5 B04-A — protected hosted SQL baseline

**2026-11-02: B04-A VERIFIED NONPRODUCTION (bounded): deliberate RED and two complete GREEN runs on the same pinned candidate. B04-B remains PLANNED / NOT RUN; B04 is incomplete.** [Owning plan](../../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04) · [runbook and trust/cleanup design](../../development/WEB5_SQL_CI_BASELINE.md) · [draft PR #89](https://github.com/chung6a8m/MyDmsVn/pull/89).

## Candidate, environment and scope

- Isolated branch `codex/web5-b04a-sql-ci-foundation`, starting clean main and immutable test baseline `fc36c879fdd2d46ee77d136bdb6f9e70f7191b64`. B01 PR #88 remains separate/unmerged; its process-crash tests were not imported.
- Final CI/control source `a27f1c93c37778ac3ea3d996262a872181492fde`. Qualification RED and both GREEN tags target this same commit; the test checkout remains the same pinned WEB4 source. Subsequent changes are documentation/sanitized evidence only.
- Real GitHub-hosted Windows Server 2022 VM per run, SQL Express LocalDB 2019 `15.0.4382.1` 64-bit, owned model compatibility 140, current Windows SID ownership verified; setup-dotnet reports SDK 8.0.427 and commands resolve `baseline/global.json`. Runtime target net48. The run header reports image 20261025.346.1 while the ImageVersion environment reports 20261026.347.1; both observed labels are recorded without assuming they are identical.
- [Environment readback](b04a/environment-policy.json): repository 1409766698, environment `web5-sql-baseline`, tag-only deployment policy 65189351 for `web5-sql-baseline-*`, 0 environment secrets. Workflow additionally requires Owner actor/triggering actor, immutable checkouts and read-only token; no fork/PR SQL trigger.
- Production source, migrations, permission keys, dependency locks, existing SQL-free CI, frontend and CEX unchanged. WEB4 corpus remains 48 blobs, tree `fcab4a1e7505b150a132ce2899002930e9f2a6ee`.

Control Git blobs (unchanged by final documentation):

| File | Git blob |
|---|---|
| `.github/workflows/backend-web-sql-baseline.yml` | `ac68fa15179dee53d71368c15b16ec667458461a` |
| `.github/scripts/Web5SqlInstance.ps1` | `183fb67d0fb45678738f266d58830c16a7671a92` |
| `.github/scripts/Assert-Web5SqlEvidence.ps1` | `ebab273b8872fd1445b1053fd91f135af7164d29` |
| `.github/scripts/Test-Web5SqlEvidence.ps1` | `063ac1dc6e26d54e9578039fd083fc6f83786079` |

## Hosted qualification

All three runs use ordinary locked restore/build, filtered `dotnet test --list-tests`, then filtered `dotnet test -c Release -f net48 --no-build --no-restore --logger trx` with VSTest hang bounds. Exact commands/selectors are in the [workflow](../../../.github/workflows/backend-web-sql-baseline.yml), with owners sequential. Required class counts on GREEN are journal22, status24, HTTP17, principal16, Inventory7 and Migrations17. Every required row must have a unique TRX case/execution identity and matching definition, reconcile the discovery-name multiset, pass and never skip. Raw TRX/logs remain on the disposable VM.

| Run / tag suffix | Job | Actual discovery / unique cases / execution | Outcomes | Lifecycle |
|---|---|---|---|---|
| [RED41406187517](https://github.com/chung6a8m/MyDmsVn/actions/runs/41406187517), `red-20261102-05` |125148622578|1/1/1|0PASS,1expected FAIL,0skip; workflow FAILURE|Removed,0user DB/owners|
| [GREEN41406443916](https://github.com/chung6a8m/MyDmsVn/actions/runs/41406443916), `green-20261102-02` |125149424781|Security79/79/79; Inventory7/7/7; Migrations17/17/17|103PASS,0fail,0skip; SUCCESS|Removed,0user DB/owners|
| [Repeat41407254862](https://github.com/chung6a8m/MyDmsVn/actions/runs/41407254862), `green-20261102-03` |125151985219|Security79/79/79; Inventory7/7/7; Migrations17/17/17|103PASS,0fail,0skip; SUCCESS|Removed,0user DB/owners|

RED changes only one oracle assertion in its disposable checkout from expected24m to25m, selects that actual six-command SQL test and recognizes exactly Assert.Equal expected25/actual24. The gate remains failed; this is not a swallowed exception or synthetic PASS. GREEN has an unchanged tracked baseline. Every successful inter-owner/lifecycle check verifies SID, server/master allocation identity and random ownership token, then no user database, SQL owner/transaction or testhost/migrator worker. Final removal is graceful and never a forced DB drop.

Sanitized records retained in this repository:

| Run | Records | Original artifact / verified ZIP SHA256 |
|---|---|---|
| Final RED | [instance](b04a/red-41406187517/instance.json), [counts](b04a/red-41406187517/inventory.json) |13122989360 / `b86a10ef38a0801d78090d7b842d5c4a011ebab43adc35ab83ad6deea1785276`|
| First GREEN | [instance](b04a/green-41406443916/instance.json), [Security](b04a/green-41406443916/security.json), [Inventory](b04a/green-41406443916/inventory.json), [Migrations](b04a/green-41406443916/migrations.json) |13123562959 / `68199b0b313c0bac949bbffcc52dc6e3edffee5a882e9c0df9f00ed0cd3e32d8`|
| Repeat GREEN | [instance](b04a/green-41407254862/instance.json), [Security](b04a/green-41407254862/security.json), [Inventory](b04a/green-41407254862/inventory.json), [Migrations](b04a/green-41407254862/migrations.json) |13124595475 / `724c2951d3aa0af2508841ffe45ca2bab2e197bb0ad360070833764eb7f87754`|

Archive contents were restricted to the expected JSON names and checked against actual run/control/source pins and counters. No connection string, raw SQL identifier, SID/token, error body, raw TRX, dump or credential is retained. Environment protection plus actor restrictions provide the protected opt-in lane; repository administrators remain trusted.

## Control validation and existing CI

- `pwsh -NoProfile -File .github/scripts/Test-Web5SqlEvidence.ps1`: 14 focused checks PASS. Known RED classification first exposed an expected 250 substring bug, fixed by exact numeric lines. The later duplicate-display-name regression first failed, then passed with unique actual TRX IDs and matching definitions; duplicate case IDs still fail.
- Windows PowerShell 5.1 syntax inspection: all three scripts PASS. Framework connection builder/constructor checked locally without opening SQL. `actionlint 1.7.7` and `git diff --check` PASS.
- Local discovery only: Inventory7, Migrations17 (16unique display labels). A separate metadata probe explicitly cleared the SQL connection:12SKIPPED because theory-level skip suppresses row expansion,0SQL bodies. This was diagnostic metadata, never SQL acceptance or a GREEN gate.
- Existing secret-free CI on final control: [push41406182389](https://github.com/chung6a8m/MyDmsVn/actions/runs/41406182389) and [PR41406191060](https://github.com/chung6a8m/MyDmsVn/actions/runs/41406191060) SUCCESS:506 HTTP +62 recovery validators executed/passed,0failed/skipped in actual TRX summaries. Final documentation HEAD CI is also required before handoff; exact links belong to PR checks/body without changing the qualified control blobs.
- Independent whole-branch and follow-up reviews found 0 remaining actionable P0/P1/P2. An interrupted-check finding was fixed by persisting Checking/Completing before observations/removal; interrupted states cannot return to Ready through a later cleanup. The theory-ID correction was checked against pinned xunit2.9.3/adapter3.1.5 source. Reviewers did not execute SQL or invent runtime evidence.

## Retained failures before qualification

| Run / control | Failure and disposition |
|---|---|
| [41403872600](https://github.com/chung6a8m/MyDmsVn/actions/runs/41403872600) / `f6893632a5c0744e88f2cb903f22a9141e6913ec` | Stopped before SQL tests/allocation at version discovery; no clean-teardown/PASS claim. |
| [41404071074](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404071074) / `80cf4c3530e1b78dd0feefb062f40f84482c0175` | Bounded diagnostics exposed product-label version output; numeric extraction fixed it, reproduced by read-only local version inspection. No database allocated. |
| [41404293028](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404293028) / `b84f65ac0a0907325dfb83657b858c11fae72e62` | Private instance created, then PowerShell 5.1 IDictionary adaptation rejected DataSource before connection. Canonical builder keys fixed it. Quarantined; Complete refused removal. No user test or clean teardown claim; isolated VM is containment. |
| [41404563067](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404563067) / `21e8685b52b54e4603457c1a53e19f0d731de919` | Earlier valid oracle RED:1case failed25vs24, Removed/0resources. [Counts](b04a/red-41404563067/inventory.json), [instance](b04a/red-41404563067/instance.json); artifact13122324079 digest `7a665aa97444b03d9058e1129870893cd261cf7521abe18415cb0c4ae85b3ba0`. Requalified after guard correction. |
| [41404828973](https://github.com/chung6a8m/MyDmsVn/actions/runs/41404828973) / `21e8685` | Security79 and Inventory7 PASS; Migrations guard rejected two identical truncated theory labels before reading counters. No qualified Migration result/overall GREEN. Terminal [quarantine](b04a/quarantined-41404828973/instance.json) retained; recorded0resource counts are the last successful inter-owner observation, not fresh cleanup proof. Artifact13123430976 digest `4c1779557dec5e3d67974e67ee6d3304283f2a9d8fbafdcbdff8ee5821a9eddd`. |

No failed/uncertain instance was adopted or force-cleaned. Only new explicitly tagged runs received fresh hosted VMs. The hosted Migration guard subsequently reconciled17distinct cases and all17passed.

## Acceptance boundary

B5-CI-01 existing SQL-free CI, B5-CI-02 repeatability/RED, B5-CI-03 protection/fail-closed quarantine and B5-CI-04 baseline prerequisites are VERIFIED NONPRODUCTION within this candidate: two full103-case GREEN runs, zero required skips, known oracle RED and clean qualification teardown. B04-B/B5-CI-05 remains NOT RUN and must select new B02 tests at the exact combined B02 candidate before its acceptance; B04 is not DONE.

This proves the selected WEB4 baseline on real hosted LocalDB 2019 compatibility 140. It does not prove execution on the SQL Server 2017 engine itself, B01 process death, B02 terminal negatives, browser/IIS, operator tooling, production rollout or enabling same-key resend. PR #89 remains draft/unmerged; Owner acceptance and any production decision remain separate.
