# P5.2-T03 Goods Receipt Queries and UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver authorized Dapper List/Get queries and a responsive WinForms master-detail workflow for creating, viewing, and editing Goods Receipt drafts.

**Architecture:** Keep the existing Contracts → Desktop.Application → Local adapter → Server.Application → Server.Infrastructure dependency flow. Dapper assembles paged receipt aggregates and opaque version tokens; focused desktop ViewModels own cancellation, debounce, lookup refresh, validation, and display-only totals; a dedicated control binds those ViewModels without accessing SQL or Infrastructure.

**Tech Stack:** C# targeting `net48`, `net8.0`, and `net8.0-windows`; MediatR; FluentValidation; Dapper; CommunityToolkit.Mvvm; Bootstrap5WinFormUI/BootstrapSourceGrid; xUnit; SQL Server disposable integration database.

**Spec:** `docs/superpowers/specs/2026-10-10-p5-2-t03-goods-receipt-queries-ui-design.md`

## Global Constraints

- Do not use subagents. Execute this plan natively in the current session.
- Run target frameworks sequentially; never run tests for two TFMs concurrently.
- Keep `net48`, `net8.0`, and `net8.0-windows`; do not move to .NET 10.
- Use RepoDb only for the existing command repositories and Dapper for new List/Get projections.
- Preserve server-side authorization, explicit UnitOfWork ownership for commands, opaque Base64 rowversion semantics, and Local/HTTP-compatible `ApiResponse<T>` contracts.
- Do not implement Post, stock ledger, balances, stock card, returns, pricing, accounting, or unrelated P5.2 gate work.
- SQL integration tests may use only the isolated disposable test database from `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`; otherwise they must report an explicit skip.
- Follow RED-GREEN-REFACTOR. Name the concrete production break each test catches before writing its body.
- The current `main` baseline reports `NU1004` for stale desktop project-reference lock entries under `--locked-mode`; investigate it without weakening locked restore or hiding the result.

## Review Focus

- An empty List page must return an empty aggregate page without emitting an invalid or unbounded line query.
- Joining receipt lines/products must not inflate `TotalCount` or duplicate receipt aggregates.
- Deactivated warehouse, employee, or product rows must retain their selected ID and readable label while being unavailable for new selection.
- A canceled request that completes late must not overwrite a newer List/Get/lookup result or update a disposed ViewModel.
- A Posted receipt must be read-only in the editor even when the caller also has Write permission.

---

### Task 1: Contract and Application query boundary

**Files:**
- Modify: `src/MyDmsVn.Contracts/InventoryContracts.cs`
- Create: `src/MyDmsVn.Server.Application/Inventory/IGoodsReceiptQueryService.cs`
- Create: `src/MyDmsVn.Server.Application/Inventory/GoodsReceiptQueries.cs`
- Modify: `src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs`
- Modify: `tests/MyDmsVn.Server.Application.Tests/ContractEnvelopeTests.cs`
- Modify: `tests/MyDmsVn.Server.Application.Tests/GoodsReceiptApplicationTests.cs`

**Interfaces:**
- Consume: existing `ApplicationRequest<T>`, `IAuthorizedRequest`, `PermissionKeys.InventoryGoodsReceiptsRead`, `PagedResult<T>`, and `ApiResponseMapper` conventions.
- Produce: `IGoodsReceiptQueryService.ListAsync(GoodsReceiptListRequest, CancellationToken) -> Task<PagedResult<GoodsReceiptDto>>` and `GetByIdAsync(long, CancellationToken) -> Task<GoodsReceiptDto?>`.
- Produce: `GoodsReceiptListRequest.Search`; additive `GoodsReceiptDto.WarehouseDisplayName`, `WarehouseIsActive`, `EmployeeDisplayName`, `EmployeeIsActive`; additive `GoodsReceiptLineDto.ProductDisplayName`, `ProductIsActive`. Existing constructor calls remain source-compatible through optional trailing parameters.
- Produce: `ListGoodsReceiptsQueryValidator`, `GetGoodsReceiptByIdQueryValidator`, `ListGoodsReceiptsQueryHandler`, and `GetGoodsReceiptByIdQueryHandler`.

