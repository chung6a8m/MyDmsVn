# WEB4 operation journal and HTTP idempotency contract — proposed v1

**Owner:** MyDmsVn backend. **Status:** PROPOSED TECHNICAL BASELINE for WEB4-B01 review, NOT an implemented API or approved schema. **Last revised:** 2026-11-01. See [owning backend plan](../plans/20261101-003-web4-reliability-server.md), [ADR 0014](../adr/0014-web4-durable-operations.md), [backend test matrix](../development/WEB4_BACKEND_ACCEPTANCE.md) and frontend [durable recovery design](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/codex/activate-web4-reliability/docs/design/WEB4_DURABLE_DRAFT_AND_RECOVERY.md).

## 1. Source facts and deliberate change

At WEB3 main, the six Create/Update/Post handlers reside in Server.Application.Sales.SalesReceiptFeatures, Server.Application.Sales.PostSalesReceipt (Sales Post), and Server.Application.Inventory.GoodsReceiptFeatures. They open SqlUnitOfWorkFactory, BeginTransaction, execute repositories, Commit, and return. Repositories borrow ISqlExecutionContext.Connection/Transaction; HTTP controllers only MediatR.Send and wire-map. Thus an HTTP middleware that writes a journal BEFORE or AFTER MediatR.Send is **not atomic** with the business mutation. WEB4 must add a narrow transaction-aware coordination hook in the existing Application handler/UoW path; controller never writes business SQL. Application must not depend on System.Web or cookie/JWT types. Read existing authorization/validation/audit pipeline before choosing exact interception order.

Existing client guarantees remain: no automatic write retry, no operation journal, no durable drafts; loss of acknowledgement locks current session. WEB4 extends positive-evidence reconciliation, it does not assume an aborted browser request canceled an SQL transaction.

## 2. Identity, key and canonical request

- Proposed Idempotency-Key request header: one canonical lowercase RFC4122 GUIDv4 in D form (36 ASCII characters), randomly generated with cryptographically strong browser API, not a business/document ID. Exactly one instance; reject whitespace, duplicates, nil, non-v4, case/canonicalization differences with generic 400.
- Protected operation scope: current **server-validated numeric user identity** plus operation key in the authoritative SQL database; NOT browser-provided actor, JWT grant list, session generation, IP address or device. The browser's local draft namespace additionally requires a server-authenticated **deployment/database epoch** (opaque GUID stable within a logical DB, rotated on clone/restore with changed history/replacement) so same-origin userId reuse cannot expose another deployment's drafts. A new authenticated session for the same user may inspect the same operation if still currently authorized. Cross-user lookup must behave identically to missing operation and must never disclose existence.
- The key binds to EXACTLY one immutable command. Compute server-owned SHA-256 fingerprint over a documented canonical representation of [HTTP method, canonical route template, route resource ID where present, command kind, parsed authoritative business payload including expectedVersion, array order, string precision]. Do NOT hash raw untrusted HTTP bytes alone, truncate int64 into JS Number, omit sales gift, or use client line identity. Contract fixtures must cover property order/whitespace, null vs missing, numeric-string lexical grammar, duplicates and 1/200 lines.
- Every new *user intent* or changed payload/version needs a new operation ID. Retrying the same ID by an explicit user action is not allowed to reinterpret an updated draft. Reuse on another module/endpoint is a conflict, not a fresh operation.
- **Readback identity binding:** return authenticated/server-derived `deploymentEpoch`, `operationId`, `operationKind`, `method`, canonical `routeScope`/resource identifier and `requestFingerprint` (SHA-256 lowercase hex over the frozen authoritative input) in the protected status envelope. Bind those fields to the same stored actor+operation key+journal row, never to user-provided response metadata; validate even when the result is COMMITTED. Clients must rederive the fingerprint from their immutable persisted intent using the B01 canonical corpus, not trust the locally stored hash as authoritative. Wrong/missing kind, route, target, epoch or fingerprint is NOT a positive result.

## 3. Routes and compatibility

The six existing routes are unchanged:
- POST /api/v1/sales-receipts (Create);
- PUT /api/v1/sales-receipts/{saleId}/draft (Update);
- POST /api/v1/sales-receipts/{saleId}/post (Post);
- POST /api/v1/goods-receipts (Create);
- PUT /api/v1/goods-receipts/{receiptId}/draft (Update);
- POST /api/v1/goods-receipts/{receiptId}/post (Post).

