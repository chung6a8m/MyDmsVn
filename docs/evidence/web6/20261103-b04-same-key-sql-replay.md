# WEB6 B04 evidence — six-command same-key SQL replay

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / VERIFIED NONPRODUCTION for the existing journal and HTTP mutation boundary. Manual resend remains OFF; no eligibility endpoint or frontend behavior was added. Owner review and CI are pending.

## Candidate and environment

- Base: backend `fe790b14f0e1c2247fbe3f5a1fc519f293d63d5b` (merged B01 PR #99); paired frontend read-only reference `92afef7c9090605a172ffc351d1e641d9df12100` (merged F01 PR #33).
- Tested implementation commit: `1ab750189f65030c32a1054aeae6c7918b54c421` on `codex/web6-b04-same-key-proof`.
- Native Windows, net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`.
- SQL Server `14.0.2140.2`, Developer Edition (64-bit), engine edition 3. Fresh owned disposable databases were migrated to exact schema13; source/compatibility fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`.
- Changed runtime source: none. Changed test owners: `Web6SameKeyReplayTests.cs` and the existing crash fixture's explicit schema13 mode switch in `WebOperationCrashSqlTests.cs`. The implementation plan is under `docs/superpowers/plans/`.

The solution-level locked restore was attempted and failed before package work because three WinFormsKit vendor project files are not materialized in this worktree. Locked restores for the affected Security Tests and HttpHost Tests project graphs both passed. No vendor/submodule state was changed.

## Proof matrix and physical oracle

All eight process/SQL matrices executed on the exact candidate, each over Sales Create/Update/Post and Goods Receipt Create/Update/Post (48 command-order scenarios):

| Matrix | Result |
|---|---|
| Process death before Commit | 6/6 rolled back; independent status 404 remained observation-only; same-key worker then performed the original intent once |
| Process death at Commit entry | 6/6 classified from fresh SQL after drain; no process exit inference and no duplicate effect |
| Process death after Commit | 6/6 recovered the retained original envelope and replayed it without a second effect |
| Original first, later same-key worker | 6/6 contender application-lock waits observed; release replays the original result |
| Original first, killed before Commit | 6/6 contender waits observed; after independent rollback/quiescence it performs the original intent once under the same key |
| Resend first plus two delayed workers | 6/6 three independent workers converged to byte-equivalent success and one committed effect |
| Operator CANCELLED first | 6/6 delayed original/resend returned `Operation.ResultUnavailable`; zero business effect and unchanged terminal snapshot |
| Original COMMITTED first, operator waits | 6/6 operator application-lock waits observed, then `Operation.ResolutionConflict`; no false terminal audit |

The independent, non-pooled SQL oracle runs only after child/process drain and bounded epoch/key-fence quiescence. It checks exact actor/key/epoch/kind/target/fingerprint and one COMMITTED journal row; retained result bytes; header, ordered line IDs and row version; Receipt Post's intentional no-version response; posted state; stock balances; stock ledger; moving-average valuation; COGS; Sales duplicate product/gift lines; Goods Receipt line uniqueness; and an unaffected second warehouse. Replay snapshots prove no second source, sequence, stock, ledger, valuation or COGS effect.

## Final execution record

| Selector/artifact | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---|
| `WebOperationJournalTests|WebOperationResolutionTests|WebOperationCrashProcessTests` — `TestResults/web6-b04-final-core/b04-journal-resolution-process-final.trx` | 49 | 49 | 0 | 0 | PASS |
| Five original crash/contention matrices, each isolated — `web6-b04-final-before`, `-commit-window`, `-after`, `-delayed`, `-killed` | 5 | 5 | 0 | 0 | PASS; 30 command scenarios |
| Three new ordering matrices — `TestResults/web6-b04-final-new-races/b04-new-races-final.trx` | 3 | 3 | 0 | 0 | PASS; 18 command scenarios |
| HTTP/SQL identity, terminal, unavailable, 404 and no-auto-retry boundaries — `TestResults/web6-b04-final-boundaries/b04-failure-boundaries-final.trx` | 15 | 15 | 0 | 0 | PASS |
| SQL-free v1/v2 endpoint, legacy-mode and frozen error boundaries — `TestResults/web6-b04-final-http/b04-http-boundaries-final.trx` | 31 | 31 | 0 | 0 | PASS |
| **Successful final total** | **103** | **103** | **0** | **0** | **PASS** |

The test assembly and CrashHost were rebuilt from the candidate before execution; no `--list-tests` result is counted. Full solution build, IIS/IIS Express, browser, frontend and protected hosted CI were NOT RUN for this test-only PR.

## Failed development runs and resource disposition

RED/GREEN work found two fixture issues rather than a production defect: operator races initially used schema12, and the in-process operator observer passed a SQL session ID to a helper expecting an OS process ID. The final tests explicitly use schema13, observe the operator SQL session, use a dedicated MARS connection to drain the expected XACT_ABORT conflict, and dispose it before quiescence.

Five newly allocated development fixtures were fail-closed **QUARANTINED** after cleanup/quiescence could not be proved: four while diagnosing the operator-client transaction lifecycle and one when an eight-matrix run was mistakenly overlapped with another SQL test process. They were not inspected, adopted or deleted by prefix; their private identities are not published here. A combined run from that overlap recorded 2 pass/6 fail, with five fail-fast results after the first quarantine, and is not acceptance evidence. Every final successful matrix independently reported child drain, SQL quiescence/fence proof and owned database removal. The two historical WEB5 quarantines were never queried or changed.

## Acceptance and review

| Gate | State | Evidence |
|---|---|---|
| B6-RPL-01 | VERIFIED NONPRODUCTION | Six retained-result replay paths preserve bytes/version/line IDs and one effect |
| B6-RPL-02 | VERIFIED NONPRODUCTION | Six before/near/after death and both original/resend ordering matrices, real processes and independent SQL oracle |
| B6-RPL-03 | VERIFIED NONPRODUCTION | Six operator-first and original-first terminal races under exact epoch/actor/key/fingerprint fence |
| B6-RPL-04 | VERIFIED NONPRODUCTION | Actor/session/grant/epoch/body/target/kind/fingerprint, expired/unavailable, lock/cancel/availability and old-mode/capability cases fail closed |
| B6-RPL-05 | PARTIAL / OWNER DECISION PENDING | Server dispatch/no-auto-retry and 404 observation boundaries verified; proposed `web6-resend-eligibility-v1`, authoritative age source, seven-day ceiling and consent/claim policy remain unapproved and unimplemented |

Self-review findings: P0 0, P1 0, P2 1. The P2 is operational resource retention: five new quarantined development allocations require a separate exact-identity Owner disposition; no source safety defect remains known and this PR grants no cleanup authority. CI is pending. B04 does not authorize F02, production, or manual resend.

## F02 handoff

The existing `/api/v1` journal/write and `/api/v2` resolution contracts are now SQL-proven for the six commands: same actor/key/epoch/fingerprint serializes every order, retained COMMITTED replays the original result, EXPIRED/RESULT_UNAVAILABLE/CANCELLED never re-executes, and an absent row/404 is not rollback proof. No eligibility API exists; `sameKeyResendEnabled` remains false. F02 must therefore keep **Gửi lại đúng thao tác** disabled until the Owner selects the additive contract, authoritative age source, window and one-claim/consent policy, and F02/F03 receive their own verification and explicit nonproduction approval.
