# WEB4 v1 — canonical mutation and recovery corpus

Backend B01 contract freeze at source baseline `48e9cd7ae096ecadd184e6ea6a372306d77f41b1` (2026-11-01). These are authored, source-reviewed examples and a **test-only oracle**; none is captured WEB4 HTTP/SQL output. B03/B04 must implement this contract. [Owning plan](../../plans/20261101-003-web4-reliability-server.md), [transaction/schema/retention checkpoint](../../design/WEB4_B01_SOURCE_CHECKPOINT.md) and [operation design](../../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) own runtime obligations. Existing WEB2/WEB3 corpora remain unchanged.

`manifest.json` pins the exact bytes of every file except itself; the consumer must additionally pin the manifest hash and backend Git commit outside the corpus. Hashes establish integrity, not authentication. Directory-local `.gitattributes` requires UTF-8 without BOM and LF for all corpus files. A copied consumer directory runs independently with Node 22 built-ins, without sibling corpora or package installation. Never import this oracle into production.

## Source-verified six-command wire

The body is unchanged from `SalesWireInput.cs` / `InventoryWireInput.cs`; success remains HTTP 200 `{data:...}` as mapped by `SalesWireOutput.cs` / `InventoryWireOutput.cs` and their controllers. Operation metadata belongs to headers and the protected status envelope, never to the business body. No query parameters or trailing/alternate routes are part of this contract. Intent JSON files are test descriptions, **not HTTP request bodies**.

| operationKind | method | routeScope | original requestTargetId | current permission |
|---|---|---|---|---|
| Sales.Create | POST | /api/v1/sales-receipts | null | Sales.Receipts.Write |
| Sales.Update | PUT | /api/v1/sales-receipts/{saleId}/draft | saleId string | Sales.Receipts.Write |
| Sales.Post | POST | /api/v1/sales-receipts/{saleId}/post | saleId string | Sales.Receipts.Post |
| Receipt.Create | POST | /api/v1/goods-receipts | null | Inventory.GoodsReceipts.Write |
| Receipt.Update | PUT | /api/v1/goods-receipts/{receiptId}/draft | receiptId string | Inventory.GoodsReceipts.Write |
| Receipt.Post | POST | /api/v1/goods-receipts/{receiptId}/post | receiptId string | Inventory.GoodsReceipts.Post |

An intent has exactly `operationKind,method,routeScope,requestTargetId,body`. `routeScope` is the literal template above, while the real URL substitutes the exact canonical decimal target. Create has no target; the created ID appears **only in the original outcome**. Epoch, operation ID, actor, timestamps, labels and local line/session IDs do not enter the request fingerprint. Sales `saleLineId` is the existing **server line identity** and does enter it.

| Body | Exhaustive members (`?` optional) |
|---|---|
| Sales Create | saleDate:date, customerId:int+, warehouseId:int+, employeeId:int+, note?:text/null, lines:SalesLine[1..200] |
| Sales Update | Sales Create plus expectedVersion:SalesVersion |
| SalesLine | saleLineId:long0 (Create requires "0"), productId:int+, qty:quantity, unitPrice:price, isGift:boolean |
| Receipt Create | receiptDate:date, warehouseId:int+, employeeId:int+, note?:text/null, lines:ReceiptLine[1..200] |
| Receipt Update | Receipt Create plus expectedVersion:version |
| ReceiptLine | productId:int+, quantity:quantity, unitCost:price |
| Sales / Receipt Post | expectedVersion:SalesVersion / version only |

Unknown members, duplicate decoded property names (including escaped duplicates), numeric tokens for decimal/int64 strings, malformed Unicode and wrong JSON primitive types fail closed. Receipt duplicate Product is **Application business validation** (`GoodsReceipt.DuplicateProduct`), not wire decoding: it can be fingerprinted but cannot produce a successful Write result. Sales duplicates and gifts remain legal. The oracle does not pretend to validate live catalog state, existing Sales line ownership or permissions.

Scalars: `int+` is a positive JSON int32 token; `long+` is canonical digit string 1..9223372036854775807, `long0` also permits "0". `quantity` is a positive decimal(18,4) string, max 99999999999999.9999; `price` is a nonnegative decimal(19,4) string, max 999999999999999.9999. Decimal input length <=64, ASCII digits, optional dot with 1..4 fractional digits; leading integer zeros/trailing fractional zeros allowed. No sign, exponent, whitespace, rounding or numeric token. Output amount is nonnegative decimal(28,4). Gregorian date is `yyyy-MM-dd`, 0001..9999 excluding default 0001-01-01. UTC is `yyyy-MM-ddTHH:mm:ss[.1..7 digits]Z`; never lose SQL datetime2 precision through JS Date. Note max1000 UTF-16 units, no NUL, well-formed Unicode; omitted/null both map to null and empty stays empty. All text preserves Unicode code points without NFC/NFD normalization.