When WEB4 mode is enabled, all six mutations MUST carry the key. Missing/malformed key is a structured 400 before dispatch. No unkeyed fallback that can duplicate business effects. Coordinate schema12 host/Web deployment and reject an old client/host pairing rather than silently downgrade. Desktop/Local command invocations without Web operation metadata remain their existing behavior; do not accidentally require a Web header in Local code.
**Server-validated deployment precondition:** in WEB4 mode every one of the six writes **and** `GET /api/v1/operations/{operationId}` requires exactly one `X-MyDmsVn-Deployment-Epoch` header containing the canonical opaque epoch obtained after authoritative authentication. The server reads the current epoch from the same authoritative SQL database used for the request and compares BEFORE command/journal dispatch or exposing an outcome; missing, malformed or changed epoch fails closed with a documented non-cacheable generic error. This is a consistency fence, NOT an authentication/authorization grant: bearer/session/permissions remain authoritative. An old tab carrying an epoch from database A must never mutate or read journal data from database B even if the origin and numeric userId are equal. B01 freezes the exact error schema, response binding and canonical corpus; do not silently accept the header against a different SQL connection or a cached process-only epoch.

**Protected status request matching:** `GET /api/v1/operations/{operationId}` additionally requires a strict `X-MyDmsVn-Operation-Fingerprint` header (canonical 64-character lowercase SHA-256 hex, computed by the Web client from the original frozen command and B01 canonical serialization). After authenticating and locating ONLY the current actor's operation in the current database, compare the expected fingerprint to the journal's immutable fingerprint **before returning any original result**. If absent/mismatched, fail closed with a bounded generic response; no outcome is disclosed and no state may be SETTLED. The status envelope also echoes server-owned identity metadata for client-side comparison. Actor mismatch/nonexistence remains indistinguishable; the response is `Cache-Control: no-store`, contains no bearer/cookie/query secrets. Operator tooling uses separately authorized maintenance checks, never the user status GET as an unrestricted lookup.

Proposed readback: GET /api/v1/operations/{operationId}. Authenticated bearer + authoritative SQL session/account + operation-owner check, **matching deployment precondition and recomputed expected fingerprint** required. Respond with deterministic no-store **operation-specific authoritative outcome** only when CURRENT permissions still allow the corresponding operation. For Create/Update the recovered result must supply the original authorized full Write response (document identity, rowversion, ordered lines/server line identities and other fields present in that response) or return an explicit RESULT_UNAVAILABLE status that keeps editing locked; a bare ID/status is NOT sufficient. For Sales Post preserve its original version; Goods Receipt Post deliberately has no version. Status must never fabricate a version, use Get or silently leak separate Read access. Do not require a separate document Read grant to acknowledge an already-authorized Write/Post. **The status endpoint MAY return the complete original Create/Update Write-command result** (including its version, ordered lines and original field set) after fresh authentication, actor ownership and the corresponding current Write grant; it MUST NOT include fields outside that stored result, perform a new Get/Read, disclose unrelated documents, or bypass current permissions. For Post return only the original Post-command acknowledgement (Sales includes version; Receipt does not). For a revoked grant return generic 403; cross-user/nonexistent should be indistinguishable 404. Design the exact canonical JSON envelope and error codes in B01 and test corpus before source changes.

The readback contract should distinguish:
- COMMITTED: independently persisted affirmative outcome; frontend may settle unknown session without reissuing original write;
- NOT_OBSERVED/UNKNOWN: no affirmative evidence; **never** mean confirmed rollback or permission to re-Post;
- DENIED: current auth/permission is missing; no data leak, remain locked until permitted reconciliation;
- UNAVAILABLE/ERROR: no assumption about commit; remain unknown.
A processing state is OPTIONAL ONLY if B01 can prove its committed semantics. An uncommitted journal reservation is not observable via ordinary read committed queries and must not be reported as definitive processing.

Existing successful status and body for Create/Update/Post remain compatible with WEB2/WEB3; replay has to return the same command result semantics, including the original sales Post version, without re-running mutable business validation/stock writes. **Pure wire shape validation and current server authorization occur before journal read; business validation/command execution occur only for an unseen intent.** Stored outcome replay is bounded to what the same actor was originally allowed to receive and currently may access under the corresponding Write/Post grant. Store the smallest protected representation that can reproduce **exactly the full originally authorized command-specific result**, not a truncated generic acknowledgement; confirm how precommit DTO/result mappers, permitted display labels and original version/line IDs remain stable. Result retention/at-rest protection is reviewed in B01; never expand to additional Get-only fields. Do not reconstruct from a subsequent Get that might be 403 or read a different version. Do not put JWT/refresh/session secrets or raw unsafe errors into journal.

