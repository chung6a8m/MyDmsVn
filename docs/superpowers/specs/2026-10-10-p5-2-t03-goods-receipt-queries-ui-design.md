# P5.2-T03 Goods Receipt Queries and UI Design

Status: Approved in conversation on 2026-10-10.

## Purpose and scope

Implement the P5.2-T03 vertical slice so an authorized desktop user can list Goods Receipt drafts and posted receipts, open a receipt with all lines, create or edit a Draft through the existing command contracts, and keep catalog lookup choices current while the screen remains open.

The slice includes Dapper read models, Application query handlers, the Local adapter, desktop ViewModels, a dedicated WinForms master-detail control, dependency injection, automated tests, and truthful roadmap evidence. It does not implement Post, stock ledger, stock balance, stock card, cancellation, reversal, pricing, accounting, multi-warehouse receipts, or P5.2 gate concurrency scenarios assigned to later tasks.

## Existing boundaries retained

- Server-side Application queries enforce `Inventory.GoodsReceipts.Read`; UI visibility is not an authorization boundary.
- Desktop ViewModels depend only on `MyDmsVn.Desktop.Application` abstractions and `MyDmsVn.Contracts`.
- Local Mode dispatches the same Application requests that a future HTTP adapter will expose.
- RepoDb remains command-side storage. Dapper is used for List/Get projections.
- `GoodsReceipts.RowVersion` remains an opaque Base64 `version` token. Desktop code stores and returns it as `expectedVersion` without interpreting it.
- Both `net48` and `net8.0`/`net8.0-windows` targets remain supported. No new package is required.
- All asynchronous API calls accept cancellation and never block the WinForms UI thread.

## Contract model

`GoodsReceiptDto` remains the aggregate detail contract used by CreateDraft, UpdateDraft, and Get. It continues to contain the current version token and lines. Additive display fields provide the warehouse and employee labels, and each line receives an additive product label. Read queries populate these labels through Dapper joins; command responses may omit them, and the desktop refreshes Get after a successful save before presenting the persisted editor state.

`GoodsReceiptListRequest` gains an optional free-text search value while retaining warehouse and status filters and paging. Search matches receipt number and catalog code/name labels through parameters. Empty or whitespace search is normalized to no search. Maximum length and unsupported control/NUL characters are rejected by Application validation.

List continues to return `PagedResult<GoodsReceiptDto>` for compatibility with the established `IGoodsReceiptApiClient`. Each item contains its header and complete line collection. This avoids adding a second public summary type during P5 and satisfies the plan requirement that List and Get use header/line Dapper read models.

## Server query flow

Add `IGoodsReceiptQueryService` in Server.Application with `ListAsync` and `GetByIdAsync`. Application handlers remain small: validators validate request shape, the authorization behavior checks Read permission, List returns the query result, and Get maps a missing row to `GoodsReceipt.NotFound`.

`SqlGoodsReceiptQueryService` opens one read connection per request and uses parameterized Dapper commands:

- List executes a count query, a deterministic page query ordered by `ReceiptDate DESC, ReceiptId DESC`, and one set-based line query for the page's receipt IDs. It assembles lines by receipt ID without one query per receipt.
- Get executes header and ordered-line result sets for one receipt.
- Header projections join Warehouses and Employees for display labels. Line projections join Products for display labels. Inactive catalog rows remain joinable so historical selections continue to display.
- The SQL `rowversion` byte array is converted to Base64 only at the mapping boundary.
- CancellationToken is passed into every Dapper `CommandDefinition`.

The Infrastructure DI registration is explicit. No assembly scanning is added.

## Local adapter

Add `LocalGoodsReceiptApiClient` beside the existing feature clients. CreateDraft, UpdateDraft, List, and Get dispatch their corresponding MediatR request and map through `ApiResponseMapper`. `PostAsync` remains an explicit not-yet-supported response until P5.3 implements the Post handler; P5.2-T03 must not imply that posting exists.

Register `IGoodsReceiptApiClient` explicitly in `AddLocalDesktopAdapter`.

## Desktop ViewModels

Use two focused ViewModels with a small shared editor-line model:

