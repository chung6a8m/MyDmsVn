# WEB6 Resend Eligibility Design

**Status:** proposed for Owner approval. This design completes the backend B04/B6-RPL-05 eligibility boundary only. It does not enable the frontend resend action, staging, production, or automatic mutation retry.

## Intent

Allow a future frontend F02/F03 flow to determine whether one explicitly consented transmission of the exact original immutable command is eligible. The backend must provide an authoritative operation age without treating HTTP 404, a browser clock, or absence from `WebOperationJournal` as rollback proof.

The eligibility decision is a short-lived read-only hint. The existing mutation route and `SqlWebOperationJournal` remain the final authorization and one-business-effect fence. There is no privileged resend mutation endpoint.

## Goals

- Issue a server-signed receipt before the original business request is dispatched.
- Bind the receipt to actor, deployment epoch, operation ID, fingerprint, operation kind, method, route scope and target.
- Return an additive `web6-resend-eligibility-v1` decision with `ALLOW`, `DENY` or `INDETERMINATE`, stable reason, `serverUtc` and `eligibleUntilUtc`.
- Recheck the current authenticated session, original Write/Post grant, deployment epoch, immutable identity and current journal/resolution state on every eligibility read.
- Keep the maximum eligibility window at seven days, based only on server time.
- Preserve the frozen WEB4 and WEB5 wire corpora unchanged.
- Default the capability OFF and expose it only for an explicitly pinned nonproduction candidate.

## Non-goals and anti-goals

- No automatic HTTP mutation retry, background resend, service-worker replay or retry after 401/timeout/5xx/reload.
- No new operation ID, altered body, reconstructed request or special mutation endpoint.
- No claim that 404/absence means `NOT_EXECUTED`.
- No backend global consent or reusable permission. The frontend F02 durable IndexedDB claim remains separately owned and limited to one claim per original key.
- No production enablement, B05/F04 paired acceptance, frontend UI work, SQL quarantine cleanup or changes to archived WEB4/WEB5 evidence.

## Approaches considered

### Selected: stateless signed pre-dispatch receipt

The authenticated client obtains a receipt through an additive GET before the first business dispatch. The receipt is a compact signed token and creates no SQL row. A later eligibility GET validates the token, current security context and current journal state.

This avoids a schema migration and avoids creating a second mutable lifecycle beside the transaction-owned operation journal. Key rotation works through the existing bounded key ring, provided signing keys are retained for at least the receipt lifetime.

### Rejected: persisted admission row

A SQL pre-admission row gives a stronger operational audit but adds schema, retention, compaction, permissions and crash/cleanup states. It also risks coupling an advisory receipt to the journal's transaction-owned business lifecycle. This is disproportionate for the selected B6-RPL-05 boundary.

### Rejected: browser timestamp checked against server time

A browser-created timestamp can be fabricated or reset and does not prove that the server saw the immutable original identity before dispatch. It cannot be the authoritative age source.

## HTTP contract

Add a new controller family under `/api/v3/operations`. Existing `/api/v1` and `/api/v2` payloads, routes and frozen fixtures are not modified.

### Candidate context

`GET /api/v3/operations/resend-context`

The route exists only when the host is in exact `web5` mode and the explicit WEB6 eligibility option is enabled. It requires an active authenticated session and returns:

```json
{
  "contractVersion": "web6-resend-eligibility-v1",
  "candidateId": "<configured bounded identifier>",
  "deploymentEpoch": "<guid>",
  "actorUserId": 123,
  "serverUtc": "2026-11-09T00:00:00.0000000Z",
  "maximumEligibilitySeconds": 604800,
  "eligibilityEnabled": true
}
```

Unsupported mode, disabled capability or missing candidate pin returns the existing indistinguishable 404 shape.

### Pre-dispatch receipt

`GET /api/v3/operations/{operationId}/resend-receipt`

Required strict headers:

- `X-MyDmsVn-Deployment-Epoch`
- `X-MyDmsVn-Operation-Fingerprint`
- `X-MyDmsVn-Operation-Kind`
- `X-MyDmsVn-Operation-Target-Id` only for Update/Post; it must be absent for Create.

Kind determines the existing method and route scope through `WebOperationWireInput`; callers cannot supply arbitrary method or route strings. The endpoint verifies canonical GUIDv4, SHA-256, target shape, current actor/session, exact current epoch and the original Write/Post grant inside a caller-owned read transaction.

The response contains the contract version, candidate ID, exact immutable public identity, `issuedAtUtc`, `eligibleUntilUtc` and a signed `receipt`. It contains no request body, note, customer, product, result bytes, JWT or connection data.

The exact success envelope and property order frozen by the additive corpus is:

```json
{
  "contractVersion": "web6-resend-eligibility-v1",
  "candidateId": "candidate-identifier",
  "deploymentEpoch": "00000000-0000-0000-0000-000000000000",
  "actorUserId": 123,
  "operationId": "00000000-0000-4000-8000-000000000000",
  "operationKind": "Sales.Create",
  "method": "POST",
  "routeScope": "/api/v1/sales-receipts",
  "requestTargetId": null,
  "requestFingerprint": "<64 lowercase hex characters>",
  "issuedAtUtc": "2026-11-09T00:00:00.0000000Z",
  "eligibleUntilUtc": "2026-11-16T00:00:00.0000000Z",
  "receipt": "<compact JWS>"
}
```

The receipt is a compact JWS using HS256 with:

- header `alg=HS256`, root `kid`, and `typ=MYDMSVN-WEB6-RESEND-RECEIPT` with no additional header members;
- `iss` equal to the configured host issuer and `aud` equal to the configured audience plus the exact suffix `:web6-resend-eligibility-v1`;
- a 32-byte signing key calculated as `HMACSHA256(rootKey, UTF8("MyDmsVn.Web6.ResendEligibility.SigningKey.v1"))`, preventing cross-protocol use with access tokens;
- `kid` for bounded key rotation;
- exact payload claims `iss`, `aud`, `sub`, `candidate`, `epoch`, `operation`, `fingerprint`, `kind`, `method`, `route`, `target`, `iat`, `nbf` and `exp`; `sub` and a non-null target are canonical decimal strings, a Create target is JSON null, GUIDs are lowercase canonical `D`, and the fingerprint is 64 lowercase hex characters;
- an exact seven-day maximum lifetime and strict canonical decoding.

`iat` and `nbf` are the same server NumericDate second; `exp=iat+604800`. The decoded `issuedAtUtc` and `eligibleUntilUtc` use those whole-second values rendered as seven-digit UTC timestamps. The receipt is limited to 4096 ASCII characters. The verifier selects only a configured root key by `kid`, derives the receipt key again, verifies the signature, rejects duplicate/extra header or payload claims, and then applies the semantic bindings and lifetime rules. Access-token validation must reject this receipt, and receipt validation must reject an access token.

Obtaining a receipt is read-only and is not consent to resend. If no receipt was durably stored by the frontend before original dispatch, an absent operation remains ineligible.

### Eligibility read

`GET /api/v3/operations/{operationId}/resend-eligibility`

The request supplies the same immutable headers plus exactly one bounded receipt header. Validation occurs in this order:

1. Strict route/header/receipt shape and signature.
2. Candidate pin, receipt type/audience, server-issued time and seven-day expiry.
3. Receipt binding to the current actor, epoch and complete immutable operation identity.
4. Current active session, exact deployment epoch and original Write/Post grant.
5. Current protected resolution under the existing actor/key fence.

The success envelope always uses the additive contract and returns one of:

| Decision | Stable reasons | Meaning |
|---|---|---|
| `ALLOW` | `ORIGINAL_NOT_OBSERVED_RECEIPT_VALID` | No owned journal/resolution row is observed, the receipt is valid and current security admission passed. The original command may execute for the first time. |
| `DENY` | `COMMITTED_RESULT_AVAILABLE`, `COMMITTED_RESULT_UNAVAILABLE`, `RESULT_EXPIRED`, `NOT_EXECUTED`, `RECEIPT_EXPIRED` | The original key must not be manually resent. Existing GET settlement, support or reviewed new-intent paths apply. |
| `INDETERMINATE` | `TERMINAL_AUDIT_INCOMPLETE` | The journal has a terminal-looking state without the complete matched audit proof. Keep the operation protected. |

The exact success envelope and property order frozen by the additive corpus is:

```json
{
  "contractVersion": "web6-resend-eligibility-v1",
  "candidateId": "candidate-identifier",
  "deploymentEpoch": "00000000-0000-0000-0000-000000000000",
  "actorUserId": 123,
  "operationId": "00000000-0000-4000-8000-000000000000",
  "operationKind": "Sales.Create",
  "method": "POST",
  "routeScope": "/api/v1/sales-receipts",
  "requestTargetId": null,
  "requestFingerprint": "<64 lowercase hex characters>",
  "decision": "ALLOW",
  "reason": "ORIGINAL_NOT_OBSERVED_RECEIPT_VALID",
  "serverUtc": "2026-11-09T00:01:00.0000000Z",
  "eligibleUntilUtc": "2026-11-16T00:00:00.0000000Z"
}
```

Every successful decision includes the non-null signed `eligibleUntilUtc`, including `DENY` and `INDETERMINATE`. It never includes outcome/result bytes or terminal case metadata.

The fail-closed wire behavior is exact:

