# Testing strategy and quality gates

## 1. Test layers

| Layer | Coverage | Preferred evidence |
|---|---|---|
| Architecture | Dependency rules, dual-target compatibility, no HTTP/SQL in Domain or UI | architecture/reference tests + build |
| Unit (Application) | Validator codes/field names, handler decisions, permission precedence | xUnit/NUnit tests with fakes |
| Contracts | DTO/JSON golden snapshots, ApiResponse success/failure invariants | shared fixture tests |
| SQL integration | DbUp scripts, RepoDb mapping, Dapper reads, Explicit UoW, concurrency | SQL Server disposable test DB |
| Local adapter | same outputs/errors as Application conversion | adapter contract suite |
| UI | ViewModel commands, binding, error mapping; STA control smoke | Windows STA tests with dialog suppression |
| Remote HTTP (P7) | auth/status codes, JSON, same business behavior as Local | WebApplicationFactory + SQL test DB |

## 2. Cross-target matrix

- `net48` compile/run on Windows with installed Framework 4.8 targeting/runtime.
- `net8.0` Application, Infrastructure and integration tests.
- `net8.0-windows` WinForms and modern host on Windows.
- At least one clean Windows build of each host during P0/P6. Do not claim Linux CI proves WinForms parity.
- Contract DTO tests run for both applicable targets.

## 3. Mandatory SQL scenarios

1. DbUp clean install and re-run unchanged.
2. Commit persists two repository writes in the same UoW.
3. Error on second operation rolls back first.
4. Dispose without commit rolls back and releases connection.
5. Dapper within same UoW reads correct uncommitted changes.
6. Concurrent independent UoWs do not exchange transaction objects/connections.
7. Posting same Draft twice (sequential and concurrent): only one ledger entry per line and one balance increment.
8. Concurrent different receipts for same (WarehouseId,ProductId): final balance equals sum.
9. Transaction failure after ledger insertion leaves both ledger/balance unchanged and receipt Draft.
10. Stock reconciliation query: computed ledger sum equals materialized balance.
11. Unique catalog codes and FKs enforced by SQL.
12. Unauthorized Post leaves all business tables unchanged.
13. Stock Balance filters/page order are deterministic and preserve readable inactive warehouse/product history.
14. Stock Card filters by warehouse/product and UTC range, orders by `PostedAtUtc, LedgerId`, and returns stable document identity.
15. Full P5 slice: create/post a receipt, read the resulting balance/card, reject duplicate Post, and allow a pre-deactivation Draft to post exactly once after all references are reactivated.

**Required:** use isolated test database provisioned by test infrastructure with a test-only connection string, not an arbitrary existing database. Do not silently drop an unknown database. If SQL Server unavailable, report SQL tests as **not run**.

## 4. UI hangs / unattended test policy

- WinForms controls must be created/disposed on an STA thread.
- Register test exception handlers and DataError behavior so failures throw/assert/log instead of opening default modal dialogs.
- All UI interactions and async operations have deterministic timeouts, cancellation and cleanup.
- Headless CI should not open a printer dialog, login prompt, unhandled-exception dialog, SaveFileDialog or MessageBox.
- Record screenshot/UI manual smoke evidence separately; never label it automated.

## 5. Security and contract scenarios

- User direct deny overrides role grant; direct grant overrides lack of role grant.
- Unknown permission and inactive user fail closed.
- Legacy migration verifier tested only with approved real/synthetic algorithm fixtures; no plaintext/hashes in logs.
- ApiResponse JSON casing/nullable behavior, input invalid/mixed errors; for P7 cover HTTP 400/401/403/404/409/500.
- Local/HTTP parity tests reuse same command/read data and semantic assertions.
- Inventory Local adapter tests cover both read permissions, validation mapping and the absence of balance/ledger mutation APIs.

## 6. CI / acceptance gate

Before marking a roadmap task complete:
1. `dotnet restore` (locked when lockfile exists).
2. `dotnet build ... -c Release` across supported TFMs.
3. `dotnet test ... -c Release` for eligible test projects.
4. SQL integration / Windows UI tests in appropriately configured environment.
5. Document tests executed, skipped and blocked with reasons.

## SQL integration measurement safety

