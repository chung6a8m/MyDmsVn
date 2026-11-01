# WEB3 v1 — canonical Goods Receipt / Inventory corpus

**Owner:** backend WEB3-B01. **Source baseline:** `8b599c782816d9cd64f04a45d809a2b8eb1235c8`. This is a source-reviewed contract checkpoint, not captured HTTP output. The [owning plan](../../plans/20261101-002-web3-goods-receipt-inventory-http.md) records review/acceptance state. B02/B03 routes, mappers, capabilities and SQL privilege changes are **not implemented by B01**. Existing Sales/Local serialization is unchanged.

[Manifest](manifest.json) associates every golden file with its logical route, kind, transport status and SHA-256. The directory-local `.gitattributes` keeps this corpus LF even when the repository's general Windows JSON/Markdown policy is CRLF. [Source checkpoint](../../design/WEB3_B01_SOURCE_CHECKPOINT.md) owns source anchors and remaining implementation obligations; [wire design](../../design/WEB3_INVENTORY_HTTP_WIRE.md) owns security and transport. This README owns exhaustive field/lexical tables. Do not create a second authoritative fixture set in Web: consume this directory at an exact backend commit and retain byte hashes/provenance. Mutate clones for tests.

## Scalars and nulls

| Name | JSON and range |
|---|---|
| `long+` | Canonical integer **string** `1..9223372036854775807`; `long0` additionally allows `"0"` for counts only. No numeric tokens, sign, leading zeros, exponent or whitespace. |
| `int+` | JSON integer number `1..2147483647`. URL equivalents are canonical decimal digits, no sign/leading zero/exponent. |
| `quantity` | Positive decimal string, `decimal(18,4)`, max `99999999999999.9999`. Balance quantity permits zero. |
| `price` | Nonnegative decimal string, `decimal(19,4)`, max `999999999999999.9999`. Receipt Save `unitCost` uses this shape. |
| `amount` | Nonnegative decimal string, `decimal(28,4)`, max `999999999999999999999999.9999`. Stock-card quantity/value use signed quantity/amount bounds. |
| `average` | Nonnegative decimal string, `decimal(28,8)`, max `99999999999999999999.99999999`, or explicit null. Do not use the four-place Save cost parser. |
| `date` | Real Gregorian `yyyy-MM-dd`, years 0001..9999; ReceiptDate rejects default `0001-01-01`. No timestamp or timezone conversion. |
| `utc` | `yyyy-MM-ddTHH:mm:ss[.1..7 digits]Z`, years 0001..9999, no leap seconds, local offset or unspecified suffix. SQL datetime2 UTC with Unspecified kind is labeled UTC without local-offset shifting, following existing Sales mapper semantics. Preserve all seven fractional digits. |
| `version` | Canonical Base64 encoding exactly eight bytes (12 characters, one final `=`); opaque to browser. Never compare, predict or increment it. |

Decimal grammar permits at most **64 characters**, ASCII digits with an optional decimal dot and **1..scale lexical fractional digits**; no exponent, comma, plus, whitespace, empty fractional part or JSON numeric token. Leading integer zeros and trailing fractional zeros within the scale are allowed, matching the existing Sales input grammar. A fifth quantity/cost fractional digit is rejected even when zero; no silent rounding. Signed stock output permits `-` only for negative nonzero values; all negative-zero spellings are rejected. Four-place amount preview rounds nonnegative `quantity * unitCost` halfway away from zero using exact integers. Field maxima are independent: their product need not fit the Post valuation cap; Draft validation does not promise Post success.

**WEB3 response nulls are explicit required members:** `note`, `postedAtUtc`, `warehouseDisplayName`, `employeeDisplayName`, line `productDisplayName`, and `averageUnitCost`. A null average is undefined, not zero. The future WEB3 mapper must emit these nulls locally despite existing `ApiJson.NullValueHandling.Ignore`; do not change global ApiJson or Sales/Local omission semantics. Save input `note` may be omitted or null (both map to null), or a string (empty stays empty), maximum 1000 UTF-16 units and no NUL. All strings require well-formed Unicode.

## Exhaustive fields

`?` means the input member may be absent. All other members below are required, including response members that permit null. Extra members fail closed. Every success is HTTP **200**, including Create/Post; HTTP status is not a body member. Success envelope is exactly `{data:...}`.

