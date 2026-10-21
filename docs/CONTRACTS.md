# Application / Desktop API contracts

**Status:** Current Local v1 contracts on active net48, refreshed 2026-10-18. RESP changes test lifecycle and L2DB changes persistence adapters; both preserve these observable behaviors. HTTP v2 remains deferred and will need a separately approved implementation/runtime plan.

T04 source checkpoint (2026-10-22, `7eeafa4`): [reviewed evidence](evidence/sal/20261022-t04-atomic-sales-post.md) records atomic Post on one owned UoW, Shared-before-header and all sorted stock locks before writes, exact duplicate/gift allocation, immutable line cost, paired quantity/value movements and authoritative status/version materialized before commit. Receipt Post and Exclusive reconciliation use the same protocol. List/Local/UI, frozen T08, package and Owner acceptance remain open; this checkpoint is not deployable. T01 R3 remains deferred Minor.

Post accepts only SaleId and opaque expectedVersion. It returns authoritative status/time/newVersion before commit; a separately authorized Get loads full detail. AlreadyPosted precedes stale-Draft conflict after request-format validation. No postcommit detail fetch or implicit retry is introduced.

## Purpose

WinForms ViewModels use feature-specific API interfaces in `Desktop.Application/FeatureApiClients.cs`. Current Local adapters invoke Server.Application in-process and return transport-neutral `ApiResponse<T>` values. Future HTTP adapters must keep the same DTO/result semantics. Public DTOs are contracts, not Domain entities or provider objects.

## Sales contracts and reviewed Draft/detail boundary

[Direct Sales](DIRECT_SALES.md) and [query/UI](DIRECT_SALES_QUERY_AND_UI.md) specify T00 `SalesContracts.cs` and the later `ISalesReceiptApiClient`: CreateDraft, UpdateDraft, Post, Get and List through the same immutable ApiResponse/Local boundary. T00 registers the three Active code keys Sales.Receipts.Read/Write/Post without role grants. T03 implements authorized Application/infrastructure Draft Create/Update and GetById ([evidence](evidence/sal/20261022-t03-sales-draft.md)). T04 implements independently authorized Post; List/keyset and the Local adapter remain later tasks.

Save carries ordered independent lines with SaleLineId (0 for new), ProductId, Qty, UnitPrice and IsGift; duplicate Products are valid. The server assigns LineNo, Unit, amounts and identifiers. Update/Post require the opaque expectedVersion. Posted cost snapshots and gross profit are read-only; Draft cost/profit are null. DTO SaleNumber maps to SQL SaleNo. T03 validates active references/line ownership, derives Unit/revenue/LineNo and materializes the complete write response/newVersion before commit. Write uses the existing authorized command-result semantics without requiring Read; Get independently requires Read. No fallible postcommit detail fetch builds a successful write response. The T00 SalesCalendarDateJsonConverter writes SaleDate/FromDate/ToDate as invariant yyyy-MM-dd strings and accepts only valid date-only strings; normalize Local dates without timezone conversion. Scope it to these properties, preserving global ApiJson UTC audit handling and existing module contracts.

`SalesReceiptListRequest`/`SalesReceiptListPage` form a separate cursor contract: optional date/customer/warehouse/employee/status/Product/search, bounded PageSize and validated Cursor; slim Items plus nullable NextCursor/PreviousCursor. Use unique SaleId DESC and Product Any; totals cover all receipt lines. No PageNumber, required TotalCount or reuse of the existing PagedResult offset contract. Cursor encoding is not authorization. Existing catalog/receipt/stock paging remains unchanged unless explicitly extended by SAL.

T02 implements the inventory read extension under existing Inventory read keys ([evidence](evidence/sal/20261022-t02-valued-inventory.md)). `StockBalanceRow` constructors require `InventoryValue` and nullable `AverageUnitCost` (null at Q0, checked round8(V/Q) otherwise); `StockCardRow` requires `ValueChange` and resolves SalesReceipt `SaleNo`. Queries reject invalid Q/V/average or a missing valuation companion. Existing COUNT/OFFSET paging is preserved. Reconciliation retains key-presence fields and adds ledger/balance values and Issues; T04 now implements Sales Post through the same transaction-owned coordinator. [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md) supplies new compatibility obligations.

## Implemented result envelope

The current [ApiResponse](../src/MyDmsVn.Contracts/ApiResponse.cs), [ApiError](../src/MyDmsVn.Contracts/ApiError.cs), [ApiErrorDetail](../src/MyDmsVn.Contracts/ApiErrorDetail.cs) and [ApiJson](../src/MyDmsVn.Contracts/ApiJson.cs) are source-owned contracts. Their properties are immutable; do not replace them with the earlier mutable blueprint.

