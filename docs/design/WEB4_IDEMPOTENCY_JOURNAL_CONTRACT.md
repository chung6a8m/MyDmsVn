# WEB4 operation journal and HTTP idempotency contract — proposed v1

**Owner:** MyDmsVn backend. **Status:** PROPOSED TECHNICAL BASELINE for WEB4-B01 review, NOT an implemented API or approved schema. **Last revised:** 2026-11-01. See [owning backend plan](../plans/20261101-003-web4-reliability-server.md), [ADR 0014](../adr/0014-web4-durable-operations.md), [backend test matrix](../development/WEB4_BACKEND_ACCEPTANCE.md) and frontend [durable recovery design](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/design/WEB4_DURABLE_DRAFT_AND_RECOVERY.md).

## 1. Source facts and deliberate change

At WEB3 main, the six Create/Update/Post handlers reside in Server.Application.Sales.SalesReceiptFeatures and Server.Application.Inventory.GoodsReceiptFeatures. They open SqlUnitOfWorkFactory, BeginTransaction, execute repositories, Commit, and return. Repositories borrow ISqlExecutionContext.Connection/Transaction; HTTP controllers only MediatR.Send and wire-map. Thus an HTTP middleware that writes a journal BEFORE or AFTER MediatR.Send is **not atomic** with the business mutation. WEB4 must add a narrow transaction-aware coordination hook in the existing Application handler/UoW path; controller never writes business SQL. Application must not depend on System.Web or cookie/JWT types. Read existing authorization/validation/audit pipeline before choosing exact interception order.

Existing client guarantees remain: no automatic write retry, no operation journal, no durable drafts; loss of acknowledgement locks current session. WEB4 extends positive-evidence reconciliation, it does not assume an aborted browser request canceled an SQL transaction.

## 2. Identity, key and canonical request

- Proposed Idempotency-Key request header: one canonical lowercase RFC4122 GUIDv4 in D form (36 ASCII characters), randomly generated with cryptographically strong browser API, not a business/document ID. Exactly one instance; reject whitespace, duplicates, nil, non-v4, case/canonicalization differences with generic 400.
- Protected scope: current **server-validated numeric user identity** plus operation key; NOT browser-provided actor, JWT grant list, session generation, IP address or device. A new authenticated session for the same user may inspect the same operation if still currently authorized. Cross-user lookup must behave identically to missing operation and must never disclose existence.
- The key binds to EXACTLY one immutable command. Compute server-owned SHA-256 fingerprint over a documented canonical representation of [HTTP method, canonical route template, route resource ID where present, command kind, parsed authoritative business payload including expectedVersion, array order, string precision]. Do NOT hash raw untrusted HTTP bytes alone, truncate int64 into JS Number, omit sales gift, or use client line identity. Contract fixtures must cover property order/whitespace, null vs missing, numeric-string lexical grammar, duplicates and 1/200 lines.
- Every new *user intent* or changed payload/version needs a new operation ID. Retrying the same ID by an explicit user action is not allowed to reinterpret an updated draft. Reuse on another module/endpoint is a conflict, not a fresh operation.

## 3. Routes and compatibility

The six existing routes are unchanged:
- POST /api/v1/sales-receipts (Create);
- PUT /api/v1/sales-receipts/{saleId}/draft (Update);
- POST /api/v1/sales-receipts/{saleId}/post (Post);
- POST /api/v1/goods-receipts (Create);
- PUT /api/v1/goods-receipts/{receiptId}/draft (Update);
- POST /api/v1/goods-receipts/{receiptId}/post (Post).

When WEB4 mode is enabled, all six mutations MUST carry the key. Missing/malformed key is a structured 400 before dispatch. No unkeyed fallback that can duplicate business effects. Coordinate schema12 host/Web deployment and reject an old client/host pairing rather than silently downgrade. Desktop/Local command invocations without Web operation metadata remain their existing behavior; do not accidentally require a Web header in Local code.

Proposed readback: GET /api/v1/operations/{operationId}. Authenticated bearer + authoritative SQL session/account + operation-owner check required. Respond with a deterministic non-cached minimal status/acknowledgement (operation kind, committed outcome, document ID, posted time when applicable; no invented version), only if CURRENT permissions still allow the corresponding operation. Do not require a separate document Read grant to acknowledge a successfully authorized Write/Post, but never expose full detail through status or bypass explicit current permission checks. For a revoked grant return generic 403; cross-user/nonexistent should be indistinguishable 404. Design the exact canonical JSON envelope and error codes in B01 and test corpus before source changes.

