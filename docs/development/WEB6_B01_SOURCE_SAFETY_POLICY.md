# WEB6 B01 — Source and safety policy checkpoint

**Status: READY FOR OWNER TECHNICAL REVIEW, 2026-11-03. No runtime capability is enabled.** This checkpoint maps runtime source at backend `c7782429e105688e916fe2a0d41bb58fc0a1cb4d` and paired frontend `a011230fc9d5057ab76321251ad0a292e18aecbc`. Original checkpoint content commits are backend B01 `8586de3` and frontend F01 `9f5ae59`; reviewed provenance/reference fixes are backend `8eb5cc9` and frontend `c6344b4`. These documentation commits do not change the runtime pins. It does not implement B02–B06/F02–F05, change a frozen contract, or authorize a resend.

## 1. Frozen baseline

| Item | Verified value |
|---|---|
| WEB4 contract | 48 tracked files; Git tree `fcab4a1e7505b150a132ce2899002930e9f2a6ee` |
| WEB5 additive contract | 41 tracked files including manifest; manifest SHA-256 `fef5f1f18d3a556bf35d5c9ed6bc38af3eeb0c1a89dd5f9d125d7fb190a133b2` |
| Schema/mode | exact schema13 / `web5`; fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9` |
| Result retention | `ResultExpiresAtUtc = CompletedAtUtc + 7 days`; expiry/compaction retains actor/key/epoch/fingerprint tombstone |
| Principals | restricted runtime principal; distinct operator role for exact procedures; migration/maintenance identity remains separate |
| Candidate status | same-key resend OFF; staging not accepted; production NO-GO/DEFERRED |

The seven-day value is result-byte availability, not deletion of the operation identity and not proof that an absent row rolled back. Historical quarantine and WEB5 archive remain untouched.

### Source, dependency and publish inventory

| Inventory | Pinned checkpoint |
|---|---|
| Backend platform | .NET SDK `8.0.400` driving net48 projects; `global.json` SHA-256 `b245497a2898df63c85fa09a4e0117613552d3d97f4b40f9ecc72e7c734b4b8c` |
| Central packages relevant to the boundary | MediatR `12.5.0`, MediatR.Contracts `2.0.1`, Microsoft.Data.SqlClient `7.0.2`, Newtonsoft.Json `13.0.4` |
| Locked owners | HttpHost `e415809ffd9971e46383ed770cc8a3f2186d4b64e378aa8ef69a9a9a40d16f36`; HttpHost.Tests `18d1ec93be5ac20f1c605504feb6a3e82325571d441a683a61385d918eea986d`; Security.Tests `bbdee8bba02e7da16851a41c82120572316d9c569cd08dae9bbde174fbf996fd`; Inventory.Tests `b02c41ad3104d5a8ce114b19c538cb463b6996871d47d37b251e6bd00957188d`; Migrations.Tests `1d85d70e4c036a3169b8e8ea705da04ba6eae72990fc7228f1ec4f472367f194` |
| CI inventory | `.github/workflows/backend-web.yml`; protected SQL tag workflows `backend-web-sql-baseline.yml` and `backend-web-sql-candidate.yml` |
| Candidate publish provenance | **NOT BUILT / NOT AVAILABLE for B01.** No backend binary, React asset, IIS directory, SQL allocation, manifest, or publish hash was created. B05/F04 owns the future pinned published pair. |

## 2. Three actions that must not collapse

| Action | Key | Permitted result |
|---|---|---|
| Protected status/resolution GET | Original key | Read/reconcile only; bounded read retry is safe |
| Manual same-key resend | **Original key only** | One user-consented additional transmission; it can perform the original business action for the first time or replay its committed result |
| New intent after authoritative `NOT_EXECUTED` | New GUIDv4 | Separate fresh baseline and consent; never presented as a resend |

No business mutation is retried automatically after 401/refresh, timeout, 4xx/5xx, cancellation, reload, timer, service worker, or library retry. A resend never creates a new operation ID and never rebuilds the body from current UI state.

## 3. Current source and method map

Line references are for the pinned backend commit above.

| Boundary | Current owner and method | Safety property / gap |
|---|---|---|
| Journal admission | `SqlWebOperationJournal.AdmitAsync`, `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationJournal.cs:30` | Explicit transaction; epoch then actor/key application lock; current actor/session/original Write or Post grant; matching committed row replays, absent row admits original work |
| Journal completion | `SqlWebOperationJournal.CompleteAsync`, same file `:61` | Business result and COMMITTED journal insert share caller transaction; result expires after seven days (`:70`) |
| Protected v1 status | `SqlWebOperationReadRepository.ReadStatusAsync`, `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationReadRepository.cs:47`; `OperationsController.Status`, `src/MyDmsVn.Server.HttpHost/OperationsController.cs:40` | Same fence/current session/original grant; absent/foreign actor is indistinguishable 404; envelope has no trusted resend window |
| Protected v2 resolution | `SqlWebOperationResolutionRepository.ReadResolutionAsync`, `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionRepository.cs:20`; `OperationResolutionController.Resolution`, `src/MyDmsVn.Server.HttpHost/OperationResolutionController.cs:36` | Adds durable CANCELLED/`NOT_EXECUTED`; missing row remains 404; frozen `web5-resolution-v1` response must not change |
| Capability context | `OperationResolutionController.Context`, same file `:20` | Exact13/web5 advertises `sameKeyResendEnabled: false`; no server clock, retention deadline, or per-operation admission |
| Operator inspection/negative | `SqlWebOperationResolutionMaintenance.InspectAsync` / `RecordNotExecutedAsync`, `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionMaintenance.cs:18,55` | Explicit operator construction and transaction; absence is `INDETERMINATE`; procedure success is not Commit proof |
| Compaction | `SqlWebOperationMaintenance.CompactAsync`, `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationMaintenance.cs:18` | Requires database CONTROL and audit attempt; only eligible result bytes become EXPIRED; not available to runtime/ordinary operator |
| Sales Create / Update / Post | `CreateSalesReceiptDraftCommandHandler.Handle`, `src/MyDmsVn.Server.Application/Sales/SalesReceiptFeatures.cs:157`; `UpdateSalesReceiptDraftCommandHandler.Handle`, same file `:216`; `PostSalesReceiptCommandHandler.Handle`, `src/MyDmsVn.Server.Application/Sales/PostSalesReceipt.cs:49` | Each calls `ReplayAsync` before business work and `CompleteAsync` before the same UoW commit |
| Receipt Create / Update / Post | handlers in `src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs:266,357,438` | Same transaction-owned replay/complete pattern; Receipt Post result intentionally has no version |
| Runtime principal | `deployment/sql/Provision-MyDmsVnWebPrincipals.sql` | No CONTROL/DDL/journal UPDATE/DELETE and no terminal procedure execution |
| Operator principal | `deployment/sql/Provision-MyDmsVnWebOperationOperator.sql` | EXECUTE only on exact inspect/terminal procedures; no broad runtime elevation |

The paired [frontend F01 checkpoint](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/9f5ae59/docs/development/WEB6_F01_SOURCE_SAFETY_POLICY.md) owns the full client map. At the pinned frontend runtime source: `frozenWriteRequest` is `src/infrastructure/http/frozenWriteRequest.ts:10`; `createWeb4MutationRuntime` and ordinary `write()` are `src/app/runtime/web4MutationRuntime.ts:60,303`; IDB open/transaction/original `begin` are `src/infrastructure/drafts/web4OperationRepository.ts:180,225,245`. The frozen request sends the persisted original GUID/method/route/body/epoch, while ordinary `write()` creates a new UUID and is therefore forbidden for resend.

## 4. State-transition and eligibility matrix

`UNKNOWN` is a client truth: dispatch may have happened but no authoritative result was durably adopted. `NOT_OBSERVED` is a protected-read observation: the current query did not reveal a row. Neither is a rollback fact.

| Starting observation | Authoritative interpretation | Manual same-key decision | Next safe state/action |
|---|---|---|---|
| Local `DISPATCHED/UNKNOWN`; server COMMITTED + retained result | Original business action committed | Do not resend | GET result, verify identity, atomic local settle |
| COMMITTED + EXPIRED/RESULT_UNAVAILABLE | Business commit may be proven but original client result is unavailable | Deny | Keep protected; operator/business verification only; never fabricate version/line IDs |
| Durable matched CANCELLED/`NOT_EXECUTED` | Original is terminally prevented under the same key | Deny old key | Existing negative CAS; separately reviewed new intent with new GUID after fresh baseline |
| `NOT_OBSERVED`/404 | No row was disclosed after the protected read; original may be delayed, rolled back, never arrived, or hidden by a failing/unsupported path | **404 alone never allows resend** | Remain UNKNOWN; only a separate versioned admission plus explicit consent could allow a same-key send |
| Status timeout/5xx/host down | No authoritative observation | Deny | Remain UNKNOWN; bounded GET later |
| Wrong actor/session/grant/epoch/fingerprint/kind/target | Security or immutable identity mismatch | Deny | Protected; no cross-actor disclosure or local unlock |
| Old/non-web5 host, feature OFF, unpinned candidate | Capability is absent | Deny | Protected positive legacy GET only where supported |
| Exact original corrupt/missing/oversized | Frozen request cannot be proved | Deny | Quarantine local record; never reconstruct key/body |
| Eligible preflight, then actor/grant/epoch changes | Admission became stale | Deny before send | Consume no network attempt; local claim policy is owned by F02 |
| Same-key original and resend race | Either request may reach the fence first | Potentially allowed only after all gates | Both serialize on actor/key/epoch; maximum one committed business effect |

The important `NOT_OBSERVED` case can make the manual resend the **first** business execution. Consent text and B04 tests must say and prove that explicitly.

## 5. Threat matrix

| Threat | Failure if mishandled | Required control | Proof owner |
|---|---|---|---|
| Treat 404 as rollback | First or delayed original can race a new execution | 404 remains UNKNOWN; separate admission; same original key | B01 policy, B04 SQL |
| New UUID or edited body | Duplicate/different business intent | Persisted frozen bytes and GUID only; recompute fingerprint | F02 codec/runtime, B04 oracle |
| Automatic mutation retry | Multiple transmissions without fresh consent | Transport forbids unsafe 401 replay; no timer/offline/reload retry | Existing source + F02/F03 tests |
| Stale actor/session/grant | Unauthorized original-result disclosure or write | Recheck before/after async and inside journal transaction | B04/F02/F03 |
| Epoch/database rewind | Key applied to incompatible history | Exact epoch fence; rotate only on clone/rewind before writers | B03/B05 |
| Expired result interpreted as absence | Reexecution after known business commit | Permanent EXPIRED tombstone; protected operator path | Existing SQL + B04 |
| Two tabs/devices | More than one HTTP contender | IDB claim coordinates one storage partition; SQL journal is cross-device authority | F02 + B04 |
| Claim crash before/on fetch | Consent silently reused | Claim stays consumed; UNKNOWN + GET only | F02 |
| Operator races original | False negative or mutation after terminal decision | Same epoch+actor/key SQL fence and explicit transactions | B02/B04 |
| Runtime SQL elevation | Web compromise gains terminal/maintenance authority | Preserve separate restricted principals; no runtime DI/admin endpoint | B02/B03 |
| Historical cleanup by prefix | Irrecoverable evidence/database loss | Quarantine unchanged; exact ownership and separate authority required | B05/B06 |
| Frozen corpus edited in place | Old/new clients disagree silently | New versioned contract only; retain WEB4/WEB5 hashes | B01/B04 |

## 6. Minimal feasible proposal for B04/F02 — not implemented here

Current `web5-resolution-v1` cannot positively establish resend eligibility: context is fixed OFF, and status/resolution has neither server time nor a resend window. Therefore F02 must remain blocked from enabling the action.

Recommended design is a **separate additive read-only evaluation contract**, provisionally `web6-resend-eligibility-v1`; it must not alter `/api/v1` or `/api/v2` envelopes or their corpora. The request supplies the exact original operation identity and strictly decoded frozen intent; the server recomputes the fingerprint, checks exact13/web5 candidate pin, feature gate, current actor/session/original Write-or-Post grant, epoch, method/route/target, journal/tombstone state, and its own UTC clock. The response is bounded to `ALLOW | DENY | INDETERMINATE`, a stable reason, server time, and an absolute short-lived `eligibleUntilUtc`. It returns no business payload and is only a hint: actual POST/PUT repeats every check and the SQL fence is final.

An absent GUIDv4 journal row has no authoritative server creation time. B04 must therefore choose and prove one of these age sources before any `ALLOW` for `NOT_OBSERVED`:

1. **Recommended:** a server-issued, bounded, opaque receipt bound to actor/epoch/operation ID/fingerprint before the original dispatch, later verified by the eligibility evaluation. This gives authoritative age without a new business key or runtime SQL elevation.
2. Owner-accepted weaker product policy: compare persisted `createdAt` against trusted server UTC and treat it only as a conservative UI gate. This does not make browser time authoritative and cannot be a server security boundary.

Until Owner chooses an age source and B04 proves it, the endpoint design returns `INDETERMINATE` for `NOT_OBSERVED`; the UI remains OFF. A server answer never reserves the future write, and a later race can still change the outcome.

## 7. Existing test and CI map

| Area | Existing tests | What they prove / do not prove |
|---|---|---|
| Journal/fence | `WebOperationJournalTests` | Same-key wait/replay, rollback then same intent, authority/epoch/expiry/tombstone; not product consent |
| Protected reads | `WebOperationReadTests`, `WebOperationResolutionTests` | Actor/session/grant/epoch/fingerprint, absent vs terminal, retention metadata; not manual resend eligibility |
| Six-command crash/race | `WebOperationCrashSqlTests`, `WebOperationCrashProcessTests` | Real process death and same-key worker orders across six commands; historical WEB5 evidence only |
| HTTP/wire | `WebOperationEndpointTests`, `WebOperationStatusEndpointTests`, `WebOperationResolutionEndpointTests`, `WebOperationWireTests`, `WebOperationResolutionWireTests` | Strict route/headers/corpus and frozen contracts; no WEB6 contract yet |
| Exact13 bridge | `WebOperationHttpSqlTests.Web5Resolution`, inventory command/terminal tests | Existing writes/status/negative behavior and physical effects; no enabled product resend |
| CI | `backend-web.yml`, protected tag workflows `backend-web-sql-baseline.yml` / `backend-web-sql-candidate.yml` | SQL-free main coverage and explicit protected SQL gates; no fresh B01 runtime run is claimed |

## 8. Owner decisions required before B04/F02

- Approve the separate `web6-resend-eligibility-v1` direction and forbid changes to frozen v1/v2 payloads.
- Select the authoritative age source; recommendation is the server-issued bound receipt.
- Confirm seven days as the maximum initial manual-resend eligibility window, distinct from permanent tombstone retention.
- Confirm one durable browser claim maximum per original key and that a consumed ambiguous claim is not reset.
- Confirm the explicit warning that a resend after `UNKNOWN`/`NOT_OBSERVED` can execute the original business action for the first time.

No later checkpoint may interpret this proposal as approval. B04 must add real SQL proof for all six commands and both original/resend orders before F02/F03 can expose an action.

## 2026-11-09 additive contract follow-up

Owner approved the separate receipt-based direction, original-key-only semantics, seven-day maximum and explicit first-execution warning for backend implementation. The resulting `web6-resend-eligibility-v1` contract is additive and default OFF. It uses a server-signed pre-dispatch receipt bound to actor/candidate/epoch/operation/fingerprint/kind/method/route/target, and a read-only eligibility route that returns only `ALLOW`, `DENY` or `INDETERMINATE`. Frozen `/api/v1` and `/api/v2` payloads are unchanged.

This closes the backend contract implementation decision only. Receipt possession is not consent or authorization; each read rechecks the current active session and original Write/Post grant, and the later business mutation still relies on the existing journal fence. Frontend one-shot IDB claim, accessible user approval, real paired browser/SQL acceptance and explicit nonproduction enablement remain separate gates. Same-key Resend remains OFF; staging is not accepted and production remains NO-GO. See [2026-11-09 B04 evidence](../evidence/web6/20261109-b04-resend-eligibility.md).
