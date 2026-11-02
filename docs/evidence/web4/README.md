# WEB4 backend evidence index

**Current bounded checkpoint - WEB4-05/B05 (2026-11-02): DONE (bounded; Owner accepted; PR #84 merged).** Owner accepted the recorded scope/limits and authorized merge after fresh reviews and exact-head CI passed. Backend [PR #84](https://github.com/chung6a8m/MyDmsVn/pull/84) and paired frontend [PR #21](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/21) are merged. Final Chromium and Edge each passed 11 browser cases plus the independent physical SQL oracle and cleanup; focused SQL owners passed 110 rows, hosted SQL-free CI passed 506 HttpHost + 62 fixture rows. [B05 evidence](20261102-b05-recovery-pilot.md) records exact candidates, results, failed attempts and limits; [runbook/interface](../../development/WEB4_RECOVERY_PILOT_RUNBOOK.md) owns the test-only scope. B06/F05 remain PLANNED. Same-key resend stays disabled; real process-crash/Commit, attended IME/Narrator and full IIS/production remain outside this acceptance. The B04 checkpoint below is historical.

**Previous B04 checkpoint (2026-11-02):** B01/B02 are DONE (bounded, accepted/merged); B03 is the merged prerequisite through [PR #82](https://github.com/chung6a8m/MyDmsVn/pull/82), backend base `8f8091575bbdab4d85c97f14d44b82030d70927d`. B04 is DONE (bounded; Owner accepted; PR #83 merged 2026-11-02); selected as the prerequisite for frontend WEB4-04 (F02/F03); B05/B06 remain PLANNED / NOT RUN. [Owning plan](../../plans/20261101-003-web4-reliability-server.md), [backend acceptance IDs](../../development/WEB4_BACKEND_ACCEPTANCE.md), [Codex handoff](../../development/WEB4_CODEX_HANDOFF.md). Original activation and dated evidence retain their own limits. B04 runtime/test candidate `bd871518fd162927d938babd5535a2ddf91c2ad5` has 604 local passing rows, 0 failed/skipped, as recorded in its evidence. Hosted Windows CI is PASS at tested head `b225bee1370abac77d4e852302ca45572cd8d3be` ([run 41363414852](https://github.com/chung6a8m/MyDmsVn/actions/runs/41363414852)); merge `59ca21d16f7839c6d7c553395a367e465591652b` has the same tree. Owner accepted and merged B04 on 2026-11-02; B05 browser acceptance and production approval remain pending or unapproved.

For each later task produce a new dated evidence file in this directory with exact backend/frontend source and fixture/corpus SHA, selected API/SQL test owner, exact invocation and actual executed PASS/FAIL/SKIP rows, errors/corrections, sanitized result/cleanup, remaining gates and explicit Owner decision. Distinguish host unit vs SQL integration vs published HTTPS browser vs .NET wrapper vs attended manual vs docs check. No test reports with secrets, credentials, token/cookie, HAR/trace or unrestricted DB identifying information.

Existing WEB2/WEB3 evidence remains immutable and candidate-specific. WEB4 source must be verified independently. Frontend F tasks/evidence live in ReactWebUI. CEX WinForms acceptance and full IIS/production remain independent.


## B01 — DONE (bounded contract)

[2026-11-01 B01 evidence](20261101-b01-contract.md) records source/canonical corpus validation and the frontend pin/signoff gate. Runtime/SQL/CI/browser are not implied.

## B02 — Windows/net48 CI

[B02 execution evidence](20261101-b02-windows-ci.md): selected by Owner; DONE (bounded): final executable candidate hosted push/PR PASS, Owner accepted; PR #81 merged 2026-11-01. B01 acceptance and old evidence remain unchanged.

## B03 — schema12 and transaction-owned journal

[B03 execution evidence](20261101-b03-schema-journal.md) preserves the original VERIFIED CANDIDATE checkpoint, source `e2976715bb106cc22c702e60ca2dd4536d6894ae`, selected local SQL/SQL-free rows, Local compatibility and hosted checks. PR #82 is now merged in backend base `8f8091575bbdab4d85c97f14d44b82030d70927d`; it is the B04 prerequisite. The historical evidence bytes and candidate-time disposition remain unchanged.

## B04 — six keyed mutations and protected context/status

[2026-11-02 B04 evidence](20261102-b04-http-operations.md): DONE (bounded; Owner accepted; PR #83 merged 2026-11-02). The [implementation checklist](../../development/WEB4_B04_IMPLEMENTATION_CHECKLIST.md) tracks source and verification work. Actual commands/counts and independent re-review belong to that record: runtime/test candidate `bd871518fd162927d938babd5535a2ddf91c2ad5` has 604 local passing rows, 0 failed/skipped. Hosted Windows CI at `b225bee` is PASS; Owner accepted and PR #83 merged at `59ca21d16f7839c6d7c553395a367e465591652b` on 2026-11-02. B05 browser pilot and production approval are not claimed.

## WEB4-05 / B05 — DONE (bounded; Owner accepted; merged)

[Recovery fixture and integrated pilot evidence](20261102-b05-recovery-pilot.md) records final paired Chromium/Edge 11/11 each, both independent SQL oracle/cleanup passes, 110 focused SQL rows and hosted 506 + 62 SQL-free rows. Fresh reviews and exact-head CI passed; Owner accepted the recorded scope/limits and PR #84 merged with paired frontend PR #21. The acceptance section records both merge SHAs and final CI runs. B06/F05 remain PLANNED.
