# WEB6 B02b evidence — guarded operator terminal decision candidate

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / LOCAL SQL-VERIFIED CANDIDATE; B6-OPS-03 and B6-OPS-04 **BLOCKED pending protected exact-HEAD SQL CI and Owner acceptance**. Manual resend remains OFF; staging remains NOT ACCEPTED; production remains NO-GO.

## Candidate and scope

- Base SHA: `01c3c429fa8b4a67c41635629d99e762b4955fce` (`origin/main`, merged B02a and B04 baseline).
- Locally tested runtime/test source SHA before the review-fix documentation commit: `e07f3fd5f08107c1f5590cd000b95eb9c94fc7e0`.
- Review-fix commits: `397f7fb550f1c7c06a609bba42f5ae5a2d20311d`, `e07f3fd5f08107c1f5590cd000b95eb9c94fc7e0`; earlier B02b implementation commits remain in PR history.
- Branch: `codex/web6-b02-operator-decision`.
- Scope: isolated `record-not-executed --dry-run|--execute` CLI only. No schema13, migration, grant, HTTP runtime/controller/DI, JWT/session, frontend/manual-resend or frozen corpus change.
- Toolchain: Windows/net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`; SQL Server `14.0.2140.2` Developer Edition (64-bit), engine edition 3.
- Schema: exact13 / `web5`; expected fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`. Each SQL case allocated a fresh owned database and a nonempty current epoch; raw database/epoch/login identities remain local.
- Release CLI SHA-256 at tested source: `78f65f274c92e2e5782b155ab632519b6ab437f236a466d6ac34e6b669650f43`.

The command validates all input before SQL; requires exactly one mode; validates GUIDv4 operation/decision, kind/target, canonical positive actor, epoch, SHA-256 and schema13 metadata; and binds execute confirmation to command, environment, database, epoch, actor, original operation, kind/target, fingerprint, decision, case, reason and evidence. Dry-run performs only a guarded inspection and reports an explicit non-certificate preview. Execute re-inspects, opens one explicit transaction, calls existing `SqlWebOperationResolutionMaintenance.RecordNotExecutedAsync`, waits for known Commit, and reopens a restricted connection to verify matched CANCELLED/decision/case through `InspectWebOperationResolution`.

The ordinary operator role remains EXECUTE-only on the two existing procedures. No direct table read/DML or broad permission was added. Direct terminal-audit table readback was deliberately rejected because the restricted operator role does not own SELECT; schema13 inspection reports CANCELLED only when the immutable audit and journal fully match, while the same stored procedure transaction writes exact reason/evidence.

## RED → GREEN and local execution

Locked restore passed for the CLI and Security Tests graphs. Release/net48 CLI build passed with 0 warnings and 0 errors. Baseline B02a before edits was 40/40 PASS, zero failed/skipped.

| Gate / local artifact | Discovered | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---:|---|
| B02b decision class — `TestResults/web6-b02b-pr102-review-fixes/decision.trx` | 50 | 50 | 50 | 0 | 0 | PASS in 35s; SHA-256 `677078f5825b559cdeca7f1cdc4ee17c85de32f4407bcbbc7f1af38801f9e731` |
| Existing B02a inspect regression — `TestResults/web6-b02b-pr102-review-fixes/inspect.trx` | 40 | 40 | 40 | 0 | 0 | PASS in 33s; SHA-256 `5598a292acb92da3c1377813199d97f8a1f00f41d6336373acb5e4abf15bb696`; inspect exit codes unchanged |
| Existing terminal resolution + OS process interruption — `TestResults/web6-b02b-pr102-review-fixes/resolution.trx` | 26 | 26 | 26 | 0 | 0 | PASS in 1m43s; SHA-256 `230d564c1f5337841e58d1e7780569a44e81d7ce4f9ac53a9ca5f9b2c75ed8ca` |
| Exact B04 operator-first/original-first six-kind races — `TestResults/web6-b02b-pr102-review-fixes/races.trx` | 2 | 2 | 2 | 0 | 0 | PASS in 2m05s; 12 command scenarios; SHA-256 `b5e53be53d798d3a8a702cb5ece9fad93d7e11dae5cfc8c9d3d03b5bf65a9934` |

Independent `--list-tests` discovery at the tested source returned exactly 50 decision, 40 inherited inspect, 26 resolution/crash and 2 allowlisted race cases. One earlier development command used the source filename as a class filter and discovered zero; it was rejected as evidence and replaced with the source-verified `WebOperationHttpSqlTests` names. No retry is counted.

The 50-case class covers strict parser/help/exit/output boundaries, distinct operation/decision identity, exact confirmation field mutation, no-client-on-invalid input, dry-run byte-equal SQL snapshots, existing disposition and wrong principal/target denial, six kinds, duplicate decision rejection, lock timeout, cancellation after a test-only trigger independently observes the write transaction, controlled Commit/Close/Dispose/readback fault injection, no re-execute after UNKNOWN, and an actual Release EXE with a generated restricted SQL login. The executable smoke proves preview exit 7, wrong confirmation exit 2 without SQL change, execute exit 0 only after matched readback, inherited `db_datareader` denial, secret-free output, session drain and exact SID-owned login removal.

## PR #102 review findings fix

