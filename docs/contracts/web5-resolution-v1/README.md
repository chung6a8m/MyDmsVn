# WEB5 resolution v1 — additive read contract

**Implementation candidate, 2026-11-02.** Source: `OperationResolutionController`, `WebOperationResolutionWire` and `SqlWebOperationResolutionRepository`. SQL acceptance is recorded separately in [B02 evidence](../../evidence/web5/20261102-b02-terminal-resolution.md); authored fixtures are not captured SQL/HTTP evidence. `manifest.json` pins every other file's exact UTF-8/LF bytes; consumers additionally pin the backend commit and manifest hash. Preserve the separate frozen 48-blob WEB4 corpus.

Only `web5`/exact13 exposes this extension. `web4`/exact12 and `legacy-web11`/exact11 return404 for the extension. The web5 host retains the exact original v1 context, six mutations, result codec and protected status. Database epoch is not a bundle version. Same-key browser resend remains disabled.

## Requests

- `GET /api/v2/operations/context`, empty body, current Bearer/session. Success200 is exactly `{data:{contractVersion:"web5-resolution-v1",deploymentEpoch,actorUserId,sameKeyResendEnabled:false}}`.
- `GET /api/v2/operations/{operationId}/resolution`, empty body, current Bearer/session; exactly one each `X-MyDmsVn-Deployment-Epoch` and `X-MyDmsVn-Operation-Fingerprint` header. No query, escaped/case/trailing-slash aliases or alternate method. ID is lowercase canonical UUIDv4; epoch is a nonzero lowercase canonical UUID; fingerprint is64 lowercase hexadecimal characters, freshly recomputed from the original unchanged WEB4 canonical intent.

The server derives authority from the immutable stored operation kind: Create/Update requires current original Write; Post requires current original Post. Ordinary document Read is not required. Original actor/session/account, epoch and fingerprint are revalidated under the original transaction-owned key fence on every read. Cross-actor data is never read. Reads never create an audit decision or perform a business write.

## Exact response

Success200 outer object contains only `data`. It has exactly12 members: `contractVersion`, `actorUserId`, `deploymentEpoch`, `operationId`, `operationKind`, `method`, `routeScope`, `requestTargetId`, `requestFingerprint`, `state`, `outcome`, `terminal`. `actorUserId` is a positive JSON int32, matching the authenticated actor. Original method/route/kind/target/fingerprint and outcome scalars retain the WEB4 contract, including decimal/int64 strings and Sales Post version versus Receipt Post without version. Create target stays null.

| state | outcome | terminal | Consumer disposition |
|---|---|---|---|
| COMMITTED | Original strict v1 `{data:...}` envelope, at most1048576 UTF-8 bytes | null | Positive settlement only after exact local ownership and completed IDB CAS |
| RESULT_EXPIRED | null | null | Protected; no usable original editable version |
| RESULT_UNAVAILABLE | null | null | Protected; no inferred negative/business-case closure |
| INDETERMINATE | null | null | Protected; includes legacy CANCELLED without matched audit |
| NOT_EXECUTED | null | Exact terminal object below | Record terminal certificate with exact IDB CAS, preserve raw draft, require fresh authorized baseline and explicit new intent |

`terminal` is exactly `{decisionId,caseId,decidedAtUtc}`. `decisionId` is lowercase UUIDv4. `caseId` matches `[A-Za-z0-9_-]{1,128}` and is an operator correlation lookup hint, not raw reason/evidence, a signed external ticket or proof of business-case closure. `decidedAtUtc` uses `yyyy-MM-ddTHH:mm:ss.fffffffZ` with SQL datetime2(7) precision; preserve it as text. It matches the immutable audit and journal completion timestamp. The read verifies the committed CANCELLED/audit pair across epoch/actor/key/kind/target/fingerprint/time before emitting it; local data or an absent row cannot mint a certificate.

Errors use the unchanged bounded WEB4 error envelope and current request correlation: malformed identity/path/body400 ValidationError; invalid session401; revoked original grant403; absent404 Operation.NotObserved; epoch/fingerprint mismatch409; unavailable fence/storage503. None releases UNKNOWN or authorizes retry. No negative HTTP error is a terminal certificate.

## Operator authority

Append-only013 creates the immutable audit and a journal INSERT guard requiring its exact matched audit before CANCELLED. Only separately provisioned `MyDmsVnWebOperationOperatorRole` may execute `dbo.RecordWebOperationNotExecuted`; runtime has audit SELECT only and explicit DML/execute denials. The procedure uses the existing epoch then actor/key fence, refuses any prior journal/audit disposition, and writes both records in the caller's single transaction. Operator code must commit once and independently reread; no procedure return or lost acknowledgment proves Commit. No general admin HTTP endpoint or browser operator privilege exists.

Strict-mode compatibility tests are NOT_APPLICABLE under ADR0015. Additive compatibility, exact13 migration and all six terminal race orders remain mandatory B02/B04-B proof; full IIS remains B05/F04.