| Member or operation | Current contract |
|---|---|
| `ApiResponse<T>.Success(data)` | Requires non-null data and constructs a success. Use `UnitResponse.Value` for a no-content command. |
| `ApiResponse<T>.Failure(error)` | Requires a non-null ApiError and constructs a failure. |
| `Data` / `Error` | Read-only properties. JSON emits `data` on success or `error` on failure; ambiguous both-set/none-set input is rejected by the converter. |
| `IsSuccess` | In-process status flag marked `JsonIgnore`. |
| `ApiError.Status` | In-process `ApiStatusCode`, marked `JsonIgnore`; future HTTP deserialization supplies the actual response status. |
| Error JSON | `code`, `message`, `details`, optional `correlationId`; each detail carries `code`, `message`, optional `field`. |

`ApiJson` supplies the shared Newtonsoft.Json camel-case naming, invariant culture, ISO UTC date handling and null omission. `ApiJson.DeserializeResponse<T>(json, responseStatus)` requires a meaningful actual transport status and preserves parity with the Local error status. Keep correlation identifiers bounded and safe, and never serialize ErrorOr, SQL exceptions, stack traces, transactions or MediatR internals.

## Converting Application results

Application handlers and pipeline behaviors return `ErrorOr<T>`. The transport-neutral `ApiResponseMapper` in Server.Application maps this to `ApiResponse<T>` without ASP.NET dependencies. Local adapters return that result directly. A future API host will add HTTP status/headers around the same envelope; handlers must not construct `IResult`, `Results.Ok`, `HttpResponse` or `JsonResult`.

Preserve the existing strongly typed generic mapping and deterministic dominant error/status policy. Do not select error status from arbitrary enumeration order or use dynamic conversion. Unexpected failures are logged with safe diagnostics and mapped without exposing infrastructure details.

## Error taxonomy

| Situation | ApiError.Code | HTTP status (v2) |
|---|---|---|
| Request invalid | `ValidationError` | 400 |
| Unauthenticated | `Auth.Unauthorized` | 401 |
| Not permitted | `Auth.Forbidden` | 403 |
| Entity missing | Feature-qualified code, e.g. `Product.NotFound` | 404 |
| Business conflict / invalid transition | e.g. `GoodsReceipt.AlreadyPosted` | 409 |
| Unique key conflict | e.g. `Product.CodeAlreadyExists` | 409 |
| Unhandled failure | `InternalError` | 500 |

Validation detail example: `{ "code": "Validation.Required", "message": "Quantity is required.", "field": "lines[0].quantity" }`. Field paths **must** match JSON naming and array-index conventions. Do not just lowercase the first character of nested C# paths.

For multiple failures, choose a deterministic aggregate code/status policy (all validation => ValidationError; mixed types => mapped dominant safe status) and retain details. Never select a status only from arbitrary enumeration order.

## Desktop client APIs

Current interfaces include:

- `IProductApiClient`, `IWarehouseApiClient`, `IEmployeeApiClient`, `ICustomerApiClient`.
- `IGoodsReceiptApiClient`: `CreateDraftAsync`, `UpdateDraftAsync`, `PostAsync`, `GetAsync`, `ListAsync`.
- `IInventoryApiClient`: `GetStockBalancesAsync`, `GetStockCardAsync`.
- `IIdentityApiClient`: `LoginAsync` / `GetCurrentUserAsync` for authenticated identity and the existing desktop session boundary.
- `IPermissionApiClient` and `IPermissionCatalogApiClient`: current permission decisions and the code-defined catalog.
- `ISecurityAdministrationApiClient`: user/role lists, authorized mutation and permission-import preview/apply; protected-account maintenance is outside the client API.

Each call is async and accepts `CancellationToken`. Commands have DTO request and typed response; queries return paginated DTOs with total count and deterministic sort where appropriate.

The current catalog clients expose Create, Update, SetActive, List, Get and Lookup operations. List queries accept `CatalogListRequest`, sort by Code then stable ID, and return total-count paging. Lookup queries accept `CatalogLookupRequest`, return the shared `CatalogLookupDto`, and include active rows only; Get/List may still return inactive rows when requested so historical selections can be displayed. List and Lookup search text is optional, limited to 256 characters and must not contain the NUL character; catalog Code and Name values (plus Product Unit) also reject NUL on Create and Update. Invalid input returns a validation response before database access. An Update response is loaded through the command repository inside the same transaction as the write and is materialized before commit, so it preserves the persisted `IsActive` value without a fallible post-commit read. Catalog adapters remove the internal MediatR `Request` wrapper segment when mapping validation details, so fields remain relative to the public DTO (`search`, `code`, and so on). Local adapters dispatch the matching Application request and map its typed result into `ApiResponse<T>` without exposing Domain entities, persistence-provider objects or SQL exceptions.