- **P2-01 root cause:** nested `using` cleanup and an explicit `connection.Close()` ran outside the Commit certainty decision, so cleanup could replace a Commit exception with `EnvironmentFailure`. **Fix:** `OperatorDecisionExecution` now separates preflight, pre-Commit mutation, Commit outcome, post-Commit verification and cleanup. `OperatorDecisionCommitBoundary` attempts rollback/close/dispose in phase-appropriate order; once Commit starts, cleanup failures cannot escape or replace `UNKNOWN`, and a known Commit still requires exact fresh readback before `Completed`. Eight focused tests cover Commit throw with successful/throwing Close/Dispose, post-Commit readback failure/success, preflight failure, cancellation before Commit and single execution after UNKNOWN. The initial RED build failed because the required lifecycle API did not exist; GREEN is included in the 50/50 TRX.
- **P2-02 root cause:** the CLI constructed `WebOperationContext` with audit `DecisionId` in the unrelated `SessionId` position because maintenance exposed only the HTTP-oriented context. **Fix:** immutable `WebOperationTerminalIdentity` carries only original OperationId/epoch/kind/target/fingerprint; a compatible maintenance overload delegates existing callers, while the CLI uses the session-free overload. Both parser and maintenance reject DecisionId equal to OperationId. The parser regression first failed because equal IDs were accepted, then passed after the boundary validation. The real SQL oracle verifies the exact original operation/epoch/actor/kind/target/fingerprint and the separate audit DecisionId in one matched CANCELLED journal/audit pair.
- No schema, procedure parameter, grant, role, HTTP runtime/DI, dependency, retry or business behavior changed. Actual provider-level Commit fault injection remains unavailable without a provider hook; the controlled lifecycle seam proves the state machine, while the existing real CrashHost proof continues to cover OS death before/after Commit.

The existing 26-case regression covers wrong actor/epoch/fingerprint/grant, COMMITTED/EXPIRED/CANCELLED conflicts, audit failure, explicit caller rollback, immutable audit, runtime denial, and real operator OS-process death before/after Commit with independent SQL read after process/session drain. The two focused B04 tests cover operator-first and writer-first lock contention for Sales Create/Update/Post and Goods Receipt Create/Update/Post.

## Independent SQL oracle and lifecycle

- Dry-run before/after snapshots are byte-equal across `WebOperationJournal`, `WebOperationTerminalAudit`, Sales/Goods Receipt headers and lines, `StockBalances`, `StockLedger` and `StockValuationEntries`.
- Successful execute creates exactly one CANCELLED journal and one matched immutable terminal audit, with no Sales/Goods Receipt/stock/ledger/valuation/COGS business effect.
- Six-kind execution produced 6/6 terminal pairs, rejected every second decision and left all business/stock tables unchanged.
- Audit failure, explicit rollback, wrong authority/identity, timeout and cancellation leave no partial terminal pair.
- Operator-first blocks all delayed writers; writer-first COMMITTED remains authoritative and the operator cannot create a false negative.
- Every successful local fresh fixture completed normal owned cleanup. Post-run process census found zero matching `testhost`, CrashHost, Operator, DbMigrator or IIS Express processes. No new B02b quarantine was reported or created; historical WEB5/B04 quarantines were not queried or changed.

## Protected SQL CI handoff

`.github/workflows/backend-web6-b02b-sql-candidate.yml` is an Owner-tag-triggered `windows-2022` workflow for `web6-b02b-sql-candidate-*` using the existing protected `web5-sql-candidate` environment and private SID-owned LocalDB lifecycle. It pins control and candidate to the tag SHA, requires SQL-login capability, locked restore/Release net48 build, exact 50 decision + 40 inherited inspect + 26 resolution/crash + 2 race discovery/TRX counts, actual Release CLI smoke, zero fail/skip/retry, unchanged source, verified process/session/database drain and ownership-proven instance removal. Only sanitized JSON counts/capabilities/lifecycle metadata are uploaded.

**Hosted protected CI: NOT RUN / BLOCKED.** No tag or protected-environment authorization was created by this branch. Local execution is not substituted for this gate.

## Acceptance and findings

| Gate | State | Evidence / blocker |
|---|---|---|
| B6-OPS-03 | **BLOCKED** | Local dry-run/confirmation/known-Commit/reinspection/oracle and controlled lifecycle fault injection PASS. Protected exact-HEAD CI NOT RUN. Actual provider-level Commit-failure injection remains unavailable without a provider hook; existing real OS process before/after-Commit proof passed. |
| B6-OPS-04 | **BLOCKED** | Local authority, duplicate/conflict, rollback/audit failure, lock/cancel, actual EXE, six-kind operator-first/writer-first and independent oracle PASS. Protected exact-HEAD CI NOT RUN / Owner acceptance pending. |

The pre-PR independent whole-branch review found P0 `0`, P1 `0`, P2 `2`; both were fixed before opening PR #102. The PR review then found P2-01/P2-02 described above; both are fixed in the recorded review-fix source. One bounded verification gap remains: actual provider-level Commit-ambiguity injection is unperformed; the internal state machine is fault-injected and existing operator CrashHost before/after Commit is real-process verified. A fresh final review of the complete PR diff is required before the review-fix handoff is closed.

B03 is **not ready for acceptance or execution as a consequence of this candidate alone**. B03 remains separately scoped, requires an approved maintenance identity for `CompactAsync` database CONTROL or a separately reviewed narrow migration, and depends on B02b protected acceptance. Manual resend remains OFF, staging NOT ACCEPTED and production NO-GO.
