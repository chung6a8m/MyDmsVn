# WEB6 B02b evidence — guarded operator terminal decision candidate

**Date:** 2026-11-03 (Asia/Bangkok). **State:** IMPLEMENTED / LOCAL SQL-VERIFIED CANDIDATE; B6-OPS-03 and B6-OPS-04 **BLOCKED pending protected exact-HEAD SQL CI and Owner acceptance**. Manual resend remains OFF; staging remains NOT ACCEPTED; production remains NO-GO.

## Candidate and scope

- Base SHA: `01c3c429fa8b4a67c41635629d99e762b4955fce` (`origin/main`, merged B02a and B04 baseline).
- Locally tested runtime/test source SHA before documentation-only evidence commit: `685a0af015dfa002e31c9b08c65dd77359eb0073`.
- Implementation commits: `335bc2624733cb3faa7aa836b0e6c648563ff445`, `6d11848ca1c197a2f78f669597cae8246cc178f2`, `685a0af015dfa002e31c9b08c65dd77359eb0073`.
- Branch: `codex/web6-b02-operator-decision`.
- Scope: isolated `record-not-executed --dry-run|--execute` CLI only. No schema13, migration, grant, HTTP runtime/controller/DI, JWT/session, frontend/manual-resend or frozen corpus change.
- Toolchain: Windows/net48, .NET SDK `8.0.427`, VSTest `17.11.1`, Microsoft.Data.SqlClient `7.0.2`; SQL Server `14.0.2140.2` Developer Edition (64-bit), engine edition 3.
- Schema: exact13 / `web5`; expected fingerprint `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`. Each SQL case allocated a fresh owned database and a nonempty current epoch; raw database/epoch/login identities remain local.
- Release CLI SHA-256 at tested source: `eedc8bac9b3c0025aed5f8981cdff8f7ee15edf0fb24da5a51a5cf5b18050d7a`.

The command validates all input before SQL; requires exactly one mode; validates GUIDv4 operation/decision, kind/target, canonical positive actor, epoch, SHA-256 and schema13 metadata; and binds execute confirmation to command, environment, database, epoch, actor, original operation, kind/target, fingerprint, decision, case, reason and evidence. Dry-run performs only a guarded inspection and reports an explicit non-certificate preview. Execute re-inspects, opens one explicit transaction, calls existing `SqlWebOperationResolutionMaintenance.RecordNotExecutedAsync`, waits for known Commit, and reopens a restricted connection to verify matched CANCELLED/decision/case through `InspectWebOperationResolution`.

The ordinary operator role remains EXECUTE-only on the two existing procedures. No direct table read/DML or broad permission was added. Direct terminal-audit table readback was deliberately rejected because the restricted operator role does not own SELECT; schema13 inspection reports CANCELLED only when the immutable audit and journal fully match, while the same stored procedure transaction writes exact reason/evidence.

## RED → GREEN and local execution

Locked restore passed for the CLI and Security Tests graphs. Release/net48 CLI build passed with 0 warnings and 0 errors. Baseline B02a before edits was 40/40 PASS, zero failed/skipped.

| Gate / local artifact | Discovered | Executed | Pass | Fail | Skip | Result |
|---|---:|---:|---:|---:|---:|---|
| New decision class — `TestResults/web6-b02b-task3-final/task3-final.trx` | 41 | 41 | 41 | 0 | 0 | PASS in 34s; SHA-256 `b6dc74ef81255aa71ab3e1b994501bab85fa3f3a8601386314a20886adc97537` |
| Existing terminal resolution + OS process interruption — `TestResults/web6-b02b-task3-resolution-crash/resolution-crash.trx` | 26 | 26 | 26 | 0 | 0 | PASS in 1m46s; SHA-256 `864e1a5a59eed0d268ea02922cb7225c91e555d1e06590d1e3737fb42ad5c93b` |
| Exact B04 operator-first/original-first six-kind races — `TestResults/web6-b02b-task3-races/races-final.trx` | 2 | 2 | 2 | 0 | 0 | PASS in 2m08s; 12 command scenarios; SHA-256 `c53000e1bfdfbbccdcde291ecde0518fe4911b91d74a1887527c2da617738d6a` |
| B02a inspect regression during implementation — `TestResults/web6-b02b-task2/task2.trx` combined | 40 inspect + 38 then-current decision | 78 | 78 | 0 | 0 | PASS; existing inspect exit codes unchanged |