The current inventory client is query-only. `GetStockBalancesAsync` accepts optional warehouse/product IDs, optional search text, and page number/size; it returns warehouse/product codes and names, active flags, quantity, and total-count paging in deterministic warehouse-code/product-code/ID order. Search is limited to 256 characters and rejects NUL. `GetStockCardAsync` requires positive warehouse/product IDs and accepts optional UTC from/to timestamps with `from <= to`; rows include posting timestamp, document type/ID/number, quantity delta, readable warehouse/product identity and active flags, ordered by `PostedAtUtc, LedgerId`. Application enforces `Inventory.Balances.Read` and `Inventory.StockCard.Read` respectively. No Create/Update/Delete client contract exists for `StockBalances` or `StockLedger`.

Example command-level behavior:

- `UpdateGoodsReceiptDraftRequest` and `PostGoodsReceiptRequest` identify the receipt and carry the required opaque Base64 `expectedVersion` token. `GoodsReceiptDto.version` returns the current token. Clients treat these strings as opaque; no raw UnitOfWork, ADO.NET value or persistence-provider entity crosses the contract boundary.
- `SaveGoodsReceiptRequest` never accepts a receipt number. CreateDraft returns the server-issued number using the current format `GR` plus a zero-padded 10-digit non-cycling sequence value (for example `GR0000000001`). Clients display the returned value and must not generate or predict it; gaps are valid.
- CreateDraft and UpdateDraft accept 1–200 non-null lines. Each line must have a unique positive Product ID, Quantity fitting SQL `decimal(18,4)` and greater than zero, and UnitCost fitting SQL `decimal(19,4)` and greater than or equal to zero. Values with more than four effective fractional digits or precision outside those SQL types return field validation errors before database access.
- `PostGoodsReceiptResponse` returns receipt identifier, status, posting timestamp; retry of an already posted receipt returns a documented conflict (or a stable idempotent success if an idempotency contract is added later). In either case, **no second stock posting** occurs.
- Identifiers and date/time formats are consistent in Local/HTTP tests; transport changes cannot alter business semantics.

## Current Local desktop session

Production WinForms hosts gate shell/business access on successful `IIdentityApiClient.LoginAsync`. The Local adapter owns one host-scoped `IDesktopSession`, but each dispatch creates an immutable request-scoped `ICurrentUserAccessor` snapshot containing the admitted identity and generation; it never reads a later mutable user after an asynchronous await. Login publishes a session only after `IUserAccountValidator` confirms the returned user is currently active in SQL. `GetCurrentUserAsync` and every protected Local dispatch revalidate current account state; disabled/deleted users receive `Auth.Unauthorized` and the desktop session is cleared, while live-user permission revocation remains `Auth.Forbidden` for the action and retains the session. SQL validation failure returns a fail-closed unavailable error rather than treating cached identity as trusted or reporting invalid credentials.

The default Local idle timeout is 30 minutes and is configurable through `DesktopSessionOptions`. It uses `IMonotonicClock`; keyboard/mouse input records user activity, while background API calls and polls do not. Logout/expiry increments the generation, clears current-user UI state and forces reauthentication. Requests captured but not admitted under an old generation are rejected. An already-authorized in-flight use case retains its captured actor/audit identity and cannot be reassigned by logout or a later Login; late UI results remain guarded by the shell session version. No automatic re-execution of mutating commands occurs on expiry or SQL timeout. Bootstrap, recovery and rename of the configured database-bound superuser use separate maintenance authority and are outside client APIs. Permission catalog export is code-backed; upgrades do not synchronize permission records or auto-grant an Administrator role.

## Auth and HTTP-specific behavior (deferred)

The retained HTTP design uses `Authorization: Bearer`; a separately approved HTTP implementation must define and verify its session/token lifecycle in desktop HTTP infrastructure, outside ViewModels. Security rules live on Application use cases and HTTP host authentication. HTTP response codes are meaningful, despite carrying a common JSON envelope.

## Compatibility tests

Preserve or extend source-owned golden JSON and behavior fixtures for:

- success and validation failure with field detail,
- 401/403/404/409,
- pagination,
- decimal quantities and UTC timestamps,
- current catalog, receipt, inventory and security/import DTOs.

The completed RESP/L2DB/GUI-P5/BCI/BCF plans retain candidate-specific Local behavior evidence; the retired REL plan is not active. SAL must add and verify its new Local contracts without treating those historical results as current acceptance. A future approved HTTP adapter must run the same behavior/response assertions against Local and HTTP. A contract breaking change requires versioning or an ADR. [TEST_POLICY](TEST_POLICY.md) selects execution scope; this coverage reference does not require a full suite after every change.
