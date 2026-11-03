# WEB6 B04 Same-key Replay Proof Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove on owned Windows/net48 SQL fixtures that the existing six Sales/Goods Receipt HTTP mutation paths serialize same-key resend and operator races to at most one committed business effect.

**Architecture:** Extend the existing process-crash HTTP/SQL harness and independent physical oracle; do not add a resend endpoint or change production mutation semantics. New tests reuse the original immutable request, SQL actor/key/epoch fence, real worker processes, terminal operator procedure, and existing cleanup quarantine rules. Runtime source changes are allowed only if a new RED case exposes a real defect.

**Tech Stack:** C# 12, net48, xUnit 2, Microsoft.Data.SqlClient 7.0.2, OWIN Web API test host, SQL Server schema13, real `MyDmsVn.WebOperation.CrashHost` processes.

**Spec:** `docs/plans/20261103-001-web6-recovery-quality-backend.md` §5, `docs/design/WEB6_RECOVERY_OPERATION_PROTOCOL.md` §3, `docs/development/WEB6_BACKEND_ACCEPTANCE.md` B6-RPL-01..05.

## Global Constraints

- Preserve original operation ID, method, route, target, body, fingerprint, actor, session and deployment epoch.
- Never infer rollback or resend authority from 404/`NOT_OBSERVED`.
- Do not add automatic mutation retry, a privileged resend endpoint, eligibility runtime, schema migration or UI enablement.
- Use only freshly allocated owned disposable SQL databases; drain every process/connection before cleanup and quarantine on uncertain ownership/quiescence.
- Keep frozen WEB4/WEB5 corpora and archived WEB4/WEB5 evidence unchanged.

## Review Focus

- A resend-first worker can become the first business executor; all delayed peers must replay the same durable result.
- Two or more independent contenders must not create a second header, line, ledger, valuation, stock or journal effect.
- Operator-first `CANCELLED` must survive late original/resend requests without tombstone mutation.
- Original-first Commit must make the later operator fail without a false terminal audit.
- Wrong identity/grant/epoch/fingerprint, unavailable results and transport failures must remain fail-closed and never trigger mutation from status reads.

---

### Task 1: Resend-first and multi-contender six-command process proof

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6SameKeyReplayTests.cs`
- Reuse: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationCrashSqlTests.cs`

**Interfaces:**
- Consumes: existing partial `WebOperationHttpSqlTests`, `OwnedCrash`, `ArrangeCrash`, `CrashConfiguration`, `WaitForCrashFence`, `VerifyCrashCommitted`, `RecoverAndReplayCrash`.
- Produces: `Resend_first_and_two_delayed_same_key_workers_converge_for_all_six_commands()`.

- [ ] Add the SQL test that starts a held resend contender first, then two independent same-key worker processes, proves both wait on the same application lock, releases the winner, compares all three original response envelopes and runs the physical oracle.
- [ ] Run only the new fully-qualified test and record the first observed result; if it fails, confirm the failure is behavioral rather than setup/discovery.
- [ ] If a production defect is exposed, add the smallest owner-level fix only after the failing assertion exists; otherwise keep the PR test-only.
- [ ] Re-run the new test with TRX and require six commands, zero skip, worker drain and owned database cleanup.
- [ ] Commit the focused test change.

### Task 2: Operator-first and original-first terminal races for all six commands

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6SameKeyReplayTests.cs`
- Reuse: `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationResolutionMaintenance.cs`

**Interfaces:**
- Consumes: existing schema13 operator procedure and exact `WebOperationContext` constructed from the same immutable intent.
- Produces: `Operator_first_cancelled_blocks_delayed_original_and_resend_for_all_six_commands()` and `Original_first_commit_remains_authoritative_over_waiting_operator_for_all_six_commands()`.

- [ ] Add operator-first tests that commit exact CANCELLED audit/tombstone before two delayed HTTP workers, assert both fail safely, compare full business snapshots before/after and verify one unchanged journal/audit pair.
- [ ] Run the operator-first test alone and capture the first RED/observed outcome.
- [ ] Add original-first tests that hold the real writer before Commit, start the exact operator transaction, prove it waits on the actor/key fence, release the writer, assert COMMITTED remains authoritative and no terminal audit exists.
- [ ] Run the original-first test alone and capture the first RED/observed outcome.
- [ ] Apply only a minimal production fix if either test exposes a defect, then rerun both tests with zero skip and proven drain/cleanup.
- [ ] Commit the terminal race proof.

### Task 3: Security, failure and transport gate reconciliation

**Files:**
- Modify only if a coverage gap is real: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6SameKeyReplayTests.cs`
- Evidence inputs: existing `WebOperationJournalTests`, `WebOperationResolutionTests`, `WebOperationHttpSqlTests`, `WebOperationEndpointTests`, `WebOperationStatusEndpointTests`, and retry-boundary tests found by source search.

**Interfaces:**
- Consumes: current error mapping, original Write/Post authorization and status-read boundary.
- Produces: one source-verified selector set covering B6-RPL-04/B6-RPL-05 without changing frozen wire contracts.

- [ ] Map each required failure (actor/session/grant/epoch/fingerprint/target/kind, expired/unavailable, old mode/capability, lock timeout/cancellation, 401/404/5xx/redirect/no-auto-retry) to an actual test name.
- [ ] Add only missing focused assertions; each new behavioral assertion must be run before any required runtime correction.
- [ ] Run the source-verified SQL and SQL-free selectors and inspect TRX for nonzero discovery, executed/pass/fail/skip counts.
- [ ] Commit any necessary gap tests separately.

### Task 4: Evidence, self-review and PR

**Files:**
- Create: `docs/evidence/web6/20261103-b04-same-key-replay-proof.md`
- Modify: `docs/evidence/web6/README.md`

**Interfaces:**
- Consumes: candidate base/head SHA, TRX hashes/counts, SQL version/schema fingerprint, per-command oracle output and cleanup results.
- Produces: honest B6-RPL-01..05 PASS/FAIL/BLOCKED/NOT RUN register and frontend F02 handoff.

- [ ] Run locked restore, affected Release/net48 build, new B04 tests, mapped regressions and the complete affected security test project required by TDD completion policy.
- [ ] Record discovered/executed/pass/fail/skip, six-command effect counts, process drain, cleanup/quarantine, toolchain and P0/P1/P2 findings.
- [ ] State that eligibility/receipt/consent decisions remain pending and manual resend remains disabled.
- [ ] Self-review the diff against B6-RPL-01..05; fix all P0/P1 and disposition P2.
- [ ] Commit evidence, push `codex/web6-b04-same-key-proof`, and open the requested PR without merging.