Independent `--list-tests` discovery at the tested source returned exactly 41 decision, 26 resolution/crash and 2 allowlisted race cases. One earlier development command used the source filename as a class filter and discovered zero; it was rejected as evidence and replaced with the source-verified `WebOperationHttpSqlTests` names. No retry is counted.

The new 41-case class covers strict parser/help/exit/output boundaries, exact confirmation field mutation, no-client-on-invalid input, dry-run byte-equal SQL snapshots, existing disposition and wrong principal/target denial, six kinds, duplicate decision rejection, lock timeout, cancellation after a test-only trigger independently observes the write transaction, and an actual Release EXE with a generated restricted SQL login. The executable smoke proves preview exit 7, wrong confirmation exit 2 without SQL change, execute exit 0 only after matched readback, inherited `db_datareader` denial, secret-free output, session drain and exact SID-owned login removal.

The existing 26-case regression covers wrong actor/epoch/fingerprint/grant, COMMITTED/EXPIRED/CANCELLED conflicts, audit failure, explicit caller rollback, immutable audit, runtime denial, and real operator OS-process death before/after Commit with independent SQL read after process/session drain. The two focused B04 tests cover operator-first and writer-first lock contention for Sales Create/Update/Post and Goods Receipt Create/Update/Post.

## Independent SQL oracle and lifecycle

- Dry-run before/after snapshots are byte-equal across `WebOperationJournal`, `WebOperationTerminalAudit`, Sales/Goods Receipt headers and lines, `StockBalances`, `StockLedger` and `StockValuationEntries`.
- Successful execute creates exactly one CANCELLED journal and one matched immutable terminal audit, with no Sales/Goods Receipt/stock/ledger/valuation/COGS business effect.
- Six-kind execution produced 6/6 terminal pairs, rejected every second decision and left all business/stock tables unchanged.
- Audit failure, explicit rollback, wrong authority/identity, timeout and cancellation leave no partial terminal pair.
- Operator-first blocks all delayed writers; writer-first COMMITTED remains authoritative and the operator cannot create a false negative.
- Every successful local fresh fixture completed normal owned cleanup. Post-run process census found zero matching `testhost`, CrashHost, Operator, DbMigrator or IIS Express processes. No new B02b quarantine was reported or created; historical WEB5/B04 quarantines were not queried or changed.

## Protected SQL CI handoff

`.github/workflows/backend-web6-b02b-sql-candidate.yml` is an Owner-tag-triggered `windows-2022` workflow for `web6-b02b-sql-candidate-*` using the existing protected `web5-sql-candidate` environment and private SID-owned LocalDB lifecycle. It pins control and candidate to the tag SHA, requires SQL-login capability, locked restore/Release net48 build, exact 41 decision + 40 inherited inspect + 26 resolution/crash + 2 race discovery/TRX counts, actual Release CLI smoke, zero fail/skip/retry, unchanged source, verified process/session/database drain and ownership-proven instance removal. Only sanitized JSON counts/capabilities/lifecycle metadata are uploaded.

**Hosted protected CI: NOT RUN / BLOCKED.** No tag or protected-environment authorization was created by this branch. Local execution is not substituted for this gate.

## Acceptance and findings

| Gate | State | Evidence / blocker |
|---|---|---|
| B6-OPS-03 | **BLOCKED** | Local dry-run/confirmation/known-Commit/reinspection/oracle PASS. Protected exact-HEAD CI NOT RUN. CLI maps Commit/readback uncertainty to UNKNOWN, but a deterministic actual-CLI provider Commit-failure injection was not added because no safe seam exists without a production test hook; existing real OS process before/after-Commit proof passed. |
| B6-OPS-04 | **BLOCKED** | Local authority, duplicate/conflict, rollback/audit failure, lock/cancel, actual EXE, six-kind operator-first/writer-first and independent oracle PASS. Protected exact-HEAD CI NOT RUN / Owner acceptance pending. |

Independent whole-branch review found P0 `0`, P1 `0`, P2 `2`; both confirmable P2 findings were fixed before PR by moving cancellation proof into the write transaction and adding the exact 40-case B02a inspect suite to protected CI. One bounded verification gap remains: actual CLI provider Commit-ambiguity fault injection is unperformed; UNKNOWN output behavior is unit-tested and existing operator CrashHost before/after Commit is real-process verified.

B03 is **not ready for acceptance or execution as a consequence of this candidate alone**. B03 remains separately scoped, requires an approved maintenance identity for `CompactAsync` database CONTROL or a separately reviewed narrow migration, and depends on B02b protected acceptance. Manual resend remains OFF, staging NOT ACCEPTED and production NO-GO.
