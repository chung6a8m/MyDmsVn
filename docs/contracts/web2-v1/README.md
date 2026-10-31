# WEB2 v1 — canonical wire corpus

**Owner:** backend repository; source inspected at `e657d8d`. [Wire/auth design](../../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md) owns protocol and security rules. This directory owns the exhaustive field tables and named examples for WEB2-01; [manifest.json](manifest.json) associates payloads with routes and actual HTTP status. Frontend references this corpus rather than copying it.

These are proposed HTTP v1 payloads, not captures from a running server. The dedicated host mapper (WEB2-05) and browser codec (WEB2-08) must consume these fixtures in their own executable tests. Existing Local DTOs and `ApiJson` remain unchanged; they currently emit numeric `long`/`decimal`, so serializing Local DTOs directly does **not** implement this wire contract. HTTP success is `200` for the routes below, including Create and the Post acknowledgement. Auth refresh has an empty body; logout also has an empty body. There is no refresh credential in any JSON payload.

## Source ownership

| Concern | Inspected source |
|---|---|
| Success/error, camelCase, omission of null, transport-only status | [ApiResponse](../../../src/MyDmsVn.Contracts/ApiResponse.cs), [ApiJson](../../../src/MyDmsVn.Contracts/ApiJson.cs), [ApiError](../../../src/MyDmsVn.Contracts/ApiError.cs), [ApiErrorDetail](../../../src/MyDmsVn.Contracts/ApiErrorDetail.cs), [ApiResponseMapper](../../../src/MyDmsVn.Server.Application/ApiResponseMapper.cs) |
| Current user and login input | [IdentityContracts](../../../src/MyDmsVn.Contracts/IdentityContracts.cs), [LoginCommandHandler](../../../src/MyDmsVn.Server.Application/Identity/LoginCommandHandler.cs) |
| Sales payload fields, caps and nullable Draft/Posted state | [SalesContracts](../../../src/MyDmsVn.Contracts/SalesContracts.cs) |
| Save validation, create-zero and independent saved identities, version validation | [SalesReceiptFeatures](../../../src/MyDmsVn.Server.Application/Sales/SalesReceiptFeatures.cs) |
| Calendar date | [SalesCalendarDateJsonConverter](../../../src/MyDmsVn.Contracts/SalesCalendarDateJsonConverter.cs) |
| Keyset/filter binding and query limits | [SalesReceiptCursor](../../../src/MyDmsVn.Server.Application/Sales/SalesReceiptCursor.cs) |
| Revenue rounding, cost precision, signed amounts | [InventoryValuationMath](../../../src/MyDmsVn.Server.Domain/Inventory/InventoryValuationMath.cs), [schema 010](../../../src/MyDmsVn.Server.DbMigrator/Scripts/010_DirectSalesAndValuation.sql) |
| Lookup shape and limits | [CatalogContracts](../../../src/MyDmsVn.Contracts/CatalogContracts.cs), [ProductFeatures](../../../src/MyDmsVn.Server.Application/Catalog/ProductFeatures.cs), [OtherCatalogFeatureRequests](../../../src/MyDmsVn.Server.Application/Catalog/OtherCatalogFeatureRequests.cs) |

Capabilities and `{accessToken,expiresAtUtc,user}` are new HTTP shapes selected in the owning design, not existing Identity DTO members. The `read:false,write:true,post:true` capability fixture intentionally proves that Write/Post do not imply Read. Authorization still runs on each server operation. Lookup authorization uses each catalog's own Read permission from its existing query.

## Scalar notation

| Notation | JSON representation and semantic bounds |
|---|---|
| `long+` | Canonical decimal **string**, `1` through `9223372036854775807`; no sign, leading zeros, decimal point or exponent. `long0` additionally permits `"0"` for a new request line. Never convert to JS Number. |
| `int+` | JSON integer number, `1..2147483647`; catalog/user IDs stay int32. `lineNo`, `lineCount`, `pageSize`, `limit` are also numeric. |
| `quantity` | Decimal string, positive, `decimal(18,4)`, maximum `99999999999999.9999`. |
| `price` | Decimal string, nonnegative, `decimal(19,4)`, maximum `999999999999999.9999`. |
| `amount` | Decimal string, nonnegative, `decimal(28,4)`, maximum `999999999999999999999999.9999`. |
| `profit` | Signed decimal string within plus/minus the amount cap. A loss is valid. |
| `unit-cost` | Decimal string, nonnegative, `decimal(28,8)`, maximum `99999999999999999999.99999999`; retain eight-place precision. |
| `date` | Real Gregorian `yyyy-MM-dd` within .NET DateTime calendar range; `saleDate` also rejects the default `0001-01-01`. No timestamp, offset or timezone conversion. |
| `utc` | ISO UTC timestamp ending in `Z`, optional fractional seconds; distinct from `date`. |
| `version` | Opaque string to browser. Existing server validates Base64 decoding to eight bytes; never compare as an integer or derive the next version. |
| `cursor` | Opaque string, carried unchanged and URL-encoded. Existing Application validates binding to filters/pageSize/direction; browser does not decode or create cursors. |