- [ ] **Step 1: Write failing contract and query tests**

Add tests proving JSON round-trip preserves the new search/display/active fields, the existing constructor shapes still compile, List rejects page 0, page sizes outside 1–200, search over 128 characters, NUL/control search, and invalid status, Get rejects non-positive IDs, both requests declare Read permission, List delegates once to the query service, and missing Get returns `GoodsReceipt.NotFound`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run sequentially:

```powershell
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Debug -f net8.0 --filter "FullyQualifiedName~ContractEnvelopeTests|FullyQualifiedName~GoodsReceiptApplicationTests"
```

Expected: FAIL because the display fields, `Search`, query service, validators, and handlers do not exist.

- [ ] **Step 3: Implement the minimal contract and Application query code**

Keep commands in `GoodsReceiptFeatures.cs`; move the already-declared List/Get request types into `GoodsReceiptQueries.cs` with their validators/handlers to avoid duplicate types. Status accepts null/blank, `Draft`, or `Posted` using ordinal comparison. Search validation uses the catalog-safe character rules without adding a new dependency from Contracts.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the same `dotnet test ... -f net8.0` command. Expected: all selected tests pass with no warnings introduced by this task.

- [ ] **Step 5: Commit Task 1**

```powershell
git add src/MyDmsVn.Contracts/InventoryContracts.cs src/MyDmsVn.Server.Application/Inventory tests/MyDmsVn.Server.Application.Tests
git commit -m "feat(inventory): define goods receipt read queries"
```

### Task 2: Dapper receipt aggregate query service

**Files:**
- Create: `src/MyDmsVn.Server.Infrastructure/Inventory/SqlGoodsReceiptQueryService.cs`
- Modify: `src/MyDmsVn.Server.Infrastructure/DependencyInjection.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/GoodsReceiptQueryIntegrationTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.IntegrationTests/GoodsReceiptModelRegistrationTests.cs`

**Interfaces:**
- Consume: `IDbConnectionFactory.OpenConnectionAsync(CancellationToken)` and Task 1's `IGoodsReceiptQueryService`.
- Produce: `internal sealed SqlGoodsReceiptQueryService : IGoodsReceiptQueryService` explicitly registered as scoped.
- List ordering: `ReceiptDate DESC, ReceiptId DESC`; line ordering: `LineNo, ReceiptLineId`.
- Search: parameterized match on ReceiptNo, Warehouse Code/Name, Employee Code/Name, or an `EXISTS` Product Code/Name match.

- [ ] **Step 1: Write failing SQL integration and registration tests**

Using migrated disposable databases and real Dapper, add tests for: complete Get header/lines and exact Base64 rowversion; missing Get; deterministic paging; warehouse/status/search filters; total count unaffected by multiple lines; inactive catalog labels retained; and an empty page with no line-query failure. Add a DI test resolving `IGoodsReceiptQueryService` only after SQL persistence registration.