`version` is canonical Base64 of exactly 8 bytes. **Existing Sales input differs:** `SalesWireInput.Version` uses `Convert.FromBase64String`, max128 characters; ASCII space/tab/CR/LF and unused nonzero pad bits are accepted by that decoder. The oracle accepts these existing input semantics and canonicalizes the decoded 8 bytes back to Base64 before hashing. Receipt remains canonical-only. Output versions are always canonical and opaque. B04 must not silently narrow old Sales bodies or alter global serialization.

## Original command result allowlist

Each `*-response.json` is the **full original authorized command result**, not a subsequent Get. Status nests that exact envelope under `outcome`. All six `*-status.json` examples bind back to their matching `*-intent.json` and recover original versions/ordered server line IDs. Labels are only those already in the Write DTO. Read permission is not required; current corresponding Write/Post and current SQL-backed session/account are required.

| Result | Exhaustive members |
|---|---|
| Sales Create/Update data | saleId, saleNumber, saleDate, customerId, warehouseId, employeeId, status:"Draft", version, lines, totalAmount, customerIsActive:true, warehouseIsActive:true, employeeIsActive:true, customerDisplayName, warehouseDisplayName, employeeDisplayName; optional nonnull note |
| Sales result line | saleLineId, lineNo, productId, qty, unitPrice, isGift, lineAmount, productIsActive:true, unit, productDisplayName |
| Receipt Create/Update data | receiptId, receiptNumber, receiptDate, warehouseId, warehouseDisplayName, warehouseIsActive, employeeId, employeeDisplayName, employeeIsActive, status:"Draft", note, postedAtUtc:null, version, lines |
| Receipt result line | receiptLineId, lineNumber, productId, quantity, unitCost, productDisplayName, productIsActive |
| Sales Post data | saleId, status:"Posted", postedAtUtc, **version** |
| Receipt Post data | receiptId, status:"Posted", postedAtUtc — **no version** |

Sales Draft omits null `postedAtUtc`, postedUnitCost/costAmount, totalCost/grossProfit and null note under existing global null-ignore semantics. Source `SalesReceiptFeatures.ToDraftDto` hydrates authorized reference labels and active flags before Commit. SalesReferenceLoader builds nonnull labels for existing references; References validation requires all active, and Unit nonblank/max32. Source label maximum is291 UTF-16 units (Code32 + separator3 + Name256). Therefore full original Write requires unit and all labels (a later Get may have other null/inactive semantics). Receipt requires explicit null fields locally: `GoodsReceiptFeatures.ToDto` uses constructors in `InventoryContracts.cs` that supply **null labels and true active defaults**, not current Get labels. Do not turn those defaults into a new catalog-authorization claim. Receipt Update deletes/reinserts lines and returns replacement server IDs; Sales preserves existing nonzero IDs and assigns new IDs for zero. Returned line numbering is 1..N in array order, unique line IDs; Receipt also unique products. Gifts retain quantity/price and have exact zero revenue. Sales amount uses exact multiplication with four-place midpoint-away-from-zero; the oracle checks totals without binary floats.

Numbers are illustrative, including values above JS safe integer. They do not imply a live SQL record. The response/body fixtures retain the previous source-reviewed examples; original source semantics above, not an old fixture's status label, determine compatibility.

## Canonical serialization: web4-v1

1. Strictly decode UTF-8 JSON with no BOM, duplicate decoded names, extensions, noninteger numeric tokens, invalid strings or unknown fields. Validate the operation/body/route/target schema above. int32 fields are the only JSON numbers in a valid request; reject unsafe integer tokens before conversion. Do not pass raw JSON through plain JSON.parse and lose duplicate keys. The exported parser limits nesting to the existing host depth15. HTTP Content-Type remains application/json with absent/UTF-8 charset and no content encoding.
2. Construct an object with exactly `contract:"web4-v1"` plus the five intent members. On Save, materialize missing note as null. Convert quantity and price strings with exact integer/decimal arithmetic to one canonical nonnegative string: no leading zeros except integer zero, exactly four fractional digits. Thus `0001.25`, `1.2500` hash equally; `1.2501` does not. Normalize Sales expectedVersion from its decoded bytes as above. Preserve every other string value exactly, including calendar date, server line IDs, note empty/null difference and Unicode normalization form.
3. Serialize objects with member names in ascending ASCII ordinal order at **every nesting level**. All contract member names are ASCII. Arrays retain original order, including duplicate Sales products. Emit compact JSON without spaces or final newline. JSON strings use `"` and `\\` for quote/backslash, the short escapes `\b,\t,\n,\f,\r`, and lowercase `\u00xx` for other U+0000..001F (NUL already rejected); do not escape solidus or non-ASCII Unicode. Supplementary Unicode emits its scalar UTF-8 sequence, not literal surrogate escapes. U+2028/U+2029 remain literal UTF-8. Boolean/null tokens lowercase; int32 numbers decimal without sign/leading zeros.
4. Hash exactly those UTF-8 bytes using SHA-256, render 64 lowercase hex. No BOM, newline, salt, actor, epoch or operation key is included. SQL separately scopes actor/key/epoch and prevents cross-context disclosure. Every method/route/kind/target/body/version/ordered line/gift/server-line-ID difference changes identity unless explicitly normalized above.