| Shape | Fields |
|---|---|
| Create | `receiptDate:date,warehouseId:int+,employeeId:int+,note?:string|null,lines:SaveLine[]` |
| Update Draft | Create fields plus `expectedVersion:version`; `receiptId:long+` only in route |
| SaveLine | `productId:int+,quantity:quantity,unitCost:price` only |
| Post request | `expectedVersion:version` only; receipt ID in route |
| Receipt detail | `receiptId:long+,receiptNumber:string,receiptDate:date,warehouseId:int+,warehouseDisplayName:string|null,warehouseIsActive:boolean,employeeId:int+,employeeDisplayName:string|null,employeeIsActive:boolean,status:"Draft"|"Posted",note:string|null,postedAtUtc:utc|null,version:version,lines:DetailLine[]` |
| DetailLine | `receiptLineId:long+,lineNumber:int+,productId:int+,quantity:quantity,unitCost:price,productDisplayName:string|null,productIsActive:boolean` |
| Receipt Post ack | `receiptId:long+,status:"Posted",postedAtUtc:utc` — **no version** |
| Page | `items:Row[],pageNumber:int+,pageSize:1..200,totalCount:long0` (string count, never JS Number) |
| Balance row | `warehouseId:int+,warehouseCode:string,warehouseName:string,warehouseIsActive:boolean,productId:int+,productCode:string,productName:string,productIsActive:boolean,quantity:nonnegative quantity,inventoryValue:amount,averageUnitCost:average|null` |
| Stock-card row | Balance reference identity/label/active fields plus `ledgerId:long+,postedAtUtc:utc,documentType:string,documentId:long+,documentNumber:string,quantityChange:signed quantity,valueChange:signed amount`; no quantity/value balance, average, opening/running balance or product unit |
| Receipt/Sales capabilities | `read:boolean,write:boolean,post:boolean` |
| Balance/StockCard capabilities | `read:boolean` only |

Receipt List uses **full Receipt detail rows including lines**, not a summary DTO. Draft `postedAtUtc` is null; Posted requires UTC. Receipt number is `GR` + exactly ten digits for the current SQL sequence range 1..9999999999, NO CYCLE ([migration006](../../../src/MyDmsVn.Server.DbMigrator/Scripts/006_GoodsReceipt_Number_Sequence.sql)); gaps are valid and the browser never predicts the next value. Receipt lines are 1..200, unique Product IDs, unique positive returned line IDs and contiguous `lineNumber` 1..N in returned order. Save never sends line IDs. Create/Update materialize the response before commit; inserts mutate the same line objects with positive SQL identities. Update deletes/reinserts all lines and returns fresh identities. Reconcile captured sent order/lineNumber only after cardinality/order/product verification; client identity remains private.

Create/Update labels are explicitly null with source-default active flags true. Authorized Get/List supply current catalog `Code - Name` and activity flags, including inactive references; they are not historical label snapshots. Preserve selected UI labels separately when necessary and never replace authoritative Get labels with active-only lookup guesses. Successful writes do not depend on later Read permission. Post ack confirms Posted even when the separate error fixture models a denied/unavailable detail read; a fixture does not exercise a real auth transition or prove recovery.

Balances use physical Q/V and server average rounded to eight places: Q=0 requires V=0 and null average. Card movements for `GoodsReceipt` have positive Q/nonnegative V; `SalesReceipt` has negative Q/nonpositive V, including zero-value inventory. Preserve unknown source types as read-only server strings with **no inferred navigation**; SQL has a `Type#Id` fallback and does not constrain the ledger source string to these two modules. Missing valuation companion fails server-side instead of producing a fake value.

## Query and ordering contract

Query JSON fixtures are typed logical maps, **not GET bodies**. URLs encode once; GET bodies are forbidden. `pageNumber` and `pageSize` are required on all three endpoints: no HTTP defaults. UI sends 1/25 initially. `pageNumber` is positive int32; pageSize 1..200. Unknown or duplicate decoded query names, malformed percent/UTF-8, numeric ambiguity and unsupported filters fail 400.