- [ ] **Step 2: Run the focused integration tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Debug -f net8.0 --filter "FullyQualifiedName~GoodsReceiptQueryIntegrationTests|FullyQualifiedName~GoodsReceiptModelRegistrationTests"
```

Expected: FAIL for missing service/registration when SQL Server is configured; SQL-dependent cases may SKIP only through the existing explicit SQL-server fact mechanism.

- [ ] **Step 3: Implement parameterized set-based Dapper queries**

Use one open read connection per service call. List reads count and page headers, returns immediately for an empty header page, then reads all page lines using a parameterized ID set and groups them by receipt ID. Get reads one header and its lines without N+1 calls. Map `byte[] RowVersion` with `Convert.ToBase64String` and populate every display/active field.

- [ ] **Step 4: Run the focused integration tests and verify GREEN/SKIP honestly**

Run the same integration-test command. Expected: registration tests pass; SQL tests pass when configured or report the existing explicit skip reason.

- [ ] **Step 5: Commit Task 2**

```powershell
git add src/MyDmsVn.Server.Infrastructure tests/MyDmsVn.Server.Infrastructure.IntegrationTests
git commit -m "feat(inventory): query goods receipt aggregates with Dapper"
```

### Task 3: Local Goods Receipt adapter

**Files:**
- Create: `src/MyDmsVn.Desktop.Infrastructure.Local/LocalGoodsReceiptApiClient.cs`
- Modify: `src/MyDmsVn.Desktop.Infrastructure.Local/DependencyInjection.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/LocalGoodsReceiptApiClientTests.cs`

**Interfaces:**
- Consume: existing `IGoodsReceiptApiClient`, `ISender`, Create/Update/List/Get request types, and `ApiResponseMapper.Map(result, "Request")`.
- Produce: explicit transient `IGoodsReceiptApiClient` registration.
- `PostAsync` returns a deterministic failed `ApiResponse<PostGoodsReceiptResponse>` with status `InternalServerError` and code `GoodsReceipt.PostNotAvailable`; it does not dispatch or imply a P5.3 handler.

- [ ] **Step 1: Write failing Local adapter tests**

Test that DI resolves the concrete adapter, Create/Update/List/Get dispatch the correct MediatR request with cancellation and preserve success/not-found/forbidden mappings, and Post returns `GoodsReceipt.PostNotAvailable` without calling the sender.

- [ ] **Step 2: Run the focused desktop tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Debug -f net8.0-windows --filter "FullyQualifiedName~LocalGoodsReceiptApiClientTests|FullyQualifiedName~DesktopCompositionTests"
```

Expected: FAIL because the adapter and registration do not exist.

- [ ] **Step 3: Implement and register the minimal adapter**

Follow `LocalCatalogApiClientBase` request/response mapping style, without adding Server.Infrastructure references to Desktop.Application or Desktop.WinForms.

- [ ] **Step 4: Run the focused desktop tests and verify GREEN**

Run the same `dotnet test ... -f net8.0-windows` command. Expected: selected tests pass.

- [ ] **Step 5: Commit Task 3**

```powershell
git add src/MyDmsVn.Desktop.Infrastructure.Local tests/MyDmsVn.Desktop.Tests
git commit -m "feat(desktop): add local goods receipt adapter"
```

### Task 4: Receipt list and editor ViewModels

**Files:**
- Create: `src/MyDmsVn.Desktop.Application/GoodsReceiptListViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/GoodsReceiptEditorViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/GoodsReceiptLineViewModel.cs`
- Create: `src/MyDmsVn.Desktop.Application/GoodsReceiptProductOptionsViewModel.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/GoodsReceiptListViewModelTests.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/GoodsReceiptEditorViewModelTests.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/GoodsReceiptProductOptionsViewModelTests.cs`

**Interfaces:**
- Consume: `IGoodsReceiptApiClient`, `WarehouseCatalogLookupSource`, `EmployeeCatalogLookupSource`, `ProductCatalogLookupSource`, `CatalogLookupViewModel`, `CatalogChangedMessage`, `DebouncedAsyncAction`, `IUiDispatcher`, and `DesktopViewModelBase`.
- Produce: `GoodsReceiptListViewModel.ActivateAsync`, `RefreshAsync`, `SetSearch`, `SetWarehouseFilterAsync`, `SetStatusFilterAsync`, `SelectAsync`, `NewDraft`, and `Dispose`.
- Produce: `GoodsReceiptEditorViewModel.Load`, `SaveAsync`, `RefreshAsync`, `AddLine`, `RemoveLine`, `IsReadOnly`, `Total`, and `Dispose`.
- Produce: `GoodsReceiptLineViewModel.ProductId`, `ProductDisplayName`, `ProductIsAvailable`, `Quantity`, `UnitCost`, and computed `LineTotal`.
- Produce: receipt-scoped `GoodsReceiptProductOptionsViewModel.Items`, `ActivateAsync`, `SetSearch`, `RefreshAsync`, and `Dispose`; it supports many line selections without owning a single selected ID.