`fingerprint-vectors.json` stores **independently authored expected canonical strings and SHA-256**, computed by Python json/Decimal/hashlib in `generate-vectors.py` without importing/executing the JavaScript oracle. JS tests compare both exact bytes and hashes. Includes six commands, max precision, long IDs, 1/200 lines, null/missing/empty, Unicode/escaping/normalization, order, gift, target, line ID, version and Sales version decoding. Negative tests additionally cover duplicate fields, unknown/future fields, malformed scalars and inconsistent route/kind. Changing the fixtures requires reviewing expected values and repinning the manifest; normal verification never regenerates them.

Canonical equivalence permits transport reformatting; it does **not** authorize changing the frozen user intent after dispatch. A deliberate same-key resend uses the immutable original request and headers; no edited draft is substituted even when its current fingerprint happens to compare equal.

## Epoch acquisition, headers and protected status

New frozen B04 route: **GET /api/v1/operations/context** (literal route registered before operation-ID route), no body/query, authenticated Bearer plus authoritative current SQL session/account. HTTP200 exactly `{data:{deploymentEpoch,actorUserId,contractVersion:"web4-v1"}}`; actorUserId positive int32 from server identity, never request authority. No separate Write/Post grant is required merely to acquire context. `deploymentEpoch` is a canonical lowercase nonnil GUID stored in the authoritative Web DB; provision v4 and treat opaquely, stable across workers/restarts, operator-rotated for clone/restore/rewind/replacement. Context never returns DB name, SQL connection, JWT or cookie. Invalid auth 401; unavailable/schema/epoch source fails503. Context is not implemented by B01 and existing auth/me is unchanged.

In WEB4 mode each six-command write requires exactly one `Idempotency-Key` (lowercase RFC4122 GUIDv4, D form, no whitespace/nil) and exactly one `X-MyDmsVn-Deployment-Epoch`. Status **GET /api/v1/operations/{operationId}** requires the epoch plus exactly one `X-MyDmsVn-Operation-Fingerprint` (64 lowercase SHA256 hex), recomputed from the frozen intent. No request body/query. Header names case-insensitive, values strict; duplicate instances or comma-joined lists fail400 even if equal. Operation ID path uses the same key grammar. No client-supplied actor/session authority or automatic new key. Header missing/malformed/duplicate returns generic400 `ValidationError`. Valid but stale epoch returns409 `Deployment.EpochMismatch` **before mutation/journal dispatch or result disclosure** against that request's authoritative DB.

Fresh current auth and operation grants are enforced before any existing result is exposed. Lookup only the current actor's key; missing and other actor are identically404 `Operation.NotObserved`, empty details. No affirmative absence or PROCESSING response exists. An owned journal row with mismatched expected fingerprint returns generic409 `Idempotency.FingerprintMismatch` without outcome. Same key changed canonical write returns409 `Idempotency.KeyConflict`, never executes new business logic. Current grant revoked returns403; authentication invalid401. Permission/body validation for an unseen request remains existing source behavior; replay must not rerun mutable business validators or perform Get.

Status HTTP200 data has exactly `deploymentEpoch,operationId,operationKind,method,routeScope,requestTargetId,requestFingerprint,state,outcome`. Server derives every identity member from authoritative actor+key+DB journal, not echoing unchecked input. `state` is `COMMITTED` with full original `{data:...}` outcome, or `RESULT_EXPIRED` / `RESULT_UNAVAILABLE` with **explicit null** outcome. The latter states disclose no result and never unlock Edit. Original request target is null on Create even after a real created ID exists in outcome. No generic created resultDocumentId is added alongside identity. Update/Post outcome ID must equal request target. Bare document ID, synthesized version, truncated Write result, wrong route/epoch/hash, future field or late old response cannot settle.

All context/status responses and failures, and keyed write/replay responses, require `Cache-Control: no-store`, `Pragma: no-cache`, `Expires: 0`; never cache through service workers, ETags or intermediary revalidation. This is mandatory runtime behavior, not proven by fixture parsing. Protected errors have the existing exact `{error:{code,message,details:[],correlationId?}}` envelope; no result/actor/existence/SQL detail. Optional correlation is 1..128 ASCII alphanumeric/_/-. Message is generic, not a parser discriminant. Missing/format validation may use bounded existing ValidationError details, without secrets.

