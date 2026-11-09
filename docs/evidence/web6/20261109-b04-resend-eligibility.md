# WEB6 B04 follow-up — read-only resend eligibility

**Date:** 2026-11-09 (Asia/Bangkok). **State:** B6-RPL-05 IMPLEMENTED / LOCALLY VERIFIED NONPRODUCTION. Same-key Resend remains OFF; frontend F02/F03 and paired B05 are NOT RUN; staging is NOT ACCEPTED and production is NO-GO.

## Candidate and scope

- Branch: `codex/web6-b04-resend-eligibility`; base `743a56b`; approved design/plan commit `4f48edd`.
- Final runtime/test head `b72e52b0af6d61f3bf4767daba048f428ef0ca45`; documentation follows without changing runtime behavior.
- Runtime commits: configuration `5baec38`, receipt codec `9cece0b`, SQL authorization/read `eb6a0d2`, context/receipt routes `266824b`, eligibility decision route `5bcf103`, independent-review expiry/canonical-binding fixes `1f0f135`, PR-review resolution/expiry correction `b72e52b`.
- Windows NT `10.0.26300.0`; .NET SDK `8.0.427`; net48 Release target; installed .NET Framework release `534296`; VSTest `17.11.1` x64.
- No schema migration, new SQL state, business mutation route, automatic retry, frontend change, IIS/IIS Express run, browser run, staging action or production action is included.

The feature is fail-closed and defaults OFF. It is exposed only in exact `web5` mode with `MYDMSVN_WEB6_RESEND_ELIGIBILITY=receipt-v1` and a bounded `MYDMSVN_WEB6_CANDIDATE_ID`. Existing mutation routes do not accept or require the receipt. The v2 `sameKeyResendEnabled` field remains false.

## Implemented contract

The additive `/api/v3/operations` boundary provides authenticated no-store GETs for candidate context, pre-dispatch signed receipt and eligibility. The receipt uses a domain-separated HS256 key derived from the existing bounded root-key ring and binds actor, candidate, epoch, operation ID, fingerprint, kind, derived method/route and target for exactly 604800 server seconds.

Eligibility validates strict receipt/identity input before protected storage reads, then rechecks current session, original Write/Post grant, epoch and actor/key fence. A valid current receipt plus authorized absent owned row is the only `ALLOW/ORIGINAL_NOT_OBSERVED_RECEIPT_VALID` path. COMMITTED/expired/unavailable/NOT_EXECUTED map to stable DENY reasons; incomplete terminal audit maps to `INDETERMINATE/TERMINAL_AUDIT_INCOMPLETE`; a valid expired receipt maps to `DENY/RECEIPT_EXPIRED` without treating absence as rollback proof. Success returns no outcome or terminal metadata.

## Verification

Locked restore passed for both affected project graphs. Tests ran directly, once per final selector, Release/net48; no retry wrapper was used.

| Command / artifact | Executed | Pass | Fail | Skip/other | Result / SHA-256 |
|---|---:|---:|---:|---:|---|
| Final complete `MyDmsVn.Server.HttpHost.Tests` — `web6-http-review-comment-final.trx` | 618 | 618 | 0 | 0 | PASS in 4s at `b72e52b`; `c1cc5a3e7ea37bceadc98b4c3be69b8e73b80d2b22fd6bb51c384ccb0815ede0` |
| Owned SQL selection: `WebOperationResendEligibilityTests`, `WebOperationResolutionTests`, `WebOperationReadTests` — `web6-eligibility-sql.trx` | 52 | 52 | 0 | 0 | PASS in 3m13s; timeout/abort/not-executed 0; `b40fbb6d1f4920addce3b3bb0948f4eb1fa8dfa46d221c10cb8bfbba55f7d14a` |

The SQL selection uses the existing owned disposable fixture and asserts current epoch/session/original grant, absent row, retained/unavailable/expired results, terminal audit distinctions, identity conflicts and read-only behavior. All 52 completed and the owned fixture cleanup path completed; no allocation was quarantined or adopted by this task. It ran at `5bcf103`; later review corrections `1f0f135` and `b72e52b` change only HttpHost receipt/wire/tests/corpus and leave the SQL repository/interface/registration byte-identical. Historical quarantines were not queried or changed.

TDD records before independent review: configuration 54/54; receipt/auth 81/81; SQL repository 4/4; context/receipt endpoint 14/14; eligibility endpoint 25/25; focused frozen-wire regression 132/132. Review corrections added RED→GREEN coverage for expiry during the SQL wait, cross-candidate conflict, canonical Base64URL and signed extra/duplicate claims; the WEB6-focused selection passed 49/49. PR review then added a RED→GREEN case proving an expired receipt returns DENY even when the protected read returns COMMITTED resolution. The final complete/SQL rows above are the acceptance results, not a sum of overlapping focused runs.

## Corpus and hygiene

- New additive corpus: `docs/contracts/web6-resend-eligibility-v1`, with context, six receipt and eight eligibility fixtures.
- Frozen `docs/contracts/web4-v1`: 48 files; no diff from base `743a56b`.
- Frozen `docs/contracts/web5-resolution-v1`: 41 files; no diff from base `743a56b`.
- `git diff --check` passed. New contract blobs are tracked as LF; no dependency or lock-file change.

## Acceptance limits and handoff

B6-RPL-05 backend implementation is locally verified only. This is not a paired candidate, protected hosted SQL acceptance, IIS/IIS Express or browser acceptance, and it is not Owner approval to expose the product action. Frontend must still implement and verify the one-shot original-only IDB claim, accessible explicit consent and no-auto-retry UX under F02/F03. B05 must pin and exercise the exact frontend/backend candidate with real SQL and supported browsers. A separate explicit nonproduction decision is required after those gates. Until then Resend stays OFF, staging remains NOT ACCEPTED and production remains NO-GO.