- [ ] **Step 1: Write failing List ViewModel tests**

Prove rapid typing issues one final debounced List request, explicit warehouse/status filters and Refresh execute immediately, filters reset page one, canceled late results cannot overwrite current items, selecting loads Get, activation fetches fresh data, errors are surfaced, and dispose cancels/ignores delayed work.

- [ ] **Step 2: Run List ViewModel tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Debug -f net8.0-windows --filter "FullyQualifiedName~GoodsReceiptListViewModelTests"
```

Expected: FAIL because the ViewModel does not exist.

- [ ] **Step 3: Implement the minimal List ViewModel and verify GREEN**

Use the shared debouncer only for free text. Maintain a generation counter plus linked cancellation source for every list/detail load and dispatch observable changes through `IUiDispatcher`.

- [ ] **Step 4: Write failing editor and product-options tests**

Prove create/update request shape, opaque version reuse, successful save followed by Get refresh, immediate field errors, line/receipt totals, stale conflict preserving input, Posted read-only behavior, product/warehouse/employee catalog messages refreshing only the matching options, burst coalescing, inactive selections retaining ID/label, active-only new choices, and disposal unregistering/canceling all work.

- [ ] **Step 5: Run editor/options tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Debug -f net8.0-windows --filter "FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~GoodsReceiptProductOptionsViewModelTests"
```

Expected: FAIL because the editor, line, and product-options ViewModels do not exist.

- [ ] **Step 6: Implement minimal editor/line/product-options behavior**

Reuse header `CatalogLookupViewModel` instances. Preserve server field paths such as `warehouseId`, `employeeId`, and `lines[n].productId`; expose UI-friendly error access without duplicating server business validation. Calculate totals locally only for display and never add totals to save contracts.

