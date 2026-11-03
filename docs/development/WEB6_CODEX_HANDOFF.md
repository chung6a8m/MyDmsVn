# Codex handoff — WEB6 backend (new selected execution queue)

**Owner selection 2026-11-03. PLANNED / NOT EXECUTED.** [Roadmap 06](../plans/20261103-001-web6-recovery-quality-backend.md) owns B01–B06. [Safety protocol](../design/WEB6_RECOVERY_OPERATION_PROTOCOL.md), [acceptance gates](WEB6_BACKEND_ACCEPTANCE.md), [candidate runbook](WEB6_CANDIDATE_RUNBOOK.md), [WEB5 archive](../archive/20261103-web5-bounded-closeout/README.md), [WEB5 original contracts](../contracts/web5-resolution-v1/README.md) and [current selector](../plans/README.md). React companion [F01–F05](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261103-001-web6-recovery-quality-web.md) is independently owned.

## Task selector: one PR at a time

| Task | Read, inspect, modify | Stop / exit |
|---|---|---|
| **B01** contract/threat baseline | `SqlWebOperationJournal`, `SqlWebOperationResolutionMaintenance`, `SqlWebOperationMaintenance`, `013_WebOperationTerminalResolution.sql`, controller/wire and frozen WEB4/WEB5 corpora | Map exact source and all six commands; separate status vs manual same-key vs post-negative new intent; no runtime/flag enable |
| **B02a** operator read-only | Prefer isolated net48 CLI project with DI only for an explicit operator connection, `InspectAsync` and CLI help. Reuse SQL role/provision script. | Dry-run/inspect cannot mutate; exact actor/epoch/key/fingerprint, limited exit/secret-free; SQL user boundary and tests |
| **B02b** operator negative command | `RecordNotExecutedAsync` in owned transaction and explicit typed `--execute` with full metadata; SQL terminal triggers. | Fail closed: owner/role, wrong key, racing writer, cancelled transaction, ambiguous Commit and audit; zero unsafe unlock |
| **B03** maintenance/operator | `CompactAsync`, backup/restore to other disposable DB, epoch rotate before admission, retained tombstone, positive COMMITTED-result-unavailable case workflow | Bounded preview and atomic change, independent ledger oracle, complete cleanup only for owned fixtures; support closure does not SETTLE |
| **B04** manual resend SQL proof | `WebOperationCrashSqlTests`, `WebOperationJournalTests`, `WebOperationResolutionTests`, `WebHostIisExpressTests.Recovery.cs`; optional additive capability read if justified | Six-command original/resend/terminal race matrix PASS on real SQL, same effect once, no mutation replay middleware; B04 accepted before frontend UI enable |
| **B05a** operational hardening | `TraceSecurityAuditSink`, `AuthAttemptLimiter`, pipeline/security tests, no reverse proxy. | Actual sink ACL/retention and one-worker limitations measured; fail-closed, no unsupported multiworker claim |
| **B05b** paired integration | `Web5IisExpressHostAdmission`, `Web5OwnedSupervisor`, published host/React assets, candidate runbook | Verify owned queue+exclusive worker+TLS before HTTP; six commands/cutover/load/recovery/SQL oracle; release still NO-GO |
| **B06** acceptance dossier | Both repos' exact merged SHAs and hashes, baseline + metrics + no-go register | Owner signs separate nonprod resend, staging and production dispositions; no implicit production approval |

**Order:** B01 → B04 SQL proof and B02a parallel → F02 implementation guarded → B02b/B03 rehearsal and F03 native UI → B05a/B05b + F04 → B06 + F05. No B03 operator CLI requirement silently changes the WEB5 backend runtime.

## Implementation conventions

1. Before editing: fetch current main, check `AGENTS.md`, `AI_CONTEXT.md`, `docs/TEST_POLICY.md`, selected plan and code. Compare against archived WEB5 baselines, do not rely on handoff summaries as source. No new identity system, JWT storage change, schema migration for convenience or proprietary grid.
2. Use one small branch/PR per package; source + focused tests + dated evidence only for that package. Do not conflate docs-only selection with implementation. Preserve active independent CEX work, Desktop10, Web11 exact11, Web4 exact12, Web5 exact13 and existing schema fingerprints. New schema/contract requires separate reviewed ADR/version/migration and wire corpus; never mutate frozen blobs.
3. Test-first: enumerate RED cases per failure boundary, run them and record genuine RED; implement minimum change; run GREEN focused + integration and native Windows where relevant; distinguish 0 discovered/skip from PASS. Codex Local may lack elevated rights; do not bypass sandbox, SQL credentials, Windows features or test-host ownership policy.
4. All SQL destructive checks use newly allocated owned disposable DB, distinct runtime/operator users, current exact epoch and cleanup oracle. Never inspect or remove the two quarantined WEB5 databases by pattern. When policy blocks a tool, write `BLOCKED`, do not try alternative unauthorized routes.
5. Use locked restore, no surprise NuGet/npm upgrades. Windows encoding per repo; generated React docs UTF-8 (no BOM)/LF. Include PR body: feature subset, source pin, gates P0/P1/P2, actual TRX and checksum, native vs hosted vs local, failures, cleanup and retained blockers.
6. **Manual resend must be tested but not enabled by backend alone.** Preserve original v1 operation and existing HTTP POST/PUT route; false negative, status404, expired result or missing journal cannot trigger new key. Main runtime and SQL operator paths remain separate. No auto mutation retry or extra privileged endpoint.
7. Full IIS and unperformed Narrator remain Owner-accepted NOT RUN exceptions only within bounded nonproduction. Production remains NO-GO until new B06/F05 Owner decision; no previous WEB5 archive shall be edited or reactivated.

## Evidence/checkpoint handoff

Record B01 source/ADR review and each PR's `docs/evidence/web6/YYYYMMDD-bXX-*.md` with exact test counts. The [WEB6 evidence index](../evidence/web6/README.md) is **PLANNED** initially; update task state only after actual review/merge. End each Codex task with one summary: commit/PR URL, green/failed/skipped, source vs published artifacts, same-key effect count, SQL ownership/cleanup and pending Owner action.

**Never** run tests against non-owned staging/production SQL, send workload to a foreign IIS listener, enable HTTP resend for all users, auto-POST an UNKNOWN, delete journal/tombstone or adopt quarantined resources. If a required gate cannot be met on Windows Home, deliver correct scoped source/tests and an explicit BLOCKED handoff rather than silently claiming completion.