| Query | Optional fields and source order |
|---|---|
| Receipt list | `warehouseId:int+,status:"Draft"|"Posted",search:string` (max128/no NUL). No Product/date/cursor filter. SQL trims search; blank→null. Matches receipt number, warehouse/employee code/name and Product code/name through EXISTS, not Note. Sort ReceiptDate DESC, ReceiptId DESC; lines LineNo, ReceiptLineId. |
| Balances | `warehouseId:int+,productId:int+,search:string` (max256/no NUL). Sort warehouse.Code, WarehouseId, product.Code, ProductId in SQL collation. |
| Stock Card | **Required** warehouseId/productId; optional `fromUtc:utc,toUtc:utc`, inclusive boundaries and from<=to. Sort PostedAtUtc, LedgerId ascending. |

Unknown/blank supplied status is invalid HTTP input even though Local validator permits whitespace as no filter. Omit optional filters to mean no restriction. SQL counts are COUNT_BIG/Int64. Receipt reads use a Serializable transaction; inventory count/rows have no explicit shared transaction, so they do not promise a snapshot under concurrent posting. Do not require `items.length <= totalCount` for inventory pages, synthesize counts, fetch all pages, or invent stable offset positions after mutation.

Capabilities whitelist remains explicit: `sales`, **`goods-receipts`**, `inventory-balances`, `inventory-stock-card`. Unknown/multiple feature values or other keys fail 400. The frontend module name `goods-receipt` is intentionally distinct from the HTTP feature `goods-receipts`. Receipt Write/Post do not imply Read; StockCard.Read does not grant receipt/Sales/balance/catalog access. Server enforcement remains per operation; boolean fixtures are hints, not grants.

## Error and recovery contract

Failure envelope: `{error:{code:string,message:string,details:[{code:string,message:string,field?:string}],correlationId?:string}}`. Omit absent field/correlation, preserve fieldless issues. Correlation is 1..128 ASCII letters/digits/underscore/hyphen. Existing mapper removes `Request.` and lower-camels paths, e.g. `Request.Lines[1].ProductId` → `lines[1].productId`. Map via the captured ordered sent snapshot, not current grid order.

| HTTP | Aggregate code / detail |
|---|---|
| 400 | `ValidationError`; details include `GoodsReceipt.DuplicateProduct`, `Validation.*`, reference `GoodsReceipt.{Warehouse|Employee|Product}{NotFound|Inactive}` or fieldless `GoodsReceipt.ValuationOutOfRange` |
| 401 | `Auth.Unauthorized`, empty details |
| 403 | `Auth.Forbidden`, empty details |
| 404 | `GoodsReceipt.NotFound` |
| 409 | `GoodsReceipt.ConcurrencyConflict` or `GoodsReceipt.AlreadyPosted` |
| 429 | Existing auth-only `Auth.RateLimited`, empty details; business routes do not acquire a new limiter |
| 500 | `InternalError`, sanitized message, empty details |

Existing auth rate limiting/Origin/CSP/session behavior is unchanged; the corpus does not broaden those routes. No dispatched Create/Update/Post automatic retry, even after refresh/timeout/abort. Ack success versus lost ack are different states. Post ack contains no version; do not fabricate one or revert confirmation when Get fails. Outcome-unknown, revocation, actual grants, rollback and SQL atomicity require B02–B05/runtime evidence and frontend session tests, not this pure corpus.

## Executable checks and consumption

```powershell
node docs/contracts/web3-v1/validate-corpus.mjs
node --test docs/contracts/web3-v1/validate-corpus.test.mjs
```

Node 22 built-ins only; no install, credentials, HTTP, SQL, generated application code or product build. The validator exports `validateFixture(kind,payload)`, `validateScalar(scalar,value)`, `parseStrictJson(text)`, `validateQuery(kind,rawQuery)` and `validateCorpus(directory)`. `corpus-vectors.json` holds scalar accept/reject vectors, exact rounding controls and structural cases. `expandLines` constructs actual 1/200/201 unique-Product arrays before validating; it is not a count-only assertion. Negative cases exercise typed/scalar/semantic rejection, null omission, duplicate keys, escaped duplicates, malformed query encoding and time ranges. Corpus hashes are verified before execution; temporary tampering tests remove only their mkdtemp-owned directory.

Frontend may mirror exact bytes with pinned backend SHA and provenance hashes, including this validator, to run without a sibling checkout. This is a test oracle, never a production port. Existing Web shared Sales parsers may accept lexical forms WEB3 rejects (more than seven UTC fractional digits, negative zero); F03 must narrow at its boundary rather than weakening this corpus or changing historical Sales behavior. New tests must not mistake a common numeric helper for a complete receipt codec.
