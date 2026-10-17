# TESTGATE stabilization — SQL lease lifecycle, early manifest validation, and reduced fingerprint churn

**Status (2026-10-17):** The earlier L2/L3 evidence at `00e374b386c949917cce996d99b60a85cc2e29d3` is historical and was invalidated by the code-bearing reviewer fixes described below. The final reviewer follow-up passed strict clean-commit L3 at `6231ef9402333174bce4f792ebf2d3473107ce6c`: 11/11 gates, 786/786 source/discovered/executed/passed, zero failures/skips, and clean watchdog/ownership. Keep PR #35 Draft for the owner's review.

**Base:** SEC-REBUILD accepted at `0303a91111a514276aa02c6664c25dc0caf58d9e`, merged as PR #34 / `4520c494ce6e48e2b0e2c6e0a1f43cfb337d5033`. The accepted baseline was 11/11 Final gates and 775/775 cases, active SQL `net48`; historical `net8.0` remains parked. The current manifest contains 673 logical cases, 52 gates and 195 exemptions. In SEC-REBUILD, 525 surviving case IDs changed reviewed fingerprints. Those are baseline observations, not pass/fail thresholds for this plan.

## Non-negotiable boundaries

- Preserve the existing approved disposable SQL instance requirement, `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`, operator-managed `MYDMSVN_SQL_TEST_APPROVAL_FILE`, global lease, Windows Job Object, positive owned-database journal/cleanup, real SQL acceptance and zero mandatory skips.
- No production schema/migration, permission, authentication, authorization, or business-layer behavior is modified by TESTGATE stabilization.
- Focused Task results are never represented as unfiltered Final/PR acceptance. A metadata-only preflight never produces PASS evidence for SQL test execution, TRX, or ownership.
- Keep source, expanded discovery and executed TRX reconciliation complete for **every active project/TFM** at the final frozen commit. Do not forge or delete logical cases, hashes, historical identities or lease records to force a green result.
- Runtime test changes and test-runner changes invalidate prior gate-affecting test evidence. Refresh reviewed manifest fingerprints **once after code freeze**, then rerun the complete applicable Final. Preserve original private TRX/journals and publish only sanitized summaries.
- Do not add CI test automation during the current local-only rollout. There is no license to run SQL against production or unapproved databases.

## STAB-T01 — Decouple previous-lease recovery from current-run completion (P0)

**Code surface:** `tests/tools/MyDmsVn.SqlTestRunner/Gates/TestGateCoordinator.cs` plus runner negative tests.

- Track historical `priorLeaseRecovered` separately from `currentRunNeedsRecovery`. An old lease that was successfully and positively reconciled is evidence of past recovery, not a reason to mark the new lease unresolved.
- Mark the *new* lease `Completed` only after current-run ownership can be safely released; retain `RecoveryRequired` when the current run actually has unresolved database ownership or uncooperative child processes.
- Preserve the reporting compatibility field `recoveryRequired` as a conservative historical-or-current indicator for v1 reports; distinguish the internal state. Consider explicit separate reporting fields in a versioned reporting change.
- Add a regression sequence: (a) recovered prior lease, (b) current SQL acceptance with clean ownership/TRX, (c) lease completed and next acquisition doesn't invoke recovery. Add failure/cancellation tests proving current unresolved ownership remains blocked; never remove or overwrite live SQL journals manually.

**Exit:** recovered-prior + clean-current is not perpetually `RecoveryRequired`, and all negative SQL ownership cases still fail closed.

## STAB-T02 — Independent SQL manifest preflight and safe Task/Final semantics (P1)

**Code surface:** `SqlGateManifestPreflight.cs`, `TestGateCoordinator.cs`, `LogicalCaseBinder.cs`, `Program.cs`, `Invoke-TestGate.ps1` and their unit/negative tests.

- Add `-Mode Final -PreflightOnly` to invoke metadata checks on dirty or clean working trees **without SQL approval, SQL test execution or database DDL**. It builds the declared SQL test projects on their actual active TFMs, reads compiled test method identities/fingerprints and performs expanded `dotnet test --list-tests` discovery.
- Run the same SQL manifest preflight automatically **before the first SQL acceptance invocation and before acquiring the global SQL lease**. When it fails, do not launch SQL execution; present bounded diagnostics containing gate/project/TFM and normalized method signatures, not raw Theory arguments, SQL instance identifiers, credentials, approval contents or ownership journals.
- Validate entire project source method ownership, signatures, classification and Fact/Theory shape for Task; require reviewed fingerprints only for the Task's selected methods. Unselected fingerprint drift alone must not block a focused Task. Any unknown/missing/duplicate source method or an unreviewed selected method **still blocks** the Task.
- Final verifies complete project/TFM fingerprints, source/discovery inventory, ownership and all expanded cases. During execution, verify source/discovery **before** executing a DotnetTest gate as well; after execution still reconcile original TRX with the full required inventory.
- A preflight may reveal a pending manifest update; it must never automatically rewrite `reviewedCodeFingerprint` or approve a new test.
- Keep build/discovery commands bounded by current watchdogs; no empty case selections or unsupported targets count as success.

