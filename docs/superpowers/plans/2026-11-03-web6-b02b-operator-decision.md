# WEB6 B02b Guarded Operator Terminal Decision Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the isolated net48 operator CLI with guarded `record-not-executed` dry-run and execute modes that preserve the restricted principal, commit exactly one schema13 terminal decision, and independently verify the durable result.

**Architecture:** Keep inspect behavior stable and add decision-specific options, application, and SQL client types. Reuse the existing connection target/effective-permission guard and `SqlWebOperationResolutionMaintenance`; dry-run performs only guarded inspection, while execute rechecks state, calls the existing stored procedure in one explicit transaction, treats Commit uncertainty as `UNKNOWN/ESCALATE`, and verifies CANCELLED plus the matching immutable audit over a new guarded connection after known Commit.

**Tech Stack:** C# / net48, Microsoft.Data.SqlClient, xUnit v2, SQL Server schema13, GitHub Actions Windows protected SQL candidate workflow.

**Spec:** Owner task `Roadmap 06 / B02b: Guarded Operator Terminal Decision`, `docs/plans/20261103-001-web6-recovery-quality-backend.md` B02, `docs/design/WEB6_RECOVERY_OPERATION_PROTOCOL.md`, and `docs/development/WEB6_BACKEND_ACCEPTANCE.md` B6-OPS-03/04.

## Global Constraints

- No schema13, HTTP runtime, DI, JWT/session, frontend/manual-resend, automatic mutation retry, or frozen corpus changes.
- Operate only through the dedicated operator connection and existing `dbo.RecordWebOperationNotExecuted`; never add direct journal/audit DML or broaden operator grants.
- Parse and validate every argument before constructing a SQL client; require exactly one of `--dry-run` or `--execute`, and exact typed confirmation for execute.
- Keep existing inspect exit codes unchanged; add documented decision-specific preview/unknown outcomes without printing secrets, connection data, fingerprint, or business payload.
- SQL destructive tests use only fresh owned disposable schema13 databases and exact owned logins; no historical quarantine access or cleanup.
- Manual resend remains OFF, staging remains NOT ACCEPTED, production remains NO-GO.

## Review Focus

- A confirmation generated for another database/environment or with one changed metadata field must be rejected before SQL.
- An absent journal row remains INDETERMINATE during dry-run and is never described as proof of NOT_EXECUTED.
- A Commit exception/deadline must never be converted to success/failure or retried; output must say `UNKNOWN/ESCALATE` and identify the independent inspect recovery command shape.
- Effective privileges inherited through `PUBLIC`, nested roles, custom securables, or fixed roles must deny both dry-run and execute.
- Post-Commit success requires a fresh connection proving CANCELLED and the exact decision/case/reason/evidence audit; a verification failure is escalation, not a second execute.

---

### Task 1: Decision command contract and SQL-free application behavior

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/RecordNotExecutedOptions.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorDecisionApplication.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorDecisionResult.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/Program.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/OperatorExitCode.cs`
- Test: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`

**Interfaces:**
- Produces: strict `RecordNotExecutedOptions.TryParse`, `ConfirmationText`, `IOperatorDecisionClient.DryRunAsync/ExecuteAsync`, and `OperatorDecisionApplication.RunAsync`.
- Produces: exit code `7` for a completed read-only preview and `8` for `UNKNOWN/ESCALATE`; codes 0/2/3/4/5/6/130 retain B02a meaning.

- [ ] Write parser/application tests for help, both modes, exact composite confirmation, every field mutation, duplicate/unknown/missing arguments, GUIDv4, kind/target, canonical actor/epoch/SHA-256, schema13 metadata patterns, timeout, sanitized output, denied/timeout/cancelled/environment/unknown mapping, and zero client construction on invalid input.
- [ ] Run the focused new SQL-free tests and verify RED because the decision contract does not exist.
- [ ] Implement the minimal decision options/application/result types and command dispatch/deadline routing.
- [ ] Run the focused tests and existing inspect parser/application tests; verify GREEN and unchanged inspect exit behavior.
- [ ] Commit the contract slice.

### Task 2: Guarded dry-run and transaction-owned execute client

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorDecisionClient.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorInspectionClient.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/OperatorInspectionResult.cs`
- Test: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`

**Interfaces:**
- Consumes: validated `RecordNotExecutedOptions` and current `SqlWebOperationResolutionMaintenance.InspectAsync/RecordNotExecutedAsync`.
- Produces: reusable guarded target connection contract, read-only preview result, known-commit verified result, and ambiguous-commit result.