### Command-specific positive evidence and explicit resolution of absent operations

| Command | Original authorized successful result to capture and recover | Minimum permission for current readback |
|---|---|---|
| Sales Create / Update | Full original Sales detail: saleId, version, line identity/order, data required for the established command result | Sales.Receipts.Write (not a blanket Read grant) |
| Goods Receipt Create / Update | Full original Receipt detail: receiptId, version, replacement server line IDs/order, labels and nullable fields of the already-authorized Write response | Inventory.GoodsReceipts.Write |
| Sales Post | Original saleId/status/postedAtUtc **and version** | Sales.Receipts.Post |
| Goods Receipt Post | Original receiptId/status/postedAtUtc **without version** | Inventory.GoodsReceipts.Post |

Store a versioned canonical result representation produced before the successful business Commit, under the same SQL transaction. The authenticated actor may retrieve it only after **current** permission and session re-checks; avoid post-commit Get (Read can be denied). A bounded encrypted-at-rest or field-classified response retention policy must be source-reviewed in B01; if payload is compacted, preserve an immutable non-reusable key tombstone and return RESULT_EXPIRED/UNAVAILABLE without executing the write. Never retain bearer, refresh, SQL credentials, raw unauthorized request or arbitrary exception text.

**Absent/never-dispatched resolution policy:** a local intent can be persisted before any network send, and status 404 can also race with an old in-flight request. Automatically generating a new key or treating absence as rollback is forbidden. After user reviews the immutable original request and explicitly selects **Retry this exact operation**, client may submit the **same operation ID and byte-equivalent canonical request**, only if B03/B04 have demonstrated SQL-serialized same-key execution across workers/crashes and stable tombstone protection. Re-authenticate/re-authorize before server returns or executes, and verify current deployment epoch on the client. If an old attempt and explicit resend race, only one can commit; if schema, status service, timeout, expired response, key tombstone or permission prevents safely proving this, remain UNKNOWN with an operator-assisted, actor-authorized reconciliation flow. No self-timer, background queue, service worker, automatic refresh-time POST or "retry with fresh ID" is permitted.

The B01 corpus must contain explicit examples for absent journal after local persist/crash-before-dispatch, delayed first request vs user-confirmed same-key resend, committed replay, different fingerprint 409, expired/tombstoned key and readback without Read but with Write/Post. A status 404 is **non-affirmative**, not permission to reuse a different key.
**Additional mandatory negative corpus:** same origin/userId with changed database epoch, stale-tab POST/PUT and status GET; missing/duplicated/malformed deployment header; status GET missing/wrong expected fingerprint; status response identity mismatching local frozen operation ID, kind, route/resource or hash; corrupted IndexedDB fingerprint vs recomputed hash; same operation key reused for another command; cached/late response from a prior actor/epoch. In every case no journal result is disclosed to the wrong scope, no unauthorized write executes, and Web does not mark an intent SETTLED.


### Operator-assisted reconciliation: mandatory positive proof

For an UNKNOWN operation where replay payload has expired, journal is unavailable, current permission is revoked or client has lost the original immutable payload, B01/B06 must define an executable **privileged, audited, non-automated** workflow rather than an unconditional "unlock" button:

1. Match authenticated user, server deployment epoch, exact operation ID, route/kind and request fingerprint; do not ask support staff for JWT, refresh cookie, password, full raw draft or connection string.
2. Read journal result/tombstone and verify the authoritative SQL source/document, rowversion where available, complete Posted source set, line IDs, StockLedger, StockValuationEntries, StockBalances and frozen COGS for affected keys. The operator must use a separately authorized maintenance identity; the browser Web principal never gets elevated SQL grants.
3. Check current transaction/lock/in-flight state through the reviewed per-key serialization mechanism; absence of a committed row alone does not prove a competing worker will not later commit.
4. Outcome **CONFIRMED_COMMITTED** may only settle the local intent when there is positive journal/SQL evidence and sufficient current authority. If the original editable Create/Update detail/version is unavailable, acknowledge completion but **do not unlock Edit** until an authorized version/line snapshot is obtained. Outcome **CONFIRMED_NOT_EXECUTED** requires a source-proven exclusive key fence **and a durable terminal CANCELLED/DO_NOT_EXECUTE tombstone committed under that same key lock**, so a late original HTTP request cannot execute after operator closure. This administrative tombstone is NOT a successful business result. If the safe negative tombstone protocol is not designed, implemented and verified, **do not offer CONFIRMED_NOT_EXECUTED**; remain UNKNOWN and use positive-result/same-intent reconciliation only.
5. Record operator identity, correlation ID, decision, exact evidence source and timestamp; do not modify balances/ledger manually and do not clear/delete a tombstone to make replay pass. A user may start a new intent only after the safe disposition above, never simply because status returned 404.