**Exit:** a stale SQL manifest is rejected before any SQL test process or database DDL; selected-only Task is usable during development; full Final remains fail-closed.

## STAB-T03 — Proposed, reviewable manifest delta and stable fingerprint v2 (P2; follow-up)

This is **not** authorized as a silent in-place rewrite of v1. The present code changes retain v1 identity/fingerprint behavior until a separately reviewed migration.

1. Introduce a versioned digest input protocol that normalizes encoding/BOM/newlines and hashes the smallest *provable* source/method dependency closure. If a call graph is unresolved, report why; do not silently discard dependencies or weaken source coverage. Favor conservative explicit dependency boundaries over automatic whole-solution/project closure when safety can be proven independently.
2. Benchmark on a frozen base: unrelated code edits must leave unaffected fingerprints unchanged, while edits to actual transitive behavior must invalidate relevant fingerprints. Include async/iterator, constants, generated code, cross-project methods, and reflection/DI fallbacks. Treat previously incomplete closures as explicit review obligations.
3. Provide a deterministic `propose-manifest-delta` command that emits only a candidate patch plus per-case `Added/Removed/Moved/SignatureChanged/ClassificationChanged/FingerprintChanged/TheoryRowChanged` rationale. Do not write the checked-in manifest or auto-approve deletions/exemptions; require reviewer approval and record exactly which source/TFM revision was audited.
4. Cache source indexing per project/revision and quantify the added preflight overhead versus the cost of failed SQL acceptance. No speed claims without measurements.

**Exit:** independent tests demonstrate both true-positive invalidation and stable hashes for unrelated changes; accepted v1-to-v2 coverage bijection for active TFMs.

## STAB-T04 — Split gate policy, active inventory and historical ledger (P2; follow-up)

- Replace the single large v1 manifest only through a versioned schema migration: separate human-reviewed gate definitions, deterministic per-project/module **active** logical case inventories, and immutable/historically referenced retirement/replacement ledger. Preserve deterministic merging, duplicate-owner rejection and digest provenance.
- Preserve old-to-new logical mappings when normalized method signatures, TFM, theory row keys or classifications change. A CSV rationale alone is not a substitute for machine-reviewable old/new case-key reconciliation where an explicit mapping is required.
- Prove a one-to-one bijection between accepted v1 active cases and v2 active cases after approved intentional removals/additions; preserve parked net8 history without claiming it was executed.
- Have `Final` reject uncovered projects/TFMs, duplicate ownership, incomplete manifests or non-reviewed case deletions.

**Exit:** smaller reviewable files without any loss of active or historical coverage; an explicit migration PR and executable adversarial tests.

## Required negative/acceptance matrix

| Scenario | Before execute | Expected final result |
| --- | --- | --- |
| Missing or modified selected test fingerprint | Task discovery/source preflight | Fail before test execution |
| Modified unselected fingerprint only | Focused Task | Task may run; Final full inventory remains strict |
| New/deleted/duplicate method or wrong owner | Task and Final preflight | Fail closed; no silent count-based acceptance |
| Stale SQL fingerprints or Theory discovery rows | SQL Final preflight | No SQL test process/DDL launched |
| Recovered previous lease + clean current ownership | SQL Final | PASS with **current** lease completed, later run normal |
| Current SQL ownership unresolved/cancellation | SQL Final | BLOCKED/Incomplete, safe recovery required |
| Wrong/expired SQL approval or unapproved instance | SQL Final | Fail before DDL |
| Unfiltered full SQL project suite with complete TRX | Frozen Final | Complete real-SQL acceptance for active `net48` |
| Retired/renamed logical case | Reviewed ledger/mapping | Explicit old-to-new rationale; no unexplained case loss |

## Commands and evidence required before this PR is mergeable

Run on the approved Windows developer machine with matching .NET 8 runner SDK, active net48 targeting pack, and restored locked dependencies. First refresh the test-case/fingerprint inventory under independent review for **the exact frozen source revision**; the new runner tests necessarily change the manifest and must not be treated as covered by the old 775-case evidence.

