# WEB5 B04-B — Exact B02 candidate SQL verification

**2026-11-02: LOCAL VERIFICATION PASS; hosted candidate NOT RUN.** Owner explicitly authorized B02 and B04-B while selecting completion of frontend WEB5-02/03. Backend base `3aed45a5116f8d45ce7bfe061dc7f996b125a05b`; [B02 evidence](20261102-b02-terminal-resolution.md), [candidate lane runbook](../../development/WEB5_SQL_CANDIDATE.md), [owning plan](../../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04).

The existing B04-A protected baseline and its RED/two-GREEN proofs remain unchanged. This task adds a separate exact-candidate lane, not a rerun of the frozen 103-case source. Runtime and tests are reviewed together; no source/merge, staging or production acceptance is inferred from this preparation.

## Reviewed controls

- Owner/repository/tag-gated workflow, exact control and test SHA, secret-free contents-read token and private SID/token-owned LocalDB.
- `web5-sql-candidate` environment created/read back with custom tag policy `web5-sql-candidate-*`; zero environment secrets.
- Mandatory new terminal/inspection/principal/crash/migration and existing HTTP/SQL owners; separate optional frontend bridge wrappers excluded, never counted as required skips.
- Actionlint PASS; shared sanitized discovery/TRX guard checks 14/14 PASS. Independent source review found the missing operator inspection; implementation and focused re-review resolved that P2. No remaining established P1/P2 at the source-review checkpoint.
- Final reviewed schema013 fingerprint: `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`. Actual owned-database measurement and teardown passed; measurement alone is not migration acceptance.

## Verification record

The final local source tree executed the candidate workflow's three SQL selections sequentially, using locked restore, Release/net48 builds, `dotnet test --list-tests`, actual execution with TRX and the shared discovery/identity guard. All guard summaries have `complete=true`; each row below has zero failed/required skipped cases.

| Owner | Discovered / executed / passed | Actual classes |
|---|---:|---|
| Security | 116 / 116 / 116 | Journal22, Read24, HTTP23, originalPrincipal16, Resolution24, ResolutionPrincipal5, TerminalCrash2 |
| Inventory | 19 / 19 / 19 | Original7 and all twelve terminal fence arrangements |
| Migrations | 32 / 32 / 32 | Original17 and new exact13 migration15 |
| Total SQL | **167 / 167 / 167** | Real SQL, independent physical oracles and owned teardown |

The HTTP owner includes the mandatory six-original-result/six-negative v2 case plus retained v1/crash regression. The twelve Inventory rows are actual Theory cases, not a count inferred from prose. Both operator OS-death cases execute before/after real Commit; connection rollback alone is not reported as process-death evidence.

SQL-free checks are separate: complete HttpHost548/548 and exact existing CI recovery-validator filter69/69 (recovery28, integrated14, receipt12, bridge12, crash-control3), zero failed/skipped. Affected locked restores/builds completed with zero warnings; final actionlint, evidence guard14 and diff checks passed.

Environment: Windows NT10.0.26300, .NET SDK8.0.427, Release/net48; designated nonproduction SQL engine14.0.2140.2 Developer Edition64-bit, model compatibility140. Fixtures were allocated/disposed by existing ownership-token helpers. Post-batch inspection observed zero remaining `MyDmsVn_Test_` databases, matching SQL sessions and CrashHost children. No connection strings or raw test output are published. Local evidence is not hosted B04-B acceptance.

### Failed development attempts retained

| Attempt | Executed / passed / failed / skipped | Diagnosis and correction |
|---|---:|---|
| First Security | 17 / 16 / 1 / 0 | Assertion expected KeyConflict; inspected original read contract correctly returned FingerprintMismatch. Test assertion corrected. |
| First Inventory | 12 / 4 / 8 / 0 | Empty-table FOR JSON oracle was queried as a required row before the race. Scalar SELECT wrapper corrected the test helper. |
| Second Inventory | 12 / 12 / 0 / 0 | Focused initial race proof; superseded by final schema/source verification. |
| Second Security | 28 / 26 / 2 / 0 | Provisioner's role DENY neutralized the intentionally excessive grants. Fixtures now remove that DENY before granting actual unsafe effective authority. |
| First final Inventory | 19 / 15 / 4 / 0 | New physical line-ID oracle omitted brackets around reserved `[LineNo]`. Two test query clauses corrected; all19 then passed. |

These failures produced no production-code fix or hidden retry/skip. Their raw logs/TRX remain private; all attempts completed owned fixture disposal without reported uncertain process cleanup. The missing operator-inspection P2 was a separate source review finding, implemented and re-reviewed before the final schema measurement and 167-case run.

### Hosted exact candidate

Pending fresh protected tag at the reviewed committed source, followed by job/artifact provenance and all-owner/cleanup verification. B02/B04-B cannot be accepted from local green alone. The PR must record final head and SQL-free CI separately from the protected SQL tag run.

Strict-only B5-COMP-03/04 are NOT_APPLICABLE under ADR0015; B5-MIG-01/02 apply. Full B03 CLI/backup rehearsal, B05 full IIS, frontend native browser and production acceptance remain separate. Browser same-key resend remains disabled.
