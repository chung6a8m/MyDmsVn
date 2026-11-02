# WEB4 backend evidence index

**Roadmap 04 is ARCHIVED / COMPLETED (bounded), Owner 2026-11-02.** Backend B01–B06 are DONE within their recorded scopes. Owner accepted B06/F05 and its limits, authorized final reviews/CI and merge of backend PR #85/frontend PR #22, then explicitly requested archival. Read the [archive decision](../../archive/20261102-web4-reliability/README.md), [frozen completed plan](../../archive/20261102-web4-reliability/plans/20261101-003-web4-reliability-server.md), [former heading anchors](../../plans/20261101-003-web4-reliability-server.md) and [B06 acceptance evidence](20261102-b06-operator-handoff.md#owner-acceptance-and-merge--2026-11-02). No WEB4 task remains selected; future work needs a new explicit selection.

The [operator handoff](../../development/WEB4_OPERATOR_HANDOFF.md) and [host runbook](../../../src/MyDmsVn.Server.HttpHost/README.md#web4-coordinated-mode-and-migration-procedure) remain technical references. Preserve explicit `legacy-web11`/exact11 versus `web4`/exact12 matching artifacts/principal, same-transaction journal, current actor/session/permission and epoch/fingerprint checks, original command outcomes and Desktop exact10. Same-key resend stays disabled; missing status remains UNKNOWN. Real process-crash/Commit, negative administrative unlock, operator rotation/compaction/backup rehearsal, attended IME/Narrator and full IIS/production remain unverified or unapproved. Original evidence/WEB1–03 and independently ACTIVE CEX remain unchanged.

Original B01–B05 evidence remains byte-for-byte unchanged and candidate-specific. Only B06 and this index receive closeout updates. Hosted SQL-free CI, prior local SQL/browser verification, local documentation checks and attended/manual gates remain distinct evidence. No result is inferred from archive.

## B01 — DONE (bounded contract)

[2026-11-01 B01 evidence](20261101-b01-contract.md) records source/canonical corpus validation and the frontend pin/signoff gate. Runtime/SQL/CI/browser are not implied.

## B02 — Windows/net48 CI

[B02 execution evidence](20261101-b02-windows-ci.md): selected by Owner; DONE (bounded): final executable candidate hosted push/PR PASS, Owner accepted; PR #81 merged 2026-11-01. B01 acceptance and old evidence remain unchanged.

## B03 — schema12 and transaction-owned journal

[B03 execution evidence](20261101-b03-schema-journal.md) preserves the original VERIFIED CANDIDATE checkpoint, source `e2976715bb106cc22c702e60ca2dd4536d6894ae`, selected local SQL/SQL-free rows, Local compatibility and hosted checks. PR #82 is now merged in backend base `8f8091575bbdab4d85c97f14d44b82030d70927d`; it is the B04 prerequisite. The historical evidence bytes and candidate-time disposition remain unchanged.

## B04 — six keyed mutations and protected context/status

[2026-11-02 B04 evidence](20261102-b04-http-operations.md): DONE (bounded; Owner accepted; PR #83 merged 2026-11-02). The [implementation checklist](../../development/WEB4_B04_IMPLEMENTATION_CHECKLIST.md) tracks source and verification work. Actual commands/counts and independent re-review belong to that record: runtime/test candidate `bd871518fd162927d938babd5535a2ddf91c2ad5` has 604 local passing rows, 0 failed/skipped. Hosted Windows CI at `b225bee` is PASS; Owner accepted and PR #83 merged at `59ca21d16f7839c6d7c553395a367e465591652b` on 2026-11-02. B05 browser pilot and production approval are not claimed.

## WEB4-05 / B05 — DONE (bounded; Owner accepted; merged)

[Recovery fixture and integrated pilot evidence](20261102-b05-recovery-pilot.md) records final paired Chromium/Edge 11/11 each, both independent SQL oracle/cleanup passes, 110 focused SQL rows and hosted 506 + 62 SQL-free rows. Fresh reviews and exact-head CI passed; Owner accepted the recorded scope/limits and PR #84 merged with paired frontend PR #21. The acceptance section records both merge SHAs and final CI runs. That B05 checkpoint preceded B06/F05 completion below.

## WEB4-06 / B06 — DONE (bounded; Owner accepted; merged)

[Operator handoff evidence](20261102-b06-operator-handoff.md#owner-acceptance-and-merge--2026-11-02) records accepted documentation scope, independent reviews, final hosted 506 + 62 SQL-free passes, backend PR #85 and frontend PR #22 merges, exact candidate/tree identities and Owner-authorized archival. The operator guide remains a live reference; no B06 local runtime/SQL/maintenance rehearsal was run. Full IIS/production and retained crash/resend/negative-unlock gates remain unapproved or unverified.