```powershell
dotnet restore MyDmsVn.sln --locked-mode
dotnet build tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj -c Release --no-restore
dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --no-restore

$root = '.sqlperf/stab-preflight-' + [guid]::NewGuid().ToString('N').Substring(0,12)
& ./tests/tools/Invoke-TestGate.ps1 -Mode Final `
  -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T06' `
  -ArtifactRoot $root -PreflightOnly
```

The preflight is metadata-only and **not** an SQL acceptance certificate. After all gate-affecting edits and manifest review are committed, obtain the ordinary approved Final under a clean exact revision with the operator-managed `-ApprovalFile`; include all non-SQL and unfiltered SQL gates on active `net48`, and required GUI-specific gates if affected. Record real source/discovery/TRX totals, missing/extra identities, previous/current lease status, ownership disposal and watchdog outcomes. No CI job or network test run is claimed. See the verification update below for actual local SQL execution evidence.

**Review blockers at authoring time:** refresh/review fingerprints and new logical test IDs, compile and run unit/negative tests on Windows, run SQL preflight, and complete the frozen-revision strict Final. Until these are green, the implementation PR should remain **Draft / NOT VERIFIED**.

## Reviewer follow-up (2026-10-17)

- Added a shared 240-character Windows compatibility budget covering runner shadow dependencies, preflight output, Task/Focused/SQL TRX, timing, ownership journal temporary files, recovery artifacts, and reports. Final and focused SQL paths are rejected before lease acquisition or SQL/ownership creation when the budget is exceeded.
- Kept logical execution IDs in the SQL environment and ownership records while moving journal, TRX, timing, and recovery files to short physical IDs. `execution-id.txt` maps new physical journal folders back to their logical IDs; recovery falls back to the prior folder-name/recovery-file layout for existing artifacts.
- Task TRX folders also retain their original logical run ID in `execution-id.txt`, while their physical directory and TRX filename stay short.
- SQL manifest preflight builds and discovers from a unique temporary `BaseOutputPath` per invocation. A concurrent-preflight regression holds both builds at the same time and verifies distinct output roots and matching discovery roots.
- SQL preflight outputs now live under the repository's ignored `artifacts` directory so compiled inventory can resolve the repository source root. A temporary `Directory.Build.props` imports the repository's normal props and gives every project in the build graph its own output directory, avoiding package-copy collisions while keeping each invocation isolated. Build failures report the exit/timeout status and at most 240 characters of bounded error details.
- Replaced the runner test project's unconditional 11-project `BeforeTargets="Build"` hooks with an explicit `BuildRepositoryInventoryFixtures` audit target. `Invoke-TestGate.ps1 -Mode Final` runs the target; ordinary filtered runner tests do not.
- Added three runner logical cases and refreshed runner method fingerprints plus the 54 matching incomplete-closure exemptions for the frozen source. The old manifest and all earlier L3 evidence are not accepted for these changes.
- After the reviewer follow-up code freeze, refreshed **191** existing runner logical-case fingerprints and **54** matching incomplete-closure exemptions from the compiled inventory. No logical case was added or removed by this follow-up; owner, TFM, classification, signature, and theory-row identities were retained.

## Verification update (2026-10-17)

The following earlier evidence describes the pre-review revision and is superseded where the reviewer fixes above affect the gate inputs:

- Restored `MyDmsVn.sln` in locked mode after initializing the repository's pinned vendor submodules. The restore exposed six stale lock files for the `Server.Application` Newtonsoft.Json dependency; those lock changes are included with this stabilization update.
- Built the runner test project and its inventory fixtures on active `net48` plus tool-only `net8.0`. The fixture build contract now includes all 11 projects represented by the reviewed inventory.
- Independently reconciled compiled source inventory against v1: **8 Added** runner Fact methods; **0 Removed, SignatureChanged, ClassificationChanged, OwnershipChanged or TheoryRowChanged**; **605 FingerprintChanged**. Existing logical IDs, classifications, framework history and theory-row locators were retained. The fingerprint changes trace to the already merged source-index boundary change that excludes repository metadata/artifact folders; test source methods were unchanged since the prior manifest snapshot. Updated the 195 existing incomplete-closure fingerprints, appended the current reviewed hashes to 42 complete non-SQL exemption entries while retaining their historical hashes, and added one narrowly scoped incomplete-closure exemption for a fake-only stale-fingerprint test. No automated manifest generator was added.
- `dotnet test tests/tools/MyDmsVn.SqlTestRunner.Tests/MyDmsVn.SqlTestRunner.Tests.csproj -c Release -f net8.0 --no-restore`: **244 passed, 0 failed, 0 skipped** (1m 19s). This includes lease lifecycle, Task/Final, manifest contract and runner negative tests.
- `Invoke-TestGate.ps1 -Mode Final -PlanId '20261016-003-permission-system-rebuild' -TaskId 'SEC-REBUILD-T06' -ArtifactRoot <fresh-local-root> -PreflightOnly`: **passed for 6 reviewed project gates**; no SQL tests or database DDL executed.
- The first strict Final attempt used a long artifact path and failed 12 Foundation tests before database creation: the ownership-journal temporary filename exceeded Windows `MAX_PATH` by one character. The ownership journal stayed empty. The next lease acquisition used the runner's guarded path-failure recovery, validated the approved instance, and confirmed there were no owned test databases; no lease, journal or database was manually removed or altered.
- Reran strict Final on the same committed revision with a fresh short local artifact root (maximum sampled ownership path: 237 characters). All **11 gates** passed: **783 source / 783 discovered / 783 executed / 783 passed**, zero failed and zero skipped. All sanitized per-gate reports show complete inventory, clean watchdog and ownership. The local SQL lease record is `Completed`; SQL reports' compatibility `recoveryRequired` flag reflects the recovered prior lease. Raw TRX, timing and ownership artifacts remain local.
- The complete runner suite includes the regression proving that after a recovered prior lease and clean current run, the current lease completes and the next acquisition does not recover it again. Strict L3 completed at the exact committed revision above; leave the PR Draft for the owner's final review.
- The earlier full runner test result (244 tests) predates the review fixes and is retained only as historical context; the updated suite and manifest evidence are recorded in the reviewer follow-up section once the final clean-revision gates complete.

### Reviewer follow-up L3 rerun (2026-10-17, commit `6231ef9402333174bce4f792ebf2d3473107ce6c`)

- Focused path and concurrency regressions passed **7/7**. The runner test gate passed **247/247** on tool-only `net8.0`; the repository source/discovery contract passed within that run.
- Metadata-only `PreflightOnly` passed all **6 reviewed SQL project gates** without executing SQL tests or DDL; strict Final repeated the automatic source/discovery preflight on the exact clean commit before launching SQL.
- The clean-commit strict Final passed all **11 gates**: **786 source / 786 discovered / 786 executed / 786 passed**, with **0 failures and 0 skips**. Each sanitized report records complete inventory, no timeout/resource denial/recovery requirement, and clean watchdog and ownership reconciliation.
- SQL Final ran sequentially on active `net48` under the approved local resource. Raw TRX, timing and ownership journals remain local under `C:\t35-6231ef9`; no CI or parked application `net8.0` execution is claimed. The PR remains Draft for owner review.

### Reviewer round 2 (2026-10-17)

- Recovery now accepts a new-layout journal containing only a valid `execution-id.txt` mapping after an approved-instance identity check confirms zero owned test databases. A path-failure journal still requires positive external database-absence evidence; unexpected or temporary journal files are rejected. Mapping writes are atomic.
- The opt-in 11-project inventory fixture build now sets project-specific `BaseOutputPath`, `BaseIntermediateOutputPath`, `MSBuildProjectExtensionsPath`, and NuGet lock paths under its unique artifact root. It copies each checked-in lock file into that root and restores in locked mode, so it no longer shares normal `bin`/`obj` state or changes the dependency resolution used by a running SQL Final. Discovery reuses the isolated assembly with `--no-build --no-restore` and a resolved test adapter path.
- Added four new runner cases for metadata-only journal recovery and database-absence checks; updated the existing concurrent-build regression to run two full inventory fixture builds at once while locking the normal SQL test assembly and comparing the 11 fixture projects' normal outputs and lock files.
- Focused verification on the working tree: runner suite **252/252 passed** on tool-only `net8.0`; SQL harness preflight validator tests **15/15 passed** on `net48`; concurrent fixture build regression **1/1 passed**; manifest source/discovery contract **1/1 passed** against all 448 compiled methods across eight owners. The manifest now has 689 logical cases and 205 runner cases.
- Metadata-only `PreflightOnly` passed for all **6 reviewed SQL project gates** on the committed source; it executed no SQL tests or database DDL.
- Strict Final passed on exact gate-affecting revision `6f368de812ae3829529dc8ca65f96369f397dfc3`: **11 gates, 791 source / 791 discovered / 791 executed / 791 passed**, with **0 failures and 0 skips**. All sanitized reports record complete inventory, no timeout/resource denial/recovery requirement, and clean watchdog and ownership reconciliation.
- SQL acceptance ran sequentially on active `net48` under the approved local resource. Sanitized aggregate reports and raw local artifacts are under `C:\t35-6f368de`; raw TRX, timing and ownership journals remain local. The runner test project is tool-only `net8.0`; no parked application `net8.0`, CI or GUI manual-acceptance result is claimed. The PR remains Draft for owner review.
