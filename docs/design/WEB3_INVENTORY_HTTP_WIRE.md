# WEB3 — Inventory / Goods Receipt HTTP wire contract v1 (PROPOSED)

**Decision scope:** Owner chose WEB3 on 2026-11-01. This file is the **normative design to implement**, not a claim that endpoints exist. **Canonical executable fixtures must be created/frozen by WEB3-B01** under `docs/contracts/web3-v1/` after source reconciliation. Existing [WEB2 host/auth contract](WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md), [Contracts](../CONTRACTS.md), [Data model](../DATA_MODEL.md) and exact `main` source remain authorities for existing runtime behavior. This Web-specific wire must **not** change the Local/Desktop JSON DTO shape.

## 1. Transport, security and authority

- Extend the existing `net48` System.Web WebAPI OWIN host at `/api/v1`, through `Web2Pipeline`, not a new service. Reuse its HTTP envelope `{data:...}` / `{error:{code,message,details,correlationId}}`, guarded Bearer, current SQL-backed active account/session and per-request actor. `GET health` and Sales/Catalog remain unchanged.
- Existing access JWT: short-lived Bearer in browser localStorage. Refresh GUID only in `__Host-mydmsvn-rt` HttpOnly Secure SameSite=Strict Path=/ cookie, SQL rotation/replay. Business API uses `Authorization: Bearer` and excludes refresh cookie; cookie-changing auth endpoints keep exact Origin/CSRF. Owner accepted storage residual XSS risk, not a change to cookie auth/BFF.
- Existing host HTTPS/same-origin, trusted Host/Origin, per-request body limits, security headers/CSP nonce, reject Forwarded input and no wildcard CORS still apply. Client-supplied actor, permission key, cost/stock totals, posted status, receipt number or SQL IDs do not convey authority.
- Server `Application` handlers enforce code-defined **active** permissions; UI capabilities are display hints only. Denials must not mutate business data; 401 for absent/expired/revoked actor, 403 for lacking specific operation, 409 conflict for stale/already-posted.
- **No automatic retry** for any dispatched Create/Update/Post. Failed/timeout response after dispatch may have committed; client freezes unknown until authoritative reconciliation. Never infer transaction rollback from client AbortSignal. No operation journal/idempotency keys in WEB3.

## 2. Proposed routes and exact ownership

| Verb and route | Required Application query/command | Permission | Wire response |
|---|---|---|---|
| `GET /api/v1/goods-receipts` | `ListGoodsReceiptsQuery` | `Inventory.GoodsReceipts.Read` | page |
| `GET /api/v1/goods-receipts/{receiptId}` | `GetGoodsReceiptByIdQuery` | `Inventory.GoodsReceipts.Read` | receipt detail |
| `POST /api/v1/goods-receipts` | `CreateGoodsReceiptDraftCommand` | `Inventory.GoodsReceipts.Write` | authoritative Draft DTO |
| `PUT /api/v1/goods-receipts/{receiptId}/draft` | `UpdateGoodsReceiptDraftCommand` | `Inventory.GoodsReceipts.Write` | authoritative Draft DTO |
| `POST /api/v1/goods-receipts/{receiptId}/post` | `PostGoodsReceiptCommand` | `Inventory.GoodsReceipts.Post` | **receipt Post ack without version** |
| `GET /api/v1/inventory/balances` | `GetStockBalancesQuery` | `Inventory.Balances.Read` | page |
| `GET /api/v1/inventory/stock-card` | `GetStockCardQuery` | `Inventory.StockCard.Read` | page |
| `GET /api/v1/auth/capabilities?feature=goods-receipts` | `IPermissionAuthorizationService` | valid authenticated active actor | {read,write,post} hints |
| `GET /api/v1/auth/capabilities?feature=inventory-balances` | same | same | {read} hint |
| `GET /api/v1/auth/capabilities?feature=inventory-stock-card` | same | same | {read} hint |

The existing `?feature=sales` continues to return exactly its current `{read,write,post}` shape/semantics. Unknown/multiple feature names or unknown query keys fail 400; no broad "all permissions" response or frontend-selected permission names. Receipt Write/Post grants do not imply Read. StockCard.Read does not imply Receipt.Read, Sales.Read or Balances.Read. Catalog lookup permissions stay independent.

## 3. Numerical/time/identifier wire rules

