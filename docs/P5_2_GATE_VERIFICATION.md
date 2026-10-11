# P5.2 gate verification evidence

Date: 2026-10-11

## Scope

This evidence covers the P5.2 Goods Receipt Draft gate without implementing P5.3 posting. It closes the SQL constraint/rollback/number-allocation item and the automated safe-STA UI smoke item. It also completes the non-Post portions of the concurrency and authorization items.

The gate remains open for `UpdateDraft` versus `Post` concurrency and executable Post authorization because `PostGoodsReceiptCommandHandler` belongs to P5.3 and does not exist yet. The placeholder desktop `PostAsync` contract is not treated as posting capability.

## Automated evidence

- Existing migration tests execute direct invalid SQL against the disposable database and verify all Goods Receipt FK metadata, required columns, unique receipt number, unique line number/product, canonical Draft/Posted state, positive line number/quantity, nonnegative unit cost and rowversion advancement.
- Existing command integration tests prove header-plus-lines atomic create/update, a lines-only edit advancing the aggregate version, full rollback after an injected second-line failure and concurrent UpdateDraft calls through separate scopes/UoWs yielding one commit plus one stale-token conflict.
- `Concurrent_creates_in_separate_uows_issue_unique_server_numbers` creates eight Drafts concurrently through independent scopes/UoWs, verifies the exact `GR` plus ten digits format and verifies eight distinct persisted receipt numbers.
- Application authorization tests cover explicit deny for CreateDraft, UpdateDraft, List and Get before any UoW/query access. A Post-only permission store is denied for Write and Read operations.
- SQL integration verifies a Post-only user cannot CreateDraft, UpdateDraft, List or Get. It snapshots every persisted header field (including status, audit values and rowversion) plus every ordered line field before the denied requests, compares the full aggregate afterward and verifies the business-table row counts remain unchanged.
- Safe STA UI coverage exercises new Draft, save/edit/reload/detail display and three real Warehouse, Employee and Product `CatalogControl` instances alongside an open Goods Receipt control. The catalog controls invoke deactivate/reactivate through their production ViewModels and a shared messenger; each relevant receipt lookup reloads without changing the selected IDs.

## Verification matrix

The final Release verification ran each target framework in a separate command. No two TFMs were tested concurrently.

- `dotnet restore MyDmsVn.sln --disable-parallel` — succeeded.
- `dotnet build MyDmsVn.sln -m:1 -c Release --no-restore` — succeeded with 0 warnings and 0 errors.
- Server Application: 107/107 on `net48`, then 107/107 on `net8.0`.
- Desktop: 165/165 on `net48`, then 165/165 on `net8.0-windows`.
- Architecture: 5/5 on `net8.0`.
- SQL Server integration: 75/75 on `net48`, then 75/75 on `net8.0`, each using an isolated disposable database.

Total: 699/699 target executions passed, 0 failed and 0 skipped.

## Remaining gate work

- P5.3 must implement Post with the shared held-header lock before the `UpdateDraft` versus `Post` race can be exercised.
- P5.3 must add an authorized Post Application request/handler before Post grant/deny and no-mutation behavior can be tested.
- P5.2 remains open in `docs/DEVELOPMENT_PLAN.md`; no posting, ledger, balance or later-phase capability is claimed.