SQL integration mutation requires both `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeting `master` and an operator-maintained `MYDMSVN_SQL_TEST_APPROVAL_FILE`. The harness verifies the live effective database, exact instance identity/fingerprint, approval lifetime and version-specific DMV permission before DDL. It repeats the live check before teardown. Database ownership is journaled and flushed before creation, corroborated with a database marker and live metadata, and quarantined rather than dropped when ownership is ambiguous.

Use `tests/tools/Invoke-SqlIntegrationBenchmark.ps1` for accepted SQLPERF measurements. It runs the two TFMs sequentially beneath a Windows Job Object, uses unique TRX/timing artifacts and treats timeout, partial/missing TRX, nonzero exit, mandatory skips or reconciliation failure as incomplete. Build/restore is outside measured time. Run one labeled warm-up and at least three valid warm passes per TFM; invalid passes are replaced, never included in medians.

TRX duration is authoritative inclusive test time. Timing JSONL is opt-in and records explicit create/migration/drop scopes. Reports use interval unions, exclusive attribution, overlap and residual; nested stage durations are never added to TRX or wall-clock. Raw connection strings, hosts, approval data, theory values, journals and local paths must not enter shareable reports. Missing DMV permission/data is reported as unavailable, not zero.

SQL concurrency probes that must pause inside a business transaction use a unique test-only application-lock gate. Acceptance requires independent DMV evidence that the holder waits on the controller's APP lock while retaining the expected business lock, and that the contender waits on the expected non-APP business resource before release. All started SQL operations have bounded cancellation and are drained before the disposable database is torn down; an uncommitted signal row is never the progress condition.

Disposable database connections and production migration sessions are non-pooled. Teardown must not globally clear unrelated client pools; any test that deliberately opts into pooling owns and clears that exact pool. Guarded `SINGLE_USER WITH ROLLBACK IMMEDIATE` and positive ownership reconciliation remain mandatory, including failure/retry and interrupted-migration cases.

Gate SQLPERF-T01 passed on 2026-10-15 at commit `cd3374ff295b86c2e38ea665c16cbbed5b356621`: one warm-up plus three accepted measured runs per TFM executed 213 / 213 cases with no mandatory skips, timing overhead stayed below 5%, and the post-baseline Release regression matrix passed. The sanitized baseline, invalid-run handling, cleanup evidence and limitations are recorded in `docs/SQLPERF_T01_VERIFICATION.md`. Re-establish a matched baseline if the inventory or environment changes materially before evaluating an optimization.

For development iteration, use `tests/tools/Invoke-FocusedSqlIntegrationTests.ps1` with an explicit category and TFM list. The helper performs approval preflight, runs discovery/execution under the owned-process-tree watchdog, writes unique TRX/timing/journal artifacts, reports selected/discovered/executed/skipped counts and performs bounded ownership reconciliation after every launched execution, including timeout or result-gate failure. Valid categories are `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. Non-SQL exemption fingerprints cover reviewed integration-test, harness and application source while excluding generated build output, so changes to async/generated bodies or helper dependencies require review without becoming stale from commit metadata alone. Focused success is never a merge/release substitute: final acceptance still requires unfiltered, sequential `net48` and `net8.0` runs with the independent inventory and zero mandatory skips.

SQLPERF-T06 closed on 2026-10-15 at candidate `972674f` against unchanged baseline `2a139c258027f336cb1ded66a071a68c491ce08b`. One warm-up plus three accepted measured runs per TFM improved full-suite median wall-clock by 11.23% on `net48` and 8.56% on `net8.0`, while the candidate inventory increased from 213 to 222 executed cases. The 20–30% engineering target was not achieved; per-test database isolation and serial execution remain the reviewed policy because no safe fixture reset boundary or parallel-capacity evidence exists. Exact commands, matched-inventory analysis, migration-event and ownership reconciliation, rejected-run diagnosis and full Release results are in `docs/SQLPERF_T06_VERIFICATION.md`.

P0 created `MyDmsVn.sln`; these commands are runnable on Windows with the .NET 8 SDK and .NET Framework 4.8 targeting pack. P1 provides guarded isolated test-database infrastructure; SQL scenarios still require the explicit approved non-production configuration above.

Do not weaken a test, delete a failing assertion or skip a platform solely to pass CI. Fix cause or document a real environment dependency and preserve coverage.
