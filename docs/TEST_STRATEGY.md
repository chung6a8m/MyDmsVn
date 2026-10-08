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

## 6. CI / acceptance gate

Before marking a roadmap task complete:
1. `dotnet restore` (locked when lockfile exists).
2. `dotnet build ... -c Release` across supported TFMs.
3. `dotnet test ... -c Release` for eligible test projects.
4. SQL integration / Windows UI tests in appropriately configured environment.
5. Document tests executed, skipped and blocked with reasons.

Build commands are examples for after P0 creates `MyDmsVn.sln`; until then there is no runnable target.

Do not weaken a test, delete a failing assertion or skip a platform solely to pass CI. Fix cause or document a real environment dependency and preserve coverage.
