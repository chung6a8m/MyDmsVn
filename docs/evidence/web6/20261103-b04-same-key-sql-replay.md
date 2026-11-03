# WEB6 B04 evidence — six-command same-key SQL replay

**Date:** 2026-11-03 (Asia/Bangkok). **State:** B04 DONE (BOUNDED) / OWNER ACCEPTED / SQL-VERIFIED NONPRODUCTION. Manual resend remains OFF; no eligibility endpoint or frontend behavior was added. B6-RPL-05 policy/contract decisions remain PARTIAL and production remains NO-GO.

## Candidate and environment

- Base: backend `fe790b14f0e1c2247fbe3f5a1fc519f293d63d5b` (merged B01 PR #99); paired frontend read-only reference `92afef7c9090605a172ffc351d1e641d9df12100` (merged F01 PR #33).
- Tested implementation commit: `1ab750189f65030c32a1054aeae6c7918b54c421` on `codex/web6-b04-same-key-proof`.
- Review-fix workflow/register commit: `ffccfda76313c275dcad98cd2453bde1ea0a1476`; this adds no runtime/test behavior.
- Round-2 exact13 isolation and workflow commit: `d7e354e093015516982d4605353da6d44085a54d`; five historical WEB4 methods remain on their unchanged schema12 path, while five distinct WEB6 wrappers select schema13/web5.
- Reconciled protected-SQL-tested source/evidence baseline: `2b776b320202a9133c2ce7b329871fb043392831`, after bounded B02a PR #101 merged into `main`.
- Native Windows, net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`.
- SQL Server `14.0.2140.2`, Developer Edition (64-bit), engine edition 3. Fresh owned disposable databases were migrated to exact schema13; source/compatibility fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`.
- Changed runtime source: none. Changed test owners: `Web6SameKeyReplayTests.cs` and additive wrappers/fixture guards in `WebOperationCrashSqlTests.cs`. Every owned WEB6 fixture independently checks migration 013, the schema13 compatibility checker and definition fingerprint, a nonempty deployment epoch, and HTTP context `web5-resolution-v1` with resend still disabled before its matrix body starts. The implementation plan is under `docs/superpowers/plans/`.

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

## Round-2 regression record

| Selector/artifact | Discovered | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---:|---|
| Exact eight WEB6 exact13/web5 matrices — `TestResults/web6-b04-round2-eight-final/b04-round2-eight-final.trx` | 8 | 8 | 8 | 0 | 0 | PASS in 9m45s; 48 command scenarios; TRX names are the five WEB6 wrappers plus the three existing WEB6 methods |
| Five historical WEB4 schema12 matrices — `TestResults/web6-b04-round2-web4-history/b04-round2-web4-history.trx` | 5 | 5 | 5 | 0 | 0 | PASS in 6m05s; 30 historical command scenarios |
| Journal/resolution/process regression — `TestResults/web6-b04-round2-core/b04-round2-core.trx` | 49 | 49 | 49 | 0 | 0 | PASS in 3m08s |
| HTTP/SQL identity, permission/session, epoch/fingerprint, expired/unavailable/terminal/404 and no-auto-retry boundaries — `TestResults/web6-b04-round2-boundaries/b04-round2-boundaries.trx` | 15 | 15 | 15 | 0 | 0 | PASS in 1m35s |
| SQL-free v1/v2 endpoint, legacy-mode and frozen error boundaries — `TestResults/web6-b04-round2-http/b04-round2-http.trx` | 31 | 31 | 31 | 0 | 0 | PASS in 3s |

The obsolete mixed-schema aggregate of 103 is not reused. Discovery and actual TRX execution are reported per selector above. The Security test assembly and CrashHost were rebuilt in Release/net48 before execution; the HttpHost boundary project was also built in Release/net48. No `--list-tests` result is counted as execution. All SQL owners ran sequentially; a post-run process census found zero matching testhost, CrashHost, Operator or DbMigrator processes. At this local regression checkpoint, full solution build, IIS/IIS Express, browser and frontend were NOT RUN; the later protected hosted SQL result is recorded separately below.

## Protected exact-candidate SQL CI handoff

`.github/workflows/backend-web6-b04-sql-candidate.yml` is a dedicated Owner-tag-triggered Windows/net48 workflow. It reuses the protected `web5-sql-candidate` environment, exact-SHA dual checkout, frozen WEB4 corpus check, evidence-guard self-test, private SID-owned LocalDB lifecycle and sanitized artifacts. Its filter names exactly the five WEB6 wrappers plus three existing WEB6 methods; actual TRX names must exactly match that allowlist and reconcile to 8 executed / 8 passed / 0 failed / 0 skipped / 0 retry. Each method runs its embedded six-command independent SQL oracle and fixture-level exact13/web5 guard. The per-method hang bound is 180 seconds because the slowest measured six-command exact13 matrix took 1m38s; the job remains bounded at 25 minutes. The job refuses completion unless the exact candidate SHA remained unchanged and the owned instance reports verified ownership, zero user databases/active owners, graceful removal and no quarantine.

**PROTECTED SQL CI PASS / OWNER ACCEPTED.** Owner authorized the exact-head tag and protected environment on 2026-11-03. [Run `41643581370`](https://github.com/chung6a8m/MyDmsVn/actions/runs/41643581370) attempt 1 passed on exact reconciled SHA `2b776b320202a9133c2ce7b329871fb043392831`. The protected environment retained its WEB5 tag policy and added the reviewed WEB6 B02a/B04 tag families; no protected rule was removed.

Sanitized artifact `web6-b04-sql-candidate-counts` has GitHub digest `sha256:0436b425956c07de96cfab5b483b48076d71d76152a79817f09baa9351fb9718`. Its evidence records discovered/unique/total/executed/passed `8/8/8/8/8`, failed/skipped/retry `0/0/0`, exact class count 8 and the exact allowlisted five WEB6 wrappers plus three existing WEB6 method names. Lifecycle reports run/attempt `41643581370/1`, image `20261026.347.1`, SQL `15.0.4382.1` Express, compatibility 140, matching `controlSha`/`testSourceSha`, phase `Removed`, verified SID ownership, zero user databases, zero active owners and no quarantine. All exact-source, real SQL/process matrices, source-integrity, drain/removal, evidence guard and sanitized-upload steps passed. Existing SQL-free `Backend Web checks` are supporting evidence, not the SQL substitute.

## Failed development runs and resource disposition

RED/GREEN work found two fixture issues rather than a production defect: operator races initially used schema12, and the in-process operator observer passed a SQL session ID to a helper expecting an OS process ID. The final tests explicitly use schema13, observe the operator SQL session, use a dedicated MARS connection to drain the expected XACT_ABORT conflict, and dispose it before quiescence.

Six newly allocated development fixtures are fail-closed **QUARANTINED**: the prior four operator-client lifecycle allocations, the prior overlapped-run allocation, and one Round-2 allocation whose testhost was killed by the former 90-second blame-hang bound before fixture cleanup could report. The Round-2 process census was clean, but that is not database ownership/quiescence proof; the allocation was not queried, adopted or deleted. An earlier guard-development run also failed 8/8 because it read `Data.Epoch` before arrangement; all eight fixtures in that run explicitly reported normal owned cleanup, so it created no quarantine and is not acceptance evidence. Every final successful matrix independently reported child drain, SQL quiescence/fence proof and owned database removal. The two historical WEB5 quarantines and the prior five WEB6 quarantines were never queried or changed. The [sanitized quarantine register](20261103-b04-quarantine-register.md) records both historical WEB5, six WEB6 development and successful-fixture dispositions; exact identities remain private and require a separate Owner/operator decision.

## Acceptance and review

| Gate | State | Evidence |
|---|---|---|
| B6-RPL-01 | VERIFIED NONPRODUCTION | Six retained-result replay paths preserve bytes/version/line IDs and one effect |
| B6-RPL-02 | OWNER ACCEPTED / VERIFIED NONPRODUCTION | Six before/near/after death and both original/resend ordering matrices, real processes and independent SQL oracle pass locally and on the exact-head protected SQL candidate with owned cleanup |
| B6-RPL-03 | VERIFIED NONPRODUCTION | Six operator-first and original-first terminal races under exact epoch/actor/key/fingerprint fence |
| B6-RPL-04 | VERIFIED NONPRODUCTION | Actor/session/grant/epoch/body/target/kind/fingerprint, expired/unavailable, lock/cancel/availability and old-mode/capability cases fail closed |
| B6-RPL-05 | PARTIAL / OWNER DECISION PENDING | Server dispatch/no-auto-retry and 404 observation boundaries verified; proposed `web6-resend-eligibility-v1`, authoritative age source, seven-day ceiling and consent/claim policy remain unapproved and unimplemented |

Round-2 final review findings: P0 0, P1 0, P2 0. Owner accepted the bounded exact-head SQL proof. Six WEB6 quarantined development allocations still require a separate exact-identity Owner disposition; this acceptance grants no cleanup authority. B6-RPL-05 remains PARTIAL because the additive eligibility contract, authoritative age source and product consent/claim decisions are not implemented or approved. B04 completion does not authorize F02, staging, production or manual resend.

## F02 handoff

The existing `/api/v1` journal/write and `/api/v2` resolution contracts are now SQL-proven for the six commands: same actor/key/epoch/fingerprint serializes every order, retained COMMITTED replays the original result, EXPIRED/RESULT_UNAVAILABLE/CANCELLED never re-executes, and an absent row/404 is not rollback proof. No eligibility API exists; `sameKeyResendEnabled` remains false. F02 must therefore keep **Gửi lại đúng thao tác** disabled until the Owner selects the additive contract, authoritative age source, window and one-claim/consent policy, and F02/F03 receive their own verification and explicit nonproduction approval.