- unsupported mode, disabled capability or incomplete candidate configuration: existing indistinguishable HTTP 404 error envelope; no success decision;
- missing, duplicate, overlong, malformed, wrongly signed or non-canonical receipt/identity input: existing HTTP 400 `ValidationError` envelope;
- unauthenticated or expired/revoked session: existing HTTP 401 mapping;
- missing current original Write/Post grant: existing HTTP 403 mapping;
- epoch, fingerprint, kind, target, actor or candidate mismatch: the existing applicable conflict/authorization mapping, never a success decision and never another actor's state;
- SQL exception, lock timeout or unavailable state: existing `Operation.Unavailable` error mapping, never `INDETERMINATE` or `ALLOW`;
- a correctly signed and bound but expired receipt: HTTP 200 `DENY/RECEIPT_EXPIRED` without consulting absence as rollback proof.

A 404 never maps to `ALLOW` without a valid bound receipt.

`eligibleUntilUtc` is the signed receipt expiry. `serverUtc` is taken from `IUtcClock` at response creation. The eligibility response is `no-store`/`no-cache` and is not a reservation: the business mutation path must revalidate actor/session/grant/epoch and rely on the SQL journal fence.

## Configuration and fail-closed behavior

Extend `WebHostOptions` with optional `bool web6ResendEligibilityEnabled = false` and `string? web6CandidateId = null`. Environment variable `MYDMSVN_WEB6_RESEND_ELIGIBILITY` is optional and accepts only absent/`disabled` or `receipt-v1`. When it is `receipt-v1`, `MYDMSVN_WEB6_CANDIDATE_ID` is required and must match `\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z`; the option is rejected unless operation mode is exact `web5`. When disabled, a nonempty candidate variable is rejected to prevent misleading inactive configuration. Existing constructors remain source compatible through the optional parameters.

The initial maximum window is fixed at 604800 seconds; it is not an operator-tunable value in this change. A signing key referenced by an unexpired receipt must remain in the configured validation key ring. Removing it makes the receipt invalid and therefore fail closed.

The v2 `sameKeyResendEnabled` field remains `false`. Frontend enablement is a later F02/F03 and explicit nonproduction Owner decision.

The six existing business mutation routes do not require or accept the receipt and their frozen WEB4 request contracts remain unchanged. The receipt is a frontend durability prerequisite only: an original request sent without first durably storing a receipt may still execute normally, but if its acknowledgement is lost and no journal result is observed it remains permanently ineligible for manual resend.

## Internal ownership

- `Web6ResendEligibilityController` owns strict additive routes and response mapping.
- A focused receipt codec owns canonical claims, domain-separated signing, bounded decoding and constant-time signature validation through the pinned JWT library.
- An application-facing read method owns current epoch/session/original-grant admission for a caller-supplied allowlisted `WebOperationKind`.
- The existing resolution repository remains authoritative for COMMITTED, EXPIRED, unavailable, terminal negative and absence states; no parallel SQL state is introduced.
- `WebOperationWireInput` remains the sole mapping from kind to method/route and the sole canonical operation identity parser.

## Security and failure behavior

- Receipt possession is not authorization; every read and later mutation rechecks current authority.
- Receipt contents are identity metadata only and are safe for local durable storage, but must not be logged in full or included in support exports.
- All malformed, overlong, duplicate-header, wrong-key, wrong-candidate, wrong-actor, wrong-epoch and expired cases fail closed.
- Clock skew is bounded by the existing host skew for signature validation, but `ALLOW` is never returned after signed expiry.
- Receipt issuance and eligibility reads perform no business mutation and never invoke mutation retry logic.
- An exception or SQL availability failure returns the existing unavailable mapping and never `ALLOW`.

## Verification

Implementation follows test-driven development.

Unit and HTTP-host tests cover canonical receipt issue/verify, domain separation from access JWTs, previous-key validation, key removal, exact seven-day boundary, server clock, all six operation kinds, target rules, malformed/duplicate/overlong headers, old modes, default-off configuration, candidate mismatch, wrong actor/session/grant/epoch/fingerprint, and no-cache responses.

Repository/SQL-focused tests cover current Write/Post authorization even when ordinary Read is denied, revoked grants, absent owned row, committed retained result, expired/unavailable result, terminal `NOT_EXECUTED`, other-actor indistinguishability, lock timeout and SQL failure. They assert zero journal/business mutation from both new GET endpoints.

Contract fixtures for `web6-resend-eligibility-v1` are additive. Existing WEB4 48-blob and WEB5 resolution corpora must remain byte-identical.

The delivery evidence records source SHA, exact test selection/counts, feature OFF state and remaining frontend F02/F03/B05 gates. Passing this backend change closes B6-RPL-05 implementation only; it does not enable the product action.

## Decisions left to the builder

- Exact class/file split consistent with the current HttpHost test conventions.
- Stable internal enum/type names, provided the public wire strings above remain exact.
- The receipt request header is fixed as `X-MyDmsVn-Resend-Receipt`.