- `GoodsReceiptListViewModel` owns paging, free-text search, warehouse/status filters, selected receipt, loading/error/empty state, and opening or refreshing details. Free-text search uses the shared debouncer. Explicit filter selection and Refresh execute immediately. Each load cancels the previous load and uses a monotonically increasing generation so late responses cannot overwrite newer results.
- `GoodsReceiptEditorViewModel` owns receipt header fields, line collection, opaque version, validation errors, save state, and display-only totals. It creates or updates only Draft receipts. Validation runs immediately for explicit selections and edited fields; it is not debounced.
- `GoodsReceiptLineViewModel` owns product ID/label/availability, quantity, unit cost, and the display-only line total. The receipt total is the sum of line totals and is never sent as an authoritative value.

Warehouse and employee use `CatalogLookupViewModel`. Product choices use a receipt-scoped product lookup source shared by line editors. Catalog-change messages are filtered by kind, coalesced, dispatched to the UI owner, and cause the affected choices to reload. Existing selected IDs and display labels remain in the editor even if the related catalog item is inactive; they are marked unavailable for new selection. New selections contain active items only. Activation always performs a fresh API query as the fallback for missed in-process messages.

Both ViewModels dispose cancellation sources, debouncers, lookup subscriptions, and messenger registrations. Late callbacks after disposal are ignored.

## WinForms UI

Create a dedicated `GoodsReceiptControl`; do not overload the P4 `MasterDetailPrototypeControl` abstraction.

The left pane contains a BootstrapSourceGrid receipt list plus search, warehouse/status filters, paging, and Refresh/New actions. The right pane contains receipt date, warehouse, employee, note, status/version display, an editable BootstrapSourceGrid line area, Add/Remove/Save actions, and the display-only total.

The control maps ViewModel field errors to the nearest header or line editor and renders operation errors in its non-modal status area. Busy state disables conflicting actions and shows progress without blocking the UI thread. Grid data errors are captured through the existing headless-safe pattern rather than opening modal dialogs.

The shell adds a single `Goods Receipts` navigation item and reuses an existing tab when it is already open. Opening or activating the tab calls the ViewModel activation refresh. Closing the tab or signing out disposes the control and ViewModels.

## Error behavior

- Validation failures remain inline and preserve user input.
- Unauthorized List/Get surfaces the typed API error and displays no receipt data.
- Get for a deleted/missing receipt returns `GoodsReceipt.NotFound`.
- A stale UpdateDraft returns `GoodsReceipt.ConcurrencyConflict`; the editor preserves input, reports the conflict, and offers Refresh instead of silently overwriting.
- An already-posted receipt is rendered read-only when loaded. P5.2-T03 does not provide a Post action.
- Cancellation and disposal are quiet. Unexpected failures flow through existing notification/error mapping and never open an unhandled modal dialog in tests.

## Test strategy

Development follows RED-GREEN-REFACTOR. Tests exercise real behavior and run target frameworks sequentially.

- Server.Application tests cover List/Get validation, missing Get mapping, and Read permission metadata/pipeline denial.
- SQL integration tests cover deterministic paging/filtering, complete header/line assembly, inactive historical labels, no duplicate aggregates from joins, not-found Get, and exact Base64 version projection. They run only against the isolated disposable test database and report an explicit skip when SQL Server is unavailable.
- Local adapter tests cover dispatch and `ApiResponse` mapping for List/Get/Create/Update while Post remains unsupported.
- Desktop ViewModel tests use controllable delays/fakes to prove debounce, immediate explicit filters, cancellation, stale-response guards, totals, inline errors, catalog-message refresh, inactive-selection preservation, and disposal.
- STA WinForms tests create and dispose controls deterministically, verify master-detail/grid binding and navigation lifecycle, and suppress modal exception/data-error paths.
- Final verification restores, builds, and tests `net48` first and then `net8.0`/`net8.0-windows`, never concurrently.

## Completion evidence

The four P5.2-T03 checkboxes may be checked only when their corresponding automated behavior is implemented and verified. The P5.2 gate remains open because its SQL concurrency matrix, Post authorization, and manual UI smoke belong to the gate/P5.3. The change summary records commands, actual pass/fail/skip counts, the locked-restore baseline issue if still present, remaining risks, and the next roadmap task.