The operator procedure and denial/expiry/indeterminate examples belong to B06 runbook; B05 acceptance must test at least COMMITTED, INDETERMINATE and expired-tombstone paths. This review amendment is a **design requirement**, not a claim that an admin endpoint or manual tool already exists.


## 4. Atomic SQL state machine and lock ordering

**Key invariant:** no committed success marker exists unless the business changes and result were committed in THE SAME SQL transaction. Likewise a transaction rollback cannot leave a committed success marker. A committed row has an immutable request fingerprint and deterministic replay output.

One recommended implementation shape (exact types to be source-verified by B01):

~~~text
HTTP: strict decode + authenticated actor + request authorization
  -> construct immutable WebOperationMetadata(operationId, kind, fingerprint)
  -> existing authorized Application command
       -> existing UoW.CreateAsync(); BeginTransaction()
       -> acquire transaction-owned operation lock BEFORE document/stock locks
       -> read matching committed journal (same actor + key)
          / matching fingerprint: replay stored result, no business writes
          / different fingerprint: deterministic KeyConflict, no writes
       -> if absent: execute EXISTING command business logic through same UoW
       -> create immutable successful journal outcome in same UoW
       -> Commit ONCE (journal + header/lines/ledger/valuation/balances)
  -> wire-map existing result or replay outcome
~~~

The above is a design target, not a claim the current handlers can be wrapped untouched. The current individual handlers own their transactions, so B01 must identify minimal common code extraction or explicit journal repository calls in six handlers, with no nested BeginTransaction or alternate business handler. Avoid a second SQL connection, independent journal transaction, EF migration, side-car queue or an HTTP-level "success" write after Commit.

Concurrency design: use (ActorUserId, OperationId) UNIQUE, under transaction-owned SQL key serialization (candidate sp_getapplock LockOwner=Transaction or justified UPDLOCK/HOLDLOCK key-range lock). Take this lock consistently before receipt/sales/stock locks; examine deadlock order with InventoryValuationCoordinator Shared/Exclusive gate and sorted stock-key locks. Concurrent duplicate waits or ends with a bounded unknown/conflict, NEVER two business executions. Do not transform deadlocks/timeouts into automatic retry. Handle cancellation before dispatch, cancellation after SQL dispatch, connection failure during Commit and response lost after Commit as distinct cases; ambiguous results stay UNKNOWN unless readback finds committed journal.

**Important edge cases:** a failed deterministic validation/403/409 before business Commit has no successful journal row; client must not reinterpret that as a successful operation or reuse a key with changed payload. Operations with same key but different expectedVersion must conflict. If application audit behavior writes in a separate pipeline, preserve existing audit semantics and make no claim of its atomicity unless separately verified. Never treat a 404 readback as proof a transaction is absent while another worker may still be committing.

## 5. Proposed schema/permissions (B01 must confirm exact DDL)

Tentative table dbo.WebOperationJournal:
- OperationId uniqueidentifier NOT NULL; ActorUserId int NOT NULL;
- OperationKind constrained short code, immutable Method/ResourceScope or equivalent; RequestFingerprint binary(32) NOT NULL;
- ResourceId bigint NULL, outcome/result status, a **versioned command-specific original authoritative result snapshot** sufficient for Create/Update/Sales Post/Receipt Post replay with their distinct rowversion contracts, and separately retained immutable key tombstone when payload expires;
- CreatedAtUtc datetime2(7) NOT NULL, CompletedAtUtc datetime2(7) NOT NULL for COMMITTED; optional expiry metadata only after safe lifecycle design;
- unique (ActorUserId, OperationId) (a composite PK or unique index), required checks for terminal state and invariants; no plaintext secrets or arbitrary client JSON;
- indexed actor+key status lookup; avoid unrestricted scanning of unrelated actor rows.

