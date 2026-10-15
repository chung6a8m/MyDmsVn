# SQLPERF-T02 / T03 verification

**Date:** 2026-10-15  
**Base:** `2a139c258027f336cb1ded66a071a68c491ce08b`  
**Candidate implementation:** `4cd78f62c8f2d7ace06ea8df2de570492f39ac73`  
**Environment:** Windows, approved non-production SQL Server, `net48` and `net8.0`

This evidence closes SQLPERF-T02 and SQLPERF-T03 only. It does not complete P4.1, authenticated P5 acceptance, P6, SQLPERF-T04, SQLPERF-T05 or SQLPERF-T06.

## Implemented changes

SQLPERF-T02 replaced the three test-trigger helpers that used `WAITFOR DELAY '00:00:05'` with a unique per-scenario application-lock gate. A dedicated controller holds a session-owned exclusive APP lock; the business trigger requests the same lock transaction-owned while retaining its intended business locks. A separate observer proves both of these conditions before release:

1. the holder is waiting on the controller APP lock and retains an `IX`, `X`, `U` or `SIX` lock on the expected business table; and
2. the contender is blocked by that holder on the expected non-APP business resource.

The observer has a linked .NET timeout, SQL command timeout and cancellation token. `SqlConcurrencyCleanup` always attempts release, cancellation and controller closure, then requires every started task to complete before trigger/resource/database cleanup. A bounded drain failure raises `SqlConcurrencyCleanupIncompleteException` and deliberately does not tear down the database while tasks may still be using it. Failure-injection tests cover canceled DMV observation after confirmed trigger entry, canceled/retried release, release failure, a permanently stalled task, and departure after confirmed contender blocking but before controller release/transaction commit.

All original business assertions remain: six reference lock-order cases across Warehouses, Employees and Products, same-receipt posting, update/post winner and loser behavior, catalog serialization, StockBalances upsert serialization, idempotence, ledger/balance totals and rollback behavior. No production schema, migration, UoW, authorization or application code changed.

SQLPERF-T03 removed the test harness's global `SqlConnection.ClearAllPools()` call. The disposable database connection string and production migrator connection are non-pooled. The migration test that deliberately enables pooling already owns and clears that exact pool. A new lifecycle regression identifies the physical connection by `ClientConnectionId`, proves unrelated pool preservation, and uses targeted `ClearPool` as a negative control.

No migration reuse or broader test parallelism was introduced. DbUp, all eight shipped migrations, per-test disposable databases, global xUnit serialization, guarded `SINGLE_USER WITH ROLLBACK IMMEDIATE`, approval checks, durable ownership journaling and positive-ownership reconciliation remain unchanged.

## Commands and actual results

The approval and connection values were loaded from the existing operator-maintained User-scope configuration without printing them. The main commands were:

```powershell
dotnet build MyDmsVn.sln -c Release --no-restore

dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj `
  -c Release --no-build --framework '<net48 or net8.0>' `
  --filter '<six modified GoodsReceipt concurrency methods>'

dotnet run --project tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj `
  -c Release --no-build -- preflight

dotnet run --project tests/tools/MyDmsVn.SqlTestRunner/MyDmsVn.SqlTestRunner.csproj `
  -c Release --no-build -- baseline `
  --repo (Get-Location).Path `
  --project (Resolve-Path tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj).Path `
  --artifacts (Join-Path (Get-Location) '.sqlperf/t02-t03-candidate-4cd78f6') `
  --approval $env:MYDMSVN_SQL_TEST_APPROVAL_FILE

dotnet test MyDmsVn.sln -c Release --no-build --no-restore -m:1 `
  --logger "console;verbosity=minimal"