Decimal strings use invariant digits and optional dot; no exponent, comma or whitespace. The corpus uses four-place amounts/quantities/prices and eight-place unit costs; shorter exact forms are allowed. Caps are per field, not permission to combine all maxima into one valid payable line. [numeric-vectors.json](numeric-vectors.json) includes independent caps, carry/tie and a maximal gift (zero revenue). Existing arithmetic rounds nonnegative revenue halfway away from zero; gift retains price and quantity with zero revenue and full cost.

## Envelope, error and auth fields

| Object | Fields (`?` = nullable/optional as described) |
|---|---|
| Success | `data`: non-null route value; no `error`, `isSuccess`, status or statusCode. |
| Failure | `error:{code:string,message:string,details:ErrorDetail[],correlationId?:string}`; no `data`. Correlation, when present, is 1–128 letters/digits/underscore/hyphen. Actual HTTP status lives in the manifest/transport. |
| ErrorDetail | `code:string,message:string,field?:string`; field is omitted for fieldless issues. Ordered field paths are camelCase, e.g. `lines[1].qty`. Host passes `Request` as mapper root, removing `Request.` before casing. Browser maps indices through the exact ordered snapshot sent, never the currently reordered grid. |
| Login input | `username:string,password:string`; fixture values are inert placeholders, never credentials. |
| Token result (login/refresh) | `accessToken:string,expiresAtUtc:utc,user:CurrentUser`; `FIXTURE_ONLY_NOT_A_JWT` is deliberately not signed/usable. |
| CurrentUser (`GET me`) | `userId:int+,username:string,displayName:string`; no actor/session secret or invented roles/grants. |
| Capabilities | `read:boolean,write:boolean,post:boolean`; no user/session fields or combined capability integer. |
| Logout | `data:{}` following existing `UnitResponse` empty-object convention. |

`ApiResponseMapper` maps Validation to HTTP 400 / `ValidationError`; Unauthorized to 401 / `Auth.Unauthorized`, preserving the single `Auth.InvalidCredentials` error; Forbidden to 403 / `Auth.Forbidden`. Auth details are empty. NotFound and Conflict preserve the originating business code: fixtures use 404 / `SalesReceipt.NotFound`, 409 / `SalesReceipt.ConcurrencyConflict` and `SalesReceipt.AlreadyPosted`. This avoids inventing a new generic Conflict code. Existing internal errors remain sanitized 500 / `InternalError`; no new runtime error mapping is implemented here.

## Sales request fields

Route `{saleId}` is `long+` and supplied once in the route. Update/Post bodies omit `saleId`; unknown body fields are rejected by the future host before mutation. Create must send `"0"` on **every** line (existing create validator rejects positive IDs). Update allows `"0"` for a new line and a unique existing positive ID for a retained line. Product IDs may repeat independently of saved line identity.

| Payload | Exhaustive fields |
|---|---|
| Create | `saleDate:date,customerId:int+,warehouseId:int+,employeeId:int+,note?:string,lines:SaveLine[]` |
| Update Draft | All Create fields plus `expectedVersion:version` |
| SaveLine | `saleLineId:long0,productId:int+,qty:quantity,unitPrice:price,isGift:boolean` |
| Post | `expectedVersion:version` only |

`note` has maximum 1000 characters and no NUL; it may be absent or null on input and is omitted on output when null. There must be 1–200 ordered lines. Fields such as `clientLineId`, `lineNo`, `unit`, computed amount/cost/status/number, actor identity and total are never request members. Existing business validation additionally checks references, same-product aggregate quantity and line/document amount caps. An abort does not establish rollback; these examples add no automatic write retry or idempotency guarantee.

## Sales response fields

Create/Update/Get all return the same authoritative full detail shape. Optional nullable fields follow `ApiJson` null omission. Browser normalizes omitted nullable fields to null, without inventing zero or a missing display label.