Add an operator-owned **Web deployment/database epoch** (opaque GUID, not a credential), persisted per logical Web database and exposed to authenticated clients through a read-only server endpoint or current /me context. Every WEB4 write/status request must present that epoch; the server must compare against the current authoritative SQL epoch before dispatch/readback, and return the same server-origin epoch in the protected status identity metadata. It must remain stable through host restarts and across workers. On clone to a different deployment, restore with incompatible/rewound data, or logical database replacement, a privileged operator rotates the epoch so old local drafts cannot cross the new database boundary. Test same-origin/same-userId with different epochs; the Web runtime principal receives SELECT only and cannot rotate it. **Do not assume copying a database automatically changes an internal GUID.**

An operation is *logically complete* only if its SQL transaction committed. Do not persist a completed response in a second transaction. Storing a full unredacted sensitive DTO indefinitely is unacceptable without a reviewed protection/retention policy; B01 must select a minimal replay-safe schema, source-specific data classification and safe result encoding. If response is discarded after a retention interval, a compact durable tombstone or equivalent must reject reusing the old key; do not implement a purge that turns prior keys into fresh writes. B04/06 must document operational retention/cleanup and leave production approval deferred.

Web schema 11 is exact; proposed new 012_WebOperationJournal.sql should be opt-in via existing DbMigrator web mode, with new WEB-only exact12 admission/fingerprint/grants and safe fail-closed checks. **Mandatory source change:** the current DatabaseMigrationRunner.ValidateWebSchemaStateBeforeMigration only handles TooOld by detecting exact schema10; after switching Web checker to 12, exact11 would be rejected. Introduce a frozen standalone exact11 artifact/fingerprint validator and separately admitted 11→12 path; preserve the existing exact10→Web maintenance path through 011/012 where admissible, prohibit schema9, partial/spoofed11, too-new and unmanaged artifacts. Desktop CurrentScripts stays exact10. Preserve existing scripts and historical fingerprints byte-for-byte. Ordinary Web principal gets only minimum SELECT/INSERT/UPDATE on journal or more restricted columns as proven; deny DELETE/ALTER/CONTROL; migration principal owns DDL. Audit/review role separation and operator reprovision for existing Web databases. Confirm SQL version support against SQL Server 2017 used by the pilot. No mixed Desktop/schema12 writer rollout.

## 6. Failure/authorization decision table

| Situation | Required server result | Client rule |
|---|---|---|
| New key + valid authorized request | Business and journal commit once | Accept authoritative result |
| Same actor/key/fingerprint after commit | Original command-specific authorized result (Sales Post includes version; Receipt Post does not), zero new writes | Accept without re-running business operation |
| Same key different route/body/rowversion | deterministic 409 Idempotency.KeyConflict | Keep draft; new user intent needs new ID |
| Concurrent same-key requests | serialized single commit, or bounded UNKNOWN without second effect | Never send a fresh automatic key |
| Different actor + same key | independently scoped; status does not leak other actor | Actor fencing; never transfer private draft |
| Permission/session revoked before replay/status | 401/403 with no result leak | Require reauthentication/authorized review |
| Server commits, HTTP response is lost | readback proves COMMITTED | Mark settled; no second Post |
| No committed status row (never dispatched, in flight or rolled back) | non-affirmative outcome only | Lock unknown; allow **explicit user-confirmed SAME-ID/same-payload resend** only after B03/B04 serialization/tombstone/permission gates, otherwise operator reconciliation |
| SQL rollback/fault before Commit | no completed journal/ledger partial | Reconcile; never assume safe retry |
| Journal/schema/principal invalid at startup | fail closed | No downgraded unjournaled writes |
| Stored result expired but key tombstone remains | RESULT_EXPIRED / no re-execution | Never silently create fresh operation; operator-assisted reconciliation |
| Post-commit audit sink fails after journal+business Commit | Journal remains COMMITTED; audit failure is separately reported | Status recovery confirms the original committed business result; no duplicate Post or audit write |

## 7. Test and rollout contract

B01 publishes positive/negative canonical corpus consumed by Web F01; protect fixture hashing and provenance. B03/B04 backend owners: HttpHost SQL-free, Migrations, Security (principal + HTTP SQL), Inventory (valuation/concurrency), existing Sales/Application. Browser+physical oracle in existing owned IIS Express pilot for final WEB4 candidate. Use named SQL tests and TRX actual rows, no new global runner, fresh owned database for each browser, separate CI/SQL/manual evidence.

Do not assume schema11 host and schema12 host are interchangeable. Backend-first staged implementation may accept keyed requests only in a specifically opted-in pilot environment; final WEB4 mode rejects missing keys. Deploy matching production React build and host and operator migrations atomically by runbook, but production release acceptance is OUT OF SCOPE. Preserve full IIS/IME/audit/limiter/XSS limits.