| HTTP | Code / meaning |
|---|---|
| 400 | ValidationError; malformed/missing/duplicate operation header, malformed key/path/body |
| 401 / 403 | Auth.Unauthorized / Auth.Forbidden; no recovered data |
| 404 | Operation.NotObserved; missing and other actor indistinguishable, **not rollback** |
| 409 | Deployment.EpochMismatch / Idempotency.FingerprintMismatch / Idempotency.KeyConflict |
| 410 | Operation.ResultExpired; write resend hits expired permanent tombstone, never reexecute |
| 503 | Operation.ResultUnavailable; write replay has unavailable result, never reexecute |
| 503 | Operation.Unavailable; context/journal service unavailable; outcome unknown |
| 500 | InternalError; sanitized, never implies rollback |

Existing Sales/Receipt validation, NotFound, ConcurrencyConflict and AlreadyPosted errors remain unchanged for first execution; their existing corpora own those business errors. The WEB4 bounded error fixtures add operation protocol failures, not a replacement business error vocabulary. Receipt Post still has no version even after later Get403.

## Resend, retention and settlement boundaries

Read `protocol-scenarios.json` as **required future behavior**, not SQL/browser test evidence. A persisted-before-dispatch intent and a delayed in-flight request can both yield404. Neither allows a new key or automatic resend. Only explicit user confirmation of the frozen same-key request may resubmit, **after B03/B04 tests prove cross-worker SQL serialization, concurrent delayed attempts, crash/restart safety, current permission/epoch checks and durable non-reusable tombstones**. Those gates are not satisfied by B01. No timer/background/reload/service-worker POST, no automatic deadlock/timeout retry. A known failure does not erase an older unresolved attempt.

The [checkpoint](../../design/WEB4_B01_SOURCE_CHECKPOINT.md) owns field-classified result storage, UTF-8 size cap, seven-day bounded result policy and permanent actor/key/epoch/hash tombstones. Expiry never authorizes a new execution. Production backup/at-rest/operator retention approval remains separate. Source capture is precommit in the same UoW and SQL transaction; no HTTP post-commit Get reconstruction. Store no JWT, refresh cookie, credentials, raw request/draft or uncontrolled exception text.

Before applying any parsed status, caller must additionally pass its captured current actor, authentication generation, epoch, document/session identity and close admission. `validateStatus(payload,intent,{operationId,deploymentEpoch})` validates server-envelope binding and recomputes the hash; it cannot establish current caller identity or SQL authorization. It returns true only for a complete COMMITTED result, false for the two nonsettling states, throws for mismatch. A correct old envelope after actor/generation switch is still rejected by caller admission. Clear unsafe actor-only layout auto-restore; both layout descriptors and drafts partition by authenticated epoch+actor. Unknown stays locked until positive evidence or the reviewed operator path. No terminal NOT_EXECUTED/CANCELLED endpoint is frozen here; an operator must prove a durable fenced do-not-execute tombstone before any future negative unlock, otherwise UNKNOWN remains.

## Executable verification and consumer API

```powershell
node docs/contracts/web4-v1/validate-corpus.mjs
node --test docs/contracts/web4-v1/validate-corpus.test.mjs
```

Exports: `parseStrictJson(text)`, `validateRequest(intent)`, `canonicalizeRequest(intent)` (canonical string), `fingerprintRequest(intent)` (hex), `validateOutcome(operationKind,envelope)`, `validateStatus(envelope,intent,identity)`, `validateContext(envelope)`, `validateHeaders([[name,value],...],"write"|"status"|"context",{deploymentEpoch?,requestFingerprint?})`, `validateError(envelope,httpStatus)`, `validateCorpus(directory?)`; constants `operations` and `errorStatuses`. Shape validators throw on rejection. Parsed-object entry points require callers to use the strict raw parser first. The optional comparison arguments to validateHeaders are test expectations, not substitutes for server SQL checks.

The manifest validator checks strict text encoding/line endings, complete local file coverage, every byte hash and fixture schema, six-command identity/outcomes, independent fingerprint vectors and retention nonsettlement. The node:test suite exercises tampered copied fixtures, changed request/result identity, malformed protocol input and numeric boundaries. A constructed source-bound 200-line Sales response with note1000, labels291, unit32 and six-byte control escapes verifies the selected 1MiB UTF-8 cap; Receipt also exercises200 lines and maximal decimal scalars. Its temporary directory comes from mkdtemp and is the only deletion target. Re-authoring utilities `generate-vectors.py` and `build-manifest.py` require an intentional reviewed contract change; F01 copies exact bytes and pins them, never regenerates expectations to make a failing consumer pass.