| Object | Exhaustive fields |
|---|---|
| Detail header | `saleId:long+,saleNumber:string,saleDate:date,customerId:int+,warehouseId:int+,employeeId:int+,status:"Draft"|"Posted",note?:string,postedAtUtc?:utc,version:version,lines:DetailLine[],totalAmount:amount,totalCost?:amount,grossProfit?:profit` |
| Detail labels | `customerDisplayName?:string,customerIsActive:boolean,warehouseDisplayName?:string,warehouseIsActive:boolean,employeeDisplayName?:string,employeeIsActive:boolean` |
| DetailLine | `saleLineId:long+,lineNo:int+,productId:int+,unit:string,qty:quantity,unitPrice:price,isGift:boolean,lineAmount:amount,postedUnitCost?:unit-cost,costAmount?:amount,productDisplayName?:string,productIsActive:boolean` |
| Post acknowledgement | `saleId:long+,status:"Posted",postedAtUtc:utc,version:version` only |
| List page | `items:ListRow[],nextCursor?:cursor,previousCursor?:cursor` |
| ListRow | `saleId:long+,saleNumber:string,saleDate:date,customerId:int+,customerDisplayName:string,warehouseId:int+,warehouseDisplayName:string,employeeId:int+,employeeDisplayName:string,status:"Draft"|"Posted",lineCount:int+,totalAmount:amount` |

Draft omits header `postedAtUtc,totalCost,grossProfit` and line `postedUnitCost,costAmount`; Posted requires them (including genuine zero costs). Get's historical labels and activity flags come from its own response, independently of active lookup results. Existing reference/list queries compose each available display label as catalog `code + " - " + name`. There are no separate display `code` fields in Sales detail. `unit` belongs to each detail line; generic lookup has no unit.

The sample lifecycle uses max-int64 sale identity, duplicate product lines, gift costs and a negative gross profit. Versions progress only as independent opaque samples. The Posted Get illustrates reference deactivation **after** Post; it is not a valid request to save or post an inactive reference. Post acknowledgement is authoritative even if a later Get returns the separate 403 fixture; the corpus does not simulate a real permission change or HTTP fault.

## List and lookup queries

Query JSON files are typed **logical query maps** for fixtures, not JSON HTTP bodies. URL parameters are textual and encoded once by the client. Optional omitted parameters remain absent.

| Query | Exhaustive fields and defaults |
|---|---|
| Sales list | `fromDate?:date,toDate?:date,customerId?:int+,warehouseId?:int+,employeeId?:int+,status?:"Draft"|"Posted",productId?:int+,search?:string,pageSize?:number,cursor?:cursor`. HTTP v1 defaults pageSize to **25**, range `1..200`; existing Local DTO default remains **50**. `fromDate<=toDate`; normalized search max 256 characters, no NUL, valid Unicode. |
| Four lookups | `search?:string,limit:number` (`1..200`, search max 256/no NUL). Explicit `limit:25` is the example, not an invented server default. |
| Lookup response | `data:CatalogLookup[]`; each row has exactly `id:int+,code:string,name:string,isActive:boolean`, following `CatalogLookupDto`. Existing lookup queries return active rows. Routes are products/customers/warehouses/employees. |

The initial Sales list query has pageSize 25 and no cursor. Its one-item result is a terminal page: neither cursor is present. This agrees with the existing query service: an initial forward page cannot have a previous cursor, and an underfilled forward page has no look-ahead row to justify a next cursor. The max-int64 first identity also precludes any higher identity for backward navigation. List rows retain `SaleId DESC`, all-document totals and Product Any membership semantics.

[opaque-cursor-vectors.json](opaque-cursor-vectors.json) independently supplies next/previous lexemes for lossless browser URL transport; these are not navigation claims about the terminal list fixture. Both use an anchor below max-int64 and the inspected Application field order, filter hash and Base64url rules. Golden preservation alone does not prove server cursor admission or the existence of rows on either side; WEB2-05 must exercise the existing Application parser and real keyset queries.

## Executable corpus checks and limits

Run from the backend repository root with Node 22 (no install or extra npm dependencies):

```powershell
node --test scripts/check-web2-wire-fixtures.mjs
```

The runner uses `node:test`, strict assertions and BigInt to verify exact string/JSON preservation, declared fixture shapes, boundary values, rounding vectors, lifecycle identity/order, totals/costs, null omission, historical labels, error paths/status and lookup ownership. [sales-200-lines-expansion.json](sales-200-lines-expansion.json) defines deterministic expansion of checked request/detail templates into 200 duplicate-product rows with independent persisted IDs and exact total `0.0200`.

[negative-cases.json](negative-cases.json) declares JSON-pointer mutations with expected corpus violations: numeric long/decimal, overflow, noncanonical IDs, exponent/comma decimals, invalid precision, zero quantity, int32 type/range, non-leap/timezone/default sale dates, unknown root/line fields, positive create line identity, body sale ID, null Posted cost and conflicting envelope members. A passing negative corpus assertion proves only that this preflight corpus detects the declared violation. It does **not** prove an HTTP request was rejected or a browser/runtime codec exists. No production parser/schema, JWT issuance, cookie flow, SQL authorization, mutation, IIS, browser or Local parity is exercised by this runner.