```

| Gate | Actual result |
|---|---|
| Release solution build | Passed, 0 warnings / 0 errors |
| Gate/failure-path tests at benchmark revision | 6 / 6 passed per TFM |
| Six modified GoodsReceipt concurrency tests, 20 consecutive runs | `net48`: 120 / 120; `net8.0`: 120 / 120; zero failed/skipped |
| Strict candidate benchmark | 2 / 2 warm-ups and 6 / 6 measured runs accepted |
| Every benchmark TRX | 220 / 220 passed; zero failed/skipped/not-executed |
| Benchmark ownership reconciliation | 148 / 148 `Disposed` per run; 1,184 / 1,184 across eight runs |
| Full Release regression — Application | 133 / 133 per TFM |
| Full Release regression — Desktop | 200 / 200 per TFM |
| Full Release regression — Architecture | 5 / 5 |
| Full Release regression — Integration | 220 / 220 per TFM at benchmark revision; 222 / 222 per TFM after review fixes |
| Full Release regression — SQL harness | 52 / 52 per TFM |
| Full Release regression — SQL runner | 68 / 68 |
| Review status | Initial independent review had no remaining Critical or Important findings; PR review round 1 later found two P2 failure-path issues, both fixed and verified below |

The accepted benchmark inventory increased from the T01 baseline's 213 cases to 220 because the benchmark revision adds seven gate, failure-path and pool-lifetime tests. The strict runner reconciled source/discovery/TRX identities on every accepted pass. Two review regressions added after that benchmark bring the current integration inventory to 222; the historical benchmark result remains explicitly tied to 220 cases and was not rewritten.

## Review round 1 follow-up

The PR review identified two P2 failure-path races. The transaction-owned `sp_getapplock` had the same 10-second timeout as each observation window, so it could expire before the controller released it. Root `ServiceProvider` instances declared with `using` inside the scenario `try` could also be disposed before the outer cleanup released, canceled and drained the SQL tasks.

The follow-up changes make the holder APP lock wait for controller release or command cancellation (`@LockTimeout = -1`) while retaining the existing 30-second operation cancellation and 15-second cleanup drain watchdogs. Cleanup now owns the root provider and applies the explicit order `release -> cancel -> close controller -> drain tasks -> dispose provider -> guarded database teardown`. All five direct provider-backed concurrency scenarios and the reference lock-order loop use that lifecycle.

Fresh review-fix verification on 2026-10-15:

| Gate | Actual result |
|---|---|
| Mutation check with the former 10-second APP-lock timeout | New delayed-observation regression failed after 11 seconds because the holder had completed; restoring controller-controlled wait made it pass |
| Provider cleanup red/green | Missing cleanup overload failed compilation; after implementation, the regression proved the provider remains alive while a started task is incomplete and is disposed before guarded teardown |
| Affected gate + concurrency subset | 14 / 14 passed on `net48`; 14 / 14 passed on `net8.0` |
| Release solution build | Passed, 0 warnings / 0 errors |
| Full Release regression — Integration | 222 / 222 passed on `net48`; 222 / 222 passed on `net8.0` |
| Other full Release suites | Application 133 / 133 per TFM; Desktop 200 / 200 per TFM; Architecture 5 / 5; SQL harness 52 / 52 per TFM; SQL runner 68 / 68 |
| Cleanup reconciliation | 322 recently written direct-run journal records were `Disposed`; final live disposable-database count was zero |

## Performance evidence and decision

The candidate used the same approved SQL environment and the T01 runner protocol. Because seven tests were added, the full-suite comparison is conservative but not an identical-inventory A/B; SQLPERF-T06 must still perform any final matched comparison required for release closure.

| TFM | T01 baseline wall-clock p50 | Candidate wall-clock p50 | Delta | Candidate p95 |
|---|---:|---:|---:|---:|
| `net48` | 554,981.459 ms | 503,760.024 ms | -9.23% | 506,380.935 ms |
| `net8.0` | 540,718.071 ms | 487,244.744 ms | -9.89% | 492,530.734 ms |

The previously approximately 34-second six-lock-order reference test measured p50 4,949 ms on `net48` and 4,789.589 ms on `net8.0` in the accepted candidate reports. This is the direct T02 saving; the assertions and real SQL blocking observations remain.

T01/candidate stage totals rank migration first, database creation second and database drop third:

| TFM | Candidate migration | Candidate create | Candidate drop |
|---|---:|---:|---:|
| `net48` | 571,903.59 ms | 156,265.36 ms | 34,580.01 ms |
| `net8.0` | 567,041.40 ms | 152,015.60 ms | 31,047.43 ms |

The pool change is adopted for ownership scope and isolation, not claimed as an independently measured speedup. Aggregate drop time was mixed relative to T01 (`net48` higher, `net8.0` lower) while the number of database-owning tests also increased from 143 to 148 per run. The regression test proves global eviction is unnecessary for the known pool topology, and all normal, open/outstanding connection, pooled migration, interrupted migration, retry, mutation-guard, crash-window, torn-journal and competing-run tests passed in the full matrix. Overall candidate wall-clock improved on both TFMs without weakening cleanup.

The original 20–30% engineering goal was not achieved; the measured full-suite improvement is approximately 9–10%. Safety and isolation were retained. DMV report fields remained unavailable, so this evidence makes no wait, CPU, I/O or memory delta claim.

## Cleanup incident and final state

One direct-run journal remained `Created` after an earlier `testhost.net48` process reported success but failed to exit and was force-stopped to release its locked test DLL. Two more `Created` records belonged to candidate benchmark attempts that were intentionally canceled when review found cleanup defects before the final revision. None of these three runs was counted as benchmark evidence. Post-run inspection independently verified each exact guarded name, approved instance preflight, database ID/create timestamp and database ownership marker against its durable record; no owner process remained. `DatabaseOwnershipReconciler` classified each candidate `PositivelyOwned`. Recovery then used each run's own atomic `DropIntent` and `Disposed` journal transitions around guarded `SINGLE_USER WITH ROLLBACK IMMEDIATE` / `DROP DATABASE`, and final live checks returned absent. The default direct-run journal ended with all 1,517 records `Disposed`, both canceled-run records ended `Disposed`, and the final strict live disposable-database count was zero. No prefix-only or ambiguous cleanup was attempted.

## Remaining risks and next task

- The full-suite gain is below the 20–30% engineering target and includes seven additional tests; do not treat it as SQLPERF-T06's final matched release comparison.
- Pool-removal performance is not isolated from inventory and run-to-run variation; the justification is narrower ownership and preserved reliability.
- DMV snapshots were unavailable, so SQL wait/resource changes remain unmeasured.
- SQLPERF-T04 is conditional. T01/T03 still show migration and create cost dominate; decide separately whether fixture reuse can meet its much stronger isolation proof. Otherwise proceed to SQLPERF-T05 classification/focused tiers, then SQLPERF-T06 final verification.
