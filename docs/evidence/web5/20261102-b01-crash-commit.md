# WEB5 B01 — Real process-crash and Commit proof

**2026-11-02 — IMPLEMENTED / VERIFIED NONPRODUCTION (bounded). Owner acceptance and merge pending.** This is test-only nonproduction evidence. B02–B06 remain PLANNED / NOT RUN. No product resend, terminal negative, full IIS, browser acceptance or production release is enabled.

## Candidate and environment

- Backend base: `fc36c879fdd2d46ee77d136bdb6f9e70f7191b64`, freshly verified `main`; isolated branch `codex/web5-b01-crash-proof`.
- Core harness/SQL source commit: `e91c6826e22f6ea7ae5d876cbc4908649a7f14ea`. Follow-up SQL-free assertion correction: `e9477ab791cf5f6ff337c917106d663e5f384d10`; supervisor, child, SQL fixtures and production source are unchanged. The matching 64-case CI filter was rerun after this correction. Later documentation-only commits do not change these tested sources.
- Windows build `26300.9711`, Framework runtime release `534296`, SDK `8.0.427`, Release/net48; SQL Server Developer Edition (64-bit) `14.0.2140.2`.
- The explicitly configured `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` was validated as effective `master`, with no attachment/user-instance options. Its value and credentials were never published. The shared helper created fresh disposable schema12 databases, with actual instance/database ID/creation time/random ownership-token validation on teardown and `Pooling=false`.
- The child uses the actual OWIN `Web2Pipeline`, Application handlers and SQL repositories. Runtime calls execute as provisioned restricted `WebHttpProbe`; the separate administrative connection only arranges and independently observes test state. This is a test-process HTTP pipeline, not full IIS or a network/TLS deployment.
- Child Release EXE SHA256: `a94083765c86cac544925e401b10bea8ab681595a0af3f78d442b920526b9d89`; the copy launched from Security.Tests has the same hash.
- Production `src/`, existing dependency pins, migrations and the 48 frozen `web4-v1` blobs are unchanged. Corpus tree: `fcab4a1e7505b150a132ce2899002930e9f2a6ee`, preserving canonical source `1c603c6ac57e59a3f7b4bc6ca976b2d531c079aa`. Desktop exact10, legacy-web11/exact11 and web4/exact12 remain separate; CEX is unchanged.

## Implementation and observed scope

[Source/lock map and threat boundaries](../../design/WEB5_B01_CRASH_PROOF.md) owns the six-handler transaction diagram. All barriers live in the deliberately test-built console child. A barrier requires successful production journal `CompleteAsync` on the same UoW and exact key/epoch/kind/target. A read or replay cannot masquerade as a mutation barrier.

The supervisor uses the existing suspended-root Windows Job Object launcher, kills only that owned tree and verifies process/output drain. The current-user-only pipe verifies the kernel client PID against the launched root before sending any configuration. Frames are capped during streaming; diagnostics exclude connection strings, tokens and raw request data. The launcher change only adds an optional PID callback; its existing regression cases were rerun.

Fresh independent SQL observation follows process drain and bounded SQL quiescence plus the exact epoch/actor/key transaction-owned fence. Final teardown requires no other owned database user sessions/requests/transactions, successful HTTP disposal and allocation ownership revalidation. Failed cleanup/quiescence/fence proof permanently poisons/quarantines the allocation. An ordinary assertion/read-only oracle error still fails the test; only independently successful final cleanup proof permits deletion. No prefix-only cleanup, shared worker kill or pool clearing is used.

| Gate | Executable evidence and boundary | Result |
|---|---|---|
| B5-CR-01 | `External_process_death_before_commit_rolls_back_all_six_commands`; exact persisted source/journal/ledger/valuation/balance snapshots, excluding nontransactional sequence/identity allocation | PASS (bounded) |
| B5-CR-02 | `External_process_death_racing_commit_classifies_all_six_from_fresh_SQL`; commit-ready/release/commit-enter race, fresh SQL and protected restart status | PASS (bounded) |
| B5-CR-03 | `External_process_death_after_commit_recovers_all_six_original_results`; original IDs, ordered lines, versions, Sales Post version / Receipt Post no version, Read-denied original-grant recovery and explicit same-key replay | PASS (bounded) |
| B5-CR-04 | Two `Independent_same_key_worker_*` facts × six operations; two real child processes, SQL DMV-observed APPLICATION-lock wait, original release versus external kill, exactly one effect | PASS (bounded) |
| B5-CR-05/06 | Full existing `WebOperationHttpSqlTests`, `WebOperationJournalTests`, `WebOperationReadTests`; changed body/route/target/version/fingerprint, foreign actor, stale epoch, current grant/session revocation, expired/unavailable tombstone and malformed headers | PASS (bounded) |
| B5-CR-07 | Each crash fixture verifies process/SQL drain and ownership; two SQL-free control cases plus 19 existing owned-process lifecycle cases | PASS (bounded) |

The five crash/concurrency facts contain 30 distinct fresh-database arrangements: six operations × three crash phases, plus six × two competing-worker orders. Each includes a second warehouse holding the same product at quantity 7, value 35 and unit cost 5; its exact persisted state is unchanged. Target-warehouse Receipt Post yields quantity 10/value 120; Sales Post yields quantity 4/value 48 and line costs 36/24/12. Independent source-to-ledger-to-valuation joins and original actor columns are asserted.