| Kind | WEB3 wire | Rule/test |
|---|---|---|
| `receiptId`, `receiptLineId`, `ledgerId`, `documentId` (C# long) | JSON **string decimal integer** | Canonical 1..Int64.MaxValue (new receipt doesn't send an ID); reject JS JSON numeric token, exponent, leading zero, overflow |
| `warehouseId`, `employeeId`, `productId` | JSON int32 number | Positive <= 2147483647; never trust display labels as IDs |
| `lineNumber`, `pageNumber`, `pageSize`, `totalCount` | checked integer | 1-based pages; limits from validators, 1..200 pageSize; total count type/range verified at B01 |
| All C# decimal including `quantity`, `unitCost`, `inventoryValue`, `averageUnitCost`, `quantityChange`, `valueChange` | **string** invariant decimal | No JSON number/float; no exponent; exact precision/scale, reject NaN or silent rounding |
| Nullable `averageUnitCost` | string or explicit null | Null means undefined, not zero. Omitted/null behavior frozen by B01 fixtures |
| `receiptDate` | `yyyy-MM-dd` string | Calendar date, no timezone conversion; leap day valid |
| `postedAtUtc`, `fromUtc`, `toUtc` | ISO 8601 UTC with `Z` | Preserve precision; require UTC, no browser-local drift or unspecified kinds |
| `expectedVersion` / detail `version` | opaque Base64 string | Source rowversion is 8 bytes; no client comparison arithmetic |
| `receiptNumber` | server-generated display string | Format `GR0000000001`... as currently implemented; gaps are valid; never client-supplied |

**Source bounds:** Quantity SQL `decimal(18,4)` >0, <= `99999999999999.9999`; UnitCost SQL `decimal(19,4)` >=0, <= `999999999999999.9999`. Receipt 1..200 lines; Product IDs unique **within** one receipt, unlike Sales where repeated products are allowed. Costs/value Q/V are **server authoritative**. A UI line amount preview may be derived from exact decimal arithmetic but cannot post a precomputed valuation.

## 4. Proposed payload field tables

### 4.1 Create and Update

`POST /goods-receipts` body (create; **not** nullable receiptId, no actor):
```json
{
  "receiptDate": "2026-11-01",
  "warehouseId": 1,
  "employeeId": 2,
  "note": "Nhập hàng",
  "lines": [
    {"productId": 3, "quantity": "1.2500", "unitCost": "12.3456"}
  ]
}
```
`PUT /goods-receipts/{receiptId}/draft` body: **exact fields above +** `expectedVersion` opaque string. `receiptId` exists **only in route**. Accept nullable `note` as source permits; server decides null/empty distinction. Both reject client fields `receiptNumber`, `status`, `postedAtUtc`, `createdBy`, `receiptLineId`, `lineNumber`, `clientLineId`, `lineAmount` and any stock/valuation field.

**Identity consequence:** `SaveGoodsReceiptLineRequest` carries no line ID. `UpdateGoodsReceiptDraftCommandHandler` uses `ReplaceDraftAsync` (full replacement) and builds new ordered lines. Client `clientLineId` is private UI identity; host maps request index to server `lineNumber`, verifies result order/cardinality, and must **not** claim server line IDs survive update. Server response IDs are fresh authoritative IDs where replacement occurs. No Product-ID-based reconcile/reorder; duplicate Products are validation errors.

The existing Create/Update handlers compute `GoodsReceiptDto` **before COMMIT**, so a successful response is authoritative for Draft status, ID, number, version and lines without a later fallible Get. **Caveat:** source `ToDto` may return null header/line display names and default active flags, whereas an authorized query Get supplies historical reference labels. B01 must freeze the wire projection. Frontend must retain selected labels or fetch authorized detail separately, not fabricate historical names or treat a failed Read as a failed Save.

### 4.2 Detail and list pages

Receipt detail **proposed** names: `receiptId`, `receiptNumber`, `receiptDate`, `warehouseId`, `warehouseDisplayName`, `warehouseIsActive`, `employeeId`, `employeeDisplayName`, `employeeIsActive`, `status`, `note`, `postedAtUtc` (nullable), `version`, ordered `lines[{receiptLineId,lineNumber,productId,productDisplayName,productIsActive,quantity,unitCost}]`. Enforce string int64/decimal as in `3`, no extra client-generated line ID.

List query: `pageNumber` (1-based), `pageSize` (1..200), optional `warehouseId`, `status=Draft|Posted`, `search` <=128 (no NUL). **No date/Product query filter, no Sales cursor.** Response projection **proposed** `{items:[receipt summary/detail as source permits],pageNumber,pageSize,totalCount}`; B01 must confirm source `PagedResult<GoodsReceiptDto>` and freeze exact contents, sort, total/count encoding and nulls. Do not invent a new sort order or fetch every receipt's detail with N+1 calls.

### 4.3 Receipt Post acknowledgement

Request `POST /goods-receipts/{receiptId}/post`:
```json
{"expectedVersion":"BASE64_ROWVERSION"}
```
Response successful `data`:
```json
{"receiptId":"9007199254740993","status":"Posted","postedAtUtc":"2026-11-01T02:03:04Z"}
```
**Do not add `version` only for symmetry with Sales.** The source `PostGoodsReceiptResponse` has **no version**. Post ack proves the command's Post result; UI immediately marks this document Posted/read-only. Subsequent Get may return 403/500, which is **detail unavailable** and must not roll ack back, reactivate Post or fabricate a new version. A lost Post acknowledgement remains outcome-unknown, requires approved same-actor proof; no blind retry.

### 4.4 Inventory Balances (read-only)

`GET /inventory/balances`: optional `warehouseId`, `productId`, `search` <=256, required or explicitly defaulted `pageNumber/pageSize` as frozen B01. Proposed row: `warehouseId/Code/Name/IsActive`; `productId/Code/Name/IsActive`; `quantity`, `inventoryValue`, nullable `averageUnitCost`. Preserve actual SQL materialized values; fail closed on invalid Q/V or missing valuation companion. No mutation endpoint or fake recalculated average.

### 4.5 Stock Card (read-only)

`GET /inventory/stock-card`: required positive `warehouseId`, `productId`; optional `fromUtc` and `toUtc` (UTC with `Z`, from<=to); pageNumber/pageSize 1..200. Proposed row `ledgerId`, source warehouse/product identity, `postedAtUtc`, `documentType`, `documentId`, `documentNumber`, `quantityChange`, `valueChange`, in source order `PostedAtUtc, LedgerId`. Positive receipt and negative sale movements. **No running/opening balance in source contract**; do not invent it. Clicking a source document requires separate permission to read its source module; stock-card rights do not authorize it.

## 5. Strict input and error contract

Use current `StrictWireJson`/`WireInputException` and `WireApiController` pattern, with a narrowly reused safe parser or new Inventory-specific mapper. Preserve exact UTF-8, no duplicate JSON keys/extra body keys, no unrecognized query names/duplicate query params, malformed percent-decoding rejection, no implicit string-to-number conversions; validate body shape, limits, type, ranges **before mutation**. GETs reject unexpected request body. Status from transport, not DTO property.

| Status | Example | Action |
|---|---|---|
| 400 | validation, duplicate Product or invalid query/JSON | Use documented `ValidationError` / safe field errors, `lines[index].productId` maps to stable sent snapshot |
| 401 | no/revoked bearer/session | Fail closed; browser auth lifecycle; no write replay |
| 403 | `Inventory.*` permission denied | Server decides independently; no SQL modification |
| 404 | receipt not found | Safe not-found envelope |
| 409 | `GoodsReceipt.ConcurrencyConflict` / `GoodsReceipt.AlreadyPosted` | Preserve version rules; do not retry Post |
| 429 | existing auth limiter where applicable | Do not change pipeline |
| 5xx | unexpected SQL/protocol | Sanitize, correlation, never leak exception/credentials |

Read retry (GET/HEAD) after single refresh may be allowed within original actor generation, consistent with WEB2; dispatched writes are never automatically retried. Errors with no `field` must remain visible, never silently dropped.

## 6. Contract fixtures and verification before coding

B01 owns actual files under `docs/contracts/web3-v1/`, e.g. `README.md`, `manifest.json`, request/response golden JSON for every route/error and exact parser tests; these paths are **planned**, not created by this docs activation. Corpus must cover:

- `receiptId`/ledger/document IDs > 9007199254740991, max Int64, overflow, numeric token attack; int32 overflow, required/optional missing and duplicate keys.
- 1 and 200 unique Product lines; 0/201 invalid; duplicate Product on 2 rows; maximum Qty/UnitCost, exact four-fraction digits and decimal carry/tie.
- Draft Create/Update metadata including server number/opaque version and replace-lines behavior; historical inactive references/no fabricated names; null/omitted avg.
- Receipt Post ack **without version**; ack success then denied/error Get; lost ack; stale Get; 409; no retry.
- Two independent StockCard source types, signed quantities/value, nullable balances, UTC with fractions, from>to rejected, page reset and total counts.
- Each permission in allow/deny combinations, revocation, malformed response/Origin, restricted runtime SQL grants, clear unauthorized audit response.

**Contract freeze is a gate:** do not hand-code frontend schema from loosely matched C# Local serializer or treat example payloads in this document as already executable/verified. All source/wire discrepancies must be resolved and covered in canonical fixtures before implementing Web ports.