The readback contract should distinguish:
- COMMITTED: independently persisted affirmative outcome; frontend may settle unknown session without reissuing original write;
- NOT_OBSERVED/UNKNOWN: no affirmative evidence; **never** mean confirmed rollback or permission to re-Post;
- DENIED: current auth/permission is missing; no data leak, remain locked until permitted reconciliation;
- UNAVAILABLE/ERROR: no assumption about commit; remain unknown.
A processing state is OPTIONAL ONLY if B01 can prove its committed semantics. An uncommitted journal reservation is not observable via ordinary read committed queries and must not be reported as definitive processing.

Existing successful status and body for Create/Update/Post remain compatible with WEB2/WEB3; replay has to return the same command result semantics. Store only the minimal data necessary to reconstruct the correctly authorized canonical acknowledgement; confirm exactly how precommit DTO/result mappers are captured, and how display labels and version remain stable. Do not reconstruct from a subsequent Get that might be 403 or read a different version. Do not put JWT/refresh/session secrets or raw unsafe errors into journal.

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
- ResourceId bigint NULL, outcome/result status, replay-safe response snapshot/reference;
- CreatedAtUtc datetime2(7) NOT NULL, CompletedAtUtc datetime2(7) NOT NULL for COMMITTED; optional expiry metadata only after safe lifecycle design;
- unique (ActorUserId, OperationId) (a composite PK or unique index), required checks for terminal state and invariants; no plaintext secrets or arbitrary client JSON;
- indexed actor+key status lookup; avoid unrestricted scanning of unrelated actor rows.

An operation is *logically complete* only if its SQL transaction committed. Do not persist a completed response in a second transaction. Storing a full unredacted sensitive DTO indefinitely is unacceptable without a reviewed protection/retention policy; B01 must select a minimal replay-safe schema, source-specific data classification and safe result encoding. If response is discarded after a retention interval, a compact durable tombstone or equivalent must reject reusing the old key; do not implement a purge that turns prior keys into fresh writes. B04/06 must document operational retention/cleanup and leave production approval deferred.

Web schema 11 is exact; proposed new 012_WebOperationJournal.sql should be opt-in via existing DbMigrator web mode, with new WEB-only exact12 admission/fingerprint/grants and safe fail-closed checks. Desktop CurrentScripts stays exact10. Preserve existing scripts and historical fingerprints byte-for-byte. Ordinary Web principal gets only minimum SELECT/INSERT/UPDATE on journal or more restricted columns as proven; deny DELETE/ALTER/CONTROL; migration principal owns DDL. Audit/review role separation and operator reprovision for existing Web databases. Confirm SQL version support against SQL Server 2017 used by the pilot. No mixed Desktop/schema12 writer rollout.

## 6. Failure/authorization decision table

| Situation | Required server result | Client rule |
|---|---|---|
| New key + valid authorized request | Business and journal commit once | Accept authoritative result |
| Same actor/key/fingerprint after commit | Original authorized result; zero new writes | Accept, no editor mutation replay |
| Same key different route/body/rowversion | deterministic 409 Idempotency.KeyConflict | Keep draft; new user intent needs new ID |
| Concurrent same-key requests | serialized single commit, or bounded UNKNOWN without second effect | Never send a fresh automatic key |
| Different actor + same key | independently scoped; status does not leak other actor | Actor fencing; never transfer private draft |
| Permission/session revoked before replay/status | 401/403 with no result leak | Require reauthentication/authorized review |
| Server commits, HTTP response is lost | readback proves COMMITTED | Mark settled; no second Post |
| No committed status row (possibly in flight/rolled back) | non-affirmative outcome only | Lock unknown, manual reconciliation |
| SQL rollback/fault before Commit | no completed journal/ledger partial | Reconcile; never assume safe retry |
| Journal/schema/principal invalid at startup | fail closed | No downgraded unjournaled writes |

## 7. Test and rollout contract

B01 publishes positive/negative canonical corpus consumed by Web F01; protect fixture hashing and provenance. B03/B04 backend owners: HttpHost SQL-free, Migrations, Security (principal + HTTP SQL), Inventory (valuation/concurrency), existing Sales/Application. Browser+physical oracle in existing owned IIS Express pilot for final WEB4 candidate. Use named SQL tests and TRX actual rows, no new global runner, fresh owned database for each browser, separate CI/SQL/manual evidence.

Do not assume schema11 host and schema12 host are interchangeable. Backend-first staged implementation may accept keyed requests only in a specifically opted-in pilot environment; final WEB4 mode rejects missing keys. Deploy matching production React build and host and operator migrations atomically by runbook, but production release acceptance is OUT OF SCOPE. Preserve full IIS/IME/audit/limiter/XSS limits.