- [ ] **Step 7: Run all Task 4 tests and verify GREEN**

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Debug -f net8.0-windows --filter "FullyQualifiedName~GoodsReceiptListViewModelTests|FullyQualifiedName~GoodsReceiptEditorViewModelTests|FullyQualifiedName~GoodsReceiptProductOptionsViewModelTests"
```

Expected: all Task 4 tests pass.

- [ ] **Step 8: Commit Task 4**

```powershell
git add src/MyDmsVn.Desktop.Application tests/MyDmsVn.Desktop.Tests
git commit -m "feat(desktop): add goods receipt view models"
```

### Task 5: Dedicated WinForms master-detail control and shell integration

**Files:**
- Create: `src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs`
- Create: `src/MyDmsVn.Desktop.WinForms/GoodsReceiptControlFactory.cs`
- Modify: `src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs`
- Modify: `src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs`
- Create: `tests/MyDmsVn.Desktop.Tests/GoodsReceiptControlTests.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/FoundationShellFormTests.cs`
- Modify: `tests/MyDmsVn.Desktop.Tests/DesktopCompositionTests.cs`

**Interfaces:**
- Produce: `IGoodsReceiptControlFactory.Create() -> Control` and explicit transient registration.
- Produce: `FoundationShellForm.OpenGoodsReceipts()` using workspace key `goods-receipts` and title `Goods Receipts`.
- Consume: Task 4 ViewModels only; no Server or SQL types enter `GoodsReceiptControl`.

- [ ] **Step 1: Write failing STA control and shell tests**

Using `StaTest` and `WinFormsTestGuard`, verify receipt rows and selected details bind into two BootstrapSourceGrid instances, lookup choices bind, totals/status/busy/read-only states render, Add/Remove/Save events call ViewModel behavior, field errors appear beside inputs without modal dialogs, DataError is captured, navigation opens one reusable tab, activation refreshes, sign-out/close disposes the control, and DI resolves the factory/shell.

- [ ] **Step 2: Run the focused WinForms tests and verify RED**

```powershell
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Debug -f net8.0-windows --filter "FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~DesktopCompositionTests"
```

Expected: FAIL because the control, factory, navigation item, and registrations do not exist.

- [ ] **Step 3: Implement the minimal dedicated control and lifecycle integration**

Use a split master/detail layout. The line grid may use a selected-line editor panel for product, quantity, and unit cost rather than inventing unverified SourceGrid cell editors. All async event handlers call ViewModel methods, surface failures through existing notification state, and return control to the UI message loop.

- [ ] **Step 4: Run the focused WinForms tests and verify GREEN**

Run the same focused `net8.0-windows` command. Expected: all selected tests pass without modal hangs.

- [ ] **Step 5: Commit Task 5**

```powershell
git add src/MyDmsVn.Desktop.WinForms tests/MyDmsVn.Desktop.Tests
git commit -m "feat(desktop): add goods receipt master-detail UI"
```

### Task 6: Documentation, lockfile diagnosis, and sequential verification

**Files:**
- Modify: `docs/plans/20261008-001-sales-inventory-foundation.md`
- Modify only if evidence requires it: affected `packages.lock.json` files

**Interfaces:**
- Consume: completed Tasks 1–5 and the P5.2-T03 acceptance bullets.
- Produce: truthful plan status/checkmarks supported by actual verification output.

- [ ] **Step 1: Diagnose locked restore without hiding dependency drift**

Run `dotnet restore MyDmsVn.sln --locked-mode`. If it still reports `NU1004`, compare the affected project references with lock files. Regenerate only genuinely stale lock files using an unlocked restore, inspect the diff for package/version drift, and rerun locked restore. Do not add broad suppressions or delete lock files.

- [ ] **Step 2: Run Release builds sequentially**

Run each command only after the previous command exits:

```powershell
dotnet build src/MyDmsVn.Desktop.App/MyDmsVn.Desktop.App.csproj -c Release -f net48 --no-restore -m:1
dotnet build src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj -c Release -f net48 --no-restore -m:1
dotnet build src/MyDmsVn.Desktop.AppCore/MyDmsVn.Desktop.AppCore.csproj -c Release -f net8.0-windows --no-restore -m:1
dotnet build src/MyDmsVn.Server.DbMigrator/MyDmsVn.Server.DbMigrator.csproj -c Release -f net8.0 --no-restore -m:1
```

Expected: each exits 0. The test commands below build the remaining referenced libraries for their selected TFM.

- [ ] **Step 3: Run all test projects for `net48` sequentially**

```powershell
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net48 --no-restore
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore
```

Expected: non-SQL tests pass; SQL tests pass or explicitly skip because the isolated SQL Server variable is absent.

- [ ] **Step 4: Run all modern-TFM test projects sequentially**

```powershell
dotnet test tests/MyDmsVn.Architecture.Tests/MyDmsVn.Architecture.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/MyDmsVn.Server.Infrastructure.IntegrationTests/MyDmsVn.Server.Infrastructure.IntegrationTests.csproj -c Release -f net8.0 --no-restore
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net8.0-windows --no-restore
```

Expected: non-SQL tests pass; SQL tests pass or explicitly skip. No command overlaps another TFM.

- [ ] **Step 5: Review requirements and update phase status**

Re-read all four P5.2-T03 bullets. Check only bullets backed by the test/build evidence. Keep P5.2 gate items open, record any unavailable manual/SQL evidence, and identify P5.2 gate/P5.3-T01 as the next roadmap work.

- [ ] **Step 6: Self-review the complete diff**

Run `git diff --check HEAD~5..HEAD` (adjust the base to the design commit if commit count differs), inspect `git status --short`, review every changed file for architecture violations, accidental package changes, secrets, and out-of-scope Post/stock behavior. This replaces subagent review per the user's explicit instruction.

- [ ] **Step 7: Commit documentation and verified lockfile updates**

```powershell
git add docs/plans/20261008-001-sales-inventory-foundation.md
git add --update -- '**/packages.lock.json'
git commit -m "docs: record P5.2-T03 verification"
```