- [ ] Add real-SQL tests proving dry-run byte-equal snapshots for missing/existing states, wrong target/login/user/effective permission denial, and no audit/journal/business mutation.
- [ ] Run those tests and verify RED because no decision SQL client exists.
- [ ] Refactor only the option surface needed to reuse `OperatorSqlConnectionFactory` without weakening its effective-permission allowlist.
- [ ] Implement dry-run as guarded independent inspection and execute as pre-inspection, one explicit ReadCommitted transaction, existing maintenance call, known Commit, and fresh guarded post-Commit inspection plus exact terminal-audit readback.
- [ ] Map procedure conflicts/epoch/authority/lock timeout, explicit rollback/cancellation, known pre-Commit failures, Commit ambiguity, and failed post-Commit verification without retry.
- [ ] Run focused real-SQL tests and the complete existing 40-case B02a inspect class; verify GREEN.
- [ ] Commit the guarded SQL client slice.

### Task 3: Terminal, rollback, executable, and six-kind race proof

**Files:**
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationOperatorDecisionTests.cs`
- Modify as narrowly required: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationResolutionTests.cs`
- Modify as narrowly required: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6SameKeyReplayTests.cs`
- Modify as narrowly required: `tests/MyDmsVn.WebOperation.CrashHost/*`

**Interfaces:**
- Consumes: Release operator executable and existing owned schema13/race/oracle fixtures.
- Produces: B6-OPS-03/04 executable and SQL proof without duplicating the full B04 matrix.

- [ ] Add RED tests for exactly one CANCELLED journal plus one exact immutable audit across kinds 1-6, duplicate/second decision, COMMITTED/EXPIRED/CANCELLED conflicts, actor/epoch/fingerprint/kind/target mismatch, audit failure/rollback, operator-first/writer-first contention, lock timeout/cancellation, and zero source/stock/ledger/valuation/COGS effect.
- [ ] Add an actual Release executable restricted SQL-login smoke covering dry-run, wrong confirmation (no SQL), execute, readback, over-grant denial, and secret-free output.
- [ ] Add the narrow process/Commit ambiguity proof possible with the existing CrashHost; if deterministic post-Commit ambiguity cannot be safely injected without production hooks, record it as BLOCKED rather than adding a weakening hook.
- [ ] Run each new selector RED, implement only necessary testability seams/fixes, then run GREEN.
- [ ] Run existing `WebOperationResolutionTests` and focused B04 operator-first/original-first same-key regressions sequentially.
- [ ] Verify process/testhost/operator/CrashHost drain and owned fixture cleanup; quarantine rather than drop on uncertain ownership/quiescence.
- [ ] Commit the integration/race slice.

### Task 4: Protected SQL workflow, operator documentation, and evidence

**Files:**
- Create: `.github/workflows/backend-web6-b02b-sql-candidate.yml`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md`
- Create: `docs/evidence/web6/20261103-b02b-operator-decision.md`
- Modify: `docs/evidence/web6/README.md`
- Modify only after actual evidence: `docs/plans/20261103-001-web6-recovery-quality-backend.md`, `docs/development/WEB6_BACKEND_ACCEPTANCE.md`, `docs/development/WEB6_CODEX_HANDOFF.md`

**Interfaces:**
- Consumes: exact test allowlist and real Release executable smoke from Task 3.
- Produces: Owner-tag/protected-environment workflow and sanitized candidate handoff.

- [ ] Add workflow checks for exact tag SHA, private owned LocalDB, restricted operator login, locked restore/build, nonempty exact allowlist, TRX reconciliation, zero retry/skip, process/session drain, ownership-verified removal, and sanitized artifacts.
- [ ] Document command help, confirmation generation/use, exit codes, dry-run limitations, known/unknown Commit handling, and independent inspect recovery.
- [ ] Run locked restore, Release/net48 builds, independent discovery, focused unit/SQL/executable/race/regression selectors, and collect TRX hashes/counts.
- [ ] Record exact base/HEAD/tested SHA, binary hash, toolchain/SQL/schema/epoch/principal, oracle, drain, cleanup/quarantine, findings, and honest local/protected-CI gate states.
- [ ] Mark B6-OPS-03/04 PASS only if every mandatory actual gate ran; otherwise retain BLOCKED/NOT RUN precisely.
- [ ] Commit documentation/evidence/workflow.

### Task 5: Independent review and delivery

**Files:**
- Modify only files required by confirmed review findings.

- [ ] Run a fresh independent whole-branch review against the Owner prompt and this plan.
- [ ] Fix every P0/P1 and confirmed in-scope P2 using RED→GREEN tests; rerun affected gates.
- [ ] Run final locked restore/build/tests and inspect TRX/counts, git diff/status, UTF-8/LF, secrets, process drain, and cleanup disposition.
- [ ] Push `codex/web6-b02-operator-decision` and create PR titled `feat(web6): implement guarded operator terminal decision CLI`; do not merge.
- [ ] Report commit/PR, changed files, gate counts, B6-OPS-03/04 state, exact-head CI state, SQL oracle/drain/cleanup, findings, B03 readiness, and retained resend/staging/production no-go states.
