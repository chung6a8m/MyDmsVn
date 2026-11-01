# WEB3 — Inventory / Goods Receipt HTTP wire contract v1 (B01 checkpoint)

**Decision scope:** Owner chose WEB3 on 2026-11-01 and explicitly authorized backend B01 to complete the contract checkpoint. This is the normative contract frozen by B01. B02/B03 now implement it; [execution evidence](../evidence/web3/20261101-b02-b03-http-inventory.md) owns the bounded HTTP/TestServer + SQL verification, not IIS/browser or production acceptance. [Canonical executable corpus and exhaustive field tables](../contracts/web3-v1/README.md), [manifest](../contracts/web3-v1/manifest.json) and [source/SQL gap checkpoint](WEB3_B01_SOURCE_CHECKPOINT.md) resolve the source reconciliation against `8b599c782816d9cd64f04a45d809a2b8eb1235c8`. Review/acceptance state remains in the [B01–B05 owning plan](../plans/20261101-002-web3-goods-receipt-inventory-http.md). Existing [WEB2 host/auth contract](WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md), [Contracts](../CONTRACTS.md), [Data model](../DATA_MODEL.md) and exact source remain authorities for current runtime. This Web wire does **not** change Local/Desktop JSON DTO shape or Sales behavior.

## 1. Transport, security and authority

- Extend the existing `net48` System.Web WebAPI OWIN host at `/api/v1`, through `Web2Pipeline`, not a new service. Reuse its HTTP envelope `{data:...}` / `{error:{code,message,details,correlationId}}`, guarded Bearer, current SQL-backed active account/session and per-request actor. `GET health` and Sales/Catalog remain unchanged.
- Existing access JWT: short-lived Bearer in browser localStorage. Refresh GUID only in `__Host-mydmsvn-rt` HttpOnly Secure SameSite=Strict Path=/ cookie, SQL rotation/replay. Business API uses `Authorization: Bearer` and excludes refresh cookie; cookie-changing auth endpoints keep exact Origin/CSRF. Owner accepted storage residual XSS risk, not a change to cookie auth/BFF.
- Existing host HTTPS/same-origin, trusted Host/Origin, per-request body limits, security headers/CSP nonce, reject Forwarded input and no wildcard CORS still apply. Client-supplied actor, permission key, cost/stock totals, posted status, receipt number or SQL IDs do not convey authority.
- Server `Application` handlers enforce code-defined **active** permissions; UI capabilities are display hints only. Denials must not mutate business data; 401 for absent/expired/revoked actor, 403 for lacking specific operation, 409 conflict for stale/already-posted.
- **No automatic retry** for any dispatched Create/Update/Post. Failed/timeout response after dispatch may have committed; client freezes unknown until authoritative reconciliation. Never infer transaction rollback from client AbortSignal. No operation journal/idempotency keys in WEB3.

## 2. Routes and exact ownership

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
| `lineNumber`, `pageNumber`, `pageSize` | checked JSON int32 integer | 1-based pages; 1..200 pageSize; explicit required pageNumber/pageSize, no HTTP defaults |
| `totalCount` (source long / SQL COUNT_BIG) | canonical nonnegative Int64 **string** | `"0"`..`"9223372036854775807"`; never JS Number |
| All C# decimal including `quantity`, `unitCost`, `inventoryValue`, `averageUnitCost`, `quantityChange`, `valueChange` | **string** invariant decimal | No JSON number/float; no exponent; exact precision/scale, reject NaN or silent rounding |
| Nullable `averageUnitCost` | string or explicit null | Null means undefined, not zero; response member required. Eight fractional digits/decimal28,8, unlike Save unitCost19,4. Amounts use decimal28,4. |
| `receiptDate` | `yyyy-MM-dd` string | Calendar date, no timezone conversion; leap day valid |
| `postedAtUtc`, `fromUtc`, `toUtc` | ISO 8601 UTC with `Z`, optional 1..7 fractional digits | SQL Unspecified-kind UTC is labeled without server-local shift; no browser-local drift |
| `expectedVersion` / detail `version` | opaque Base64 string | Source rowversion is 8 bytes; no client comparison arithmetic |
| `receiptNumber` | server-generated display string | GR + ten digits, sequence1..9999999999/NO CYCLE; gaps valid; never client-supplied |

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

The existing Create/Update handlers compute and materialize `GoodsReceiptDto` **before COMMIT**. Insert/replace assigns positive SQL IDs to the same line objects, so success is authoritative for Draft status, ID, number, version and ordered lines without a later Get. Source `ToDto` returns null header/line display names and default active flags true; authorized Get/List supply current reference labels/flags including inactive references. WEB3 emits explicit nullable members per the corpus, using a local projection that does not alter global null-ignore serialization. Frontend retains selected labels separately or fetches authorized detail, never fabricates historical names or treats failed Read as failed Save.

### 4.2 Detail and list pages

Receipt detail names/nullability are frozen in the [canonical field tables](../contracts/web3-v1/README.md#exhaustive-fields). Header ID maps from source `Id` to `receiptId`, line `Id` to `receiptLineId`, while `LineNumber` stays `lineNumber`. Enforce string int64/decimal, explicit response nulls and no extra client-generated ID.

List query requires `pageNumber` (1-based), `pageSize` (1..200); optional warehouseId, exact status=Draft|Posted, search<=128/no NUL. **No date/Product query filter or Sales cursor.** Source is `PagedResult<GoodsReceiptDto>`: items are full detail including lines; sort ReceiptDate DESC/ReceiptId DESC, lines LineNo/ReceiptLineId. TotalCount is an Int64 string. Preserve the set-based batch query; no N+1. Inventory pages do not promise a transactional count/rows snapshot.

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

`GET /inventory/balances`: optional warehouseId/productId/search<=256, **required explicit** pageNumber/pageSize. Full row/scale/null table is in the corpus. Preserve actual SQL values; fail closed on invalid Q/V or missing valuation companion. No mutation endpoint or fake recalculated average.

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

B01 owns the [actual files](../contracts/web3-v1/README.md), [manifest with SHA-256](../contracts/web3-v1/manifest.json), route golden payloads and pure Node semantic tests. These are authored fixtures, not running-server captures. Coverage includes:

- `receiptId`/ledger/document IDs > 9007199254740991, max Int64, overflow, numeric token attack; int32 overflow, required/optional missing and duplicate keys.
- 1 and 200 unique Product lines; 0/201 invalid; duplicate Product on 2 rows; maximum Qty/UnitCost, exact four-fraction digits and decimal carry/tie.
- Draft Create/Update metadata including server number/opaque version and replace-lines behavior; historical inactive references/no fabricated names; null/omitted avg.
- Receipt Post ack **without version**; ack success then denied/error Get; lost ack; stale Get; 409; no retry.
- Two independent StockCard source types, signed quantities/value, nullable balances, UTC with fractions, from>to rejected, page reset and total counts.
- Each permission in allow/deny combinations, revocation, malformed response/Origin, restricted runtime SQL grants, clear unauthorized audit response.

**Contract freeze is a gate:** consume the pinned corpus, not a loosely matched C# Local serializer. B01 source discrepancies are reconciled in the checkpoint; review/acceptance is tracked separately. B02/B03 must prove actual strict HTTP projection, permission denies and least-privilege SQL with source-owned tests before live Web ports are accepted. Pure corpus PASS is not HTTP/SQL PASS.
