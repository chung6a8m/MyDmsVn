# WEB4 B04 implementation checklist

Selected by Owner on 2026-11-02 as the prerequisite for frontend milestone WEB4-04. The [B04 owning plan](../plans/20261101-003-web4-reliability-server.md#5-b04--six-idempotent-web-mutation-apis-and-readback-planned), [design](../design/WEB4_IDEMPOTENCY_JOURNAL_CONTRACT.md) and immutable [B01 corpus](../contracts/web4-v1/README.md) remain authoritative.

Base: backend `8f8091575bbdab4d85c97f14d44b82030d70927d`, B03 source `e2976715bb106cc22c702e60ca2dd4536d6894ae`; frontend `68398accdd03a1ed78a2e3bec7c5cb2499457b6d`. B01 corpus pin `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`, manifest `0acab190e7fe126d53c13c0510a1958365e5a6e1fda78a4321da78297e0b3f1a`.

## Work

- [x] Strict operation header/canonical fingerprint implementation; verify exact independent canonical vectors, invalid headers/routes, six original result codec roundtrips and bounded corrupt result rejection. The production codec never imports the test oracle or reads business data after Commit.
- [x] Protected context/status repository using caller-owned UoW transaction, current epoch/key/session/account/operation permission fences and actor-scoped lookup. Real SQL tests prove denied/foreign/missing/expired/unavailable results, held authority and no business mutations.
- [x] Explicit host modes `legacy-web11` and `web4`: production configuration must select mode, enforce its exact schema/principal admission, and never downgrade a WEB4 request to unkeyed handling. Capture validated session identity in request scope. Six controllers attach immutable metadata after strict body decode. Preserve legacy payload/status/error compatibility.
- [x] Add authenticated context/status routes; protected status metadata comes from SQL, outcome from strict original result codec. Map only frozen protocol errors; no cache, no retry, no Get-based reconstruction.
- [x] Add real HTTP + restricted SQL tests for six-command replay, conflict, lost response/readback, current permission/session/epoch checks, original Write-without-Read outcomes, tombstones and single source/ledger/value effects. Use existing ownership-checked disposable fixtures; execute SQL owners sequentially.
- [x] Focused regression, independent review, encoding/source-preservation checks, task evidence, immutable candidate pin for frontend. No B05 browser acceptance, full IIS or production approval.

## Review focus

Authentication and permission must be rechecked after a competing key releases. A different actor's key is indistinguishable from missing. Context/status use the same authoritative database as writes. Receipt Post has no version; Create request target stays null despite assigned result identity. Failed result decoding never permits reexecution. Immutable tombstones survive expiry. Both legacy and WEB4 modes require explicit matching deployment artifacts.

## Progress

Verified candidate at runtime/test commit bd871518fd162927d938babd5535a2ddf91c2ad5: 604 selected final local rows passed, zero failed/skipped; independent review has no open P1/P2. Actual commands/counts and remaining gates belong to the new B04 evidence record; historical evidence stays unchanged. Owner accepted and PR #83 merged at `59ca21d16f7839c6d7c553395a367e465591652b` on 2026-11-02 after Windows CI PASS at `b225bee`; B04 is DONE (bounded). B05 browser/process-crash gates remain outstanding.

- [x] Final independent review: no actionable P0/P1/P2; exact-head CI confirmed and expected-head merge completed. [Acceptance record](../evidence/web4/20261102-b04-http-operations.md#owner-acceptance-and-merge--2026-11-02) preserves scope and limits.