Final observed phase/classification and cleanup counts: 18 crash observations: six precommit ROLLED_BACK, six commit-entry-race COMMITTED, six postcommit COMMITTED. Twelve DMV-observed fence arrangements (six original released, six original killed) each produced one committed effect. All 30 cleanup records confirmed SQL quiescence/fence and owned database removal; zero quarantines and zero duplicate writes. The Commit-entry race produced no rolled-back outcome in this run..

## Commands and actual results

All builds used locked restore before `--no-restore`/`--no-build`; SQL owners ran sequentially. Raw TRX/logs remain ignored local artifacts under `TestResults/web5-b01/`, outside Git. Counts below are actual executed test rows, not scenario IDs or discovered-only totals.

| Owner / selection | TRX | Passed / failed / skipped |
|---|---|---|
| Full `Server.HttpHost.Tests` | `http-host.trx` | 506 / 0 / 0 |
| Full `Server.Application.Tests` | `application.trx` | 359 / 0 / 0 |
| Desktop `FullyQualifiedName~GuiOwnedProcessTests&FullyQualifiedName!~Packaged_lookup` | `owned-process-regression.trx` | 19 / 0 / 0 |
| Security `FullyQualifiedName~WebOperation&FullyQualifiedName!~Frontend_ports_recover` | `security-weboperation-final.trx` | 70 / 0 / 0 |
| Inventory `FullyQualifiedName~WebOperationCommandTests` | `inventory-weboperation.trx` | 7 / 0 / 0 |
| Existing CI recovery validator filter plus `WebOperationCrashProcessTests` | `recovery-fixture-ci-fix.trx` | 64 / 0 / 0 |

Direct execution pattern, substituting the source-verified project/filter and TRX name from the table:

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebOperation&FullyQualifiedName!~Frontend_ports_recover' --logger 'trx;LogFileName=security-weboperation-final.trx' --results-directory TestResults/web5-b01
```

The optional frontend-port bridge is explicitly outside this B01 selection; no skipped bridge is counted as a pass. The existing hosted SQL-free CI filter and per-class presence guard now include both control-process cases. This is not B04-A protected SQL CI. Published exact-head CI results are linked in [PR #88](https://github.com/chung6a8m/MyDmsVn/pull/88); the local matching 64-case filter and required-class guard passed. Hosted SQL-free results do not establish B04-A SQL acceptance.

## Failures, fixes and independent review

- Initial control run: 1 passed / 1 failed. `StreamWriter.AutoFlush` flushed an unconnected pipe during construction. Moved activation after verified connection; tightened the malformed-control assertion so an unrelated startup exception cannot satisfy it. Re-run: 2/2 passed.
- Initial SQL run: 0/5 facts passed. The independent SQL probe confirmed scalar `FOR JSON PATH` returns SQL NULL for the empty fixture result; the oracle now explicitly coalesces that empty result to `[]`. Child responses now transport actual HTTP headers so existing no-cache assertions run unchanged. Focused after-Commit rerun: 1 fact / all six operations passed before final quiescence additions.
- Desktop helper locked restore initially refused missing pinned nested SourceGrid. Initialized existing submodules at WinFormsKit `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf` and SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`; locked restore and all 19 selected helper cases then passed. No dependency pin was changed.
- Independent source reviews identified and resolved pipe-peer identity, bounded IPC retention, selected-UoW barrier binding, SQL quiescence/quarantine and required CI-class discovery. Final independent review found no remaining P0/P1/P2 implementation findings; it was source-only, separate from actual runtime evidence.
- Initial CI at `9d96be7`: [push run](https://github.com/chung6a8m/MyDmsVn/actions/runs/41398390649) passed 506 HTTP + 64 fixture tests; [PR run](https://github.com/chung6a8m/MyDmsVn/actions/runs/41398432866) passed HTTP but failed 1/64 fixture cases. Malformed-control rejection may be direct `InvalidDataException` when supervisor cancellation drains first, or `AggregateException` with the exact known child-exit failure when natural exit 2 wins. The assertion incorrectly required only the latter ordering. Commit `e9477ab` accepts exactly those two forms while retaining protocol-origin, redaction and no-poison checks; all 64 affected cases passed locally and independent review found no new P0/P1/P2 issue. No SQL/child/supervisor behavior changed, so previously completed SQL runs remain applicable.
- Resource baseline was zero matching disposable test databases. After failed development trials it was again zero. Final owned database/process count: zero disposable test databases and zero CrashHost processes; all 30 final crash allocations removed after verified cleanup.

## Limits and next dependency

`commit-enter` proves a kill race adjacent to the real synchronous Commit invocation, not that SQL was deterministically interrupted inside COMMIT. Either durable outcome is acceptable only after physical classification. The observed distribution is evidence for this run, not a guarantee of the next crash. Precommit rollback does not rewind SQL sequence/identity counters.

The durable actor oracle uses CreatedBy/UpdatedBy/PostedBy columns. Successful Post external audit delivery occurs after Commit and can be absent when the process dies there; this test does not make that audit channel transactional.

B01 is a bounded nonproduction proof, with Owner acceptance/merge pending. Same-key product resend stays disabled and there is no negative unlock. B04-A still needs actual protected hosted SQL PASS before B02 executable work; local B01 SQL does not satisfy it. Full IIS, browser/IME/Narrator, operator rehearsal, B02 protocol, B03–B06 and production remain separately pending. No archived WEB1–04 or CEX scope is reopened.
