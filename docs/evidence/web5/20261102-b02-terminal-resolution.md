# WEB5 B02 — Terminal resolution implementation

**2026-11-02: DONE / VERIFIED NONPRODUCTION (bounded).** Owner explicitly selected B02 and B04-B to complete frontend F02/F03. Base backend `3aed45a5116f8d45ce7bfe061dc7f996b125a05b`; verified source candidate `2d72700294227ef69f1a7a61a2969a6bfe2c58be`, branch `codex/web5-b02-terminal-resolution`, [PR #91](https://github.com/chung6a8m/MyDmsVn/pull/91). Production and same-key browser resend remain NOT APPROVED. Final-head CI and merge disposition are recorded separately in the PR; the source proof below does not claim a future documentation commit has already run.

This bounded implementation follows [owning plan B02](../../plans/20261102-001-web5-operational-safety-backend.md#3-b02--authoritative-terminal-negative-operator-resolution-web5-02) and [ADR0015](../../adr/0015-web5-additive-recovery-boundary.md). Existing B01 and B04-A evidence permitted implementation; [B04-B](20261102-b04b-candidate-sql.md#hosted-exact-candidate) has now verified the exact combined source candidate, completing the bounded nonproduction gate.

## Implemented boundary

- Append-only `013_WebOperationTerminalResolution.sql` creates an immutable operator audit, a matched-audit INSERT guard on CANCELLED and separate operator-only recording/inspection procedures. Recording takes the same transaction-owned epoch then actor/key fence as original writers. Any existing disposition blocks a new decision; audit and tombstone commit atomically. An absent row never independently grants NOT_EXECUTED.
- `SqlWebOperationResolutionMaintenance` is explicitly constructed by operator code, never registered in runtime DI. `InspectAsync` returns bounded exact-key metadata/retention hints; absent or unmatched records remain INDETERMINATE. The execute-only operator role gets no broad table access. Runtime can read audit metadata but is denied audit DML and operator execution; direct runtime CANCELLED cannot bypass the matched-audit trigger.
- `web5` and `--web5` admit exact13 explicitly. Existing Desktop exact10, legacy-web11 exact11 and web4 exact12 remain separate. Exact12/11/10 upgrades require source verification and drained maintenance admission; old server modes refuse schema13. Migration012 is unchanged; database epoch is preserved during code/schema upgrade.
- Separate authenticated `GET /api/v2/operations/context` and `GET /api/v2/operations/{id}/resolution` preserve all v1 envelopes and outcomes. Reads revalidate current actor/session, epoch/fingerprint and permission derived from the stored original kind. Read denial alone does not deny original Write/Post readback. NOT_EXECUTED requires the exact committed audit/journal pair; expired/unavailable/absent/mismatched states never supply it.
- The separate [resolution corpus](../../contracts/web5-resolution-v1/README.md) contains 40 manifest-owned files. Manifest SHA256: `fef5f1f18d3a556bf35d5c9ed6bc38af3eeb0c1a89dd5f9d125d7fb190a133b2`. Frozen 48-blob WEB4 tree remains `fcab4a1e7505b150a132ce2899002930e9f2a6ee`.
- B04-B adds applicable Security/Inventory/Migrations proof, twelve real command/operator fence arrangements, physical SQL oracles, actual before/after-Commit operator process death, migration/privilege negatives and a protected exact-candidate lane. See [B04-B evidence](20261102-b04b-candidate-sql.md) and [runbook](../../development/WEB5_SQL_CANDIDATE.md).

## Review and schema measurement

Independent review found one P2 scope omission: current-user v2 recovery was not the separate read-only operator inspection required by B02. The new operator inspection closes that omission. Focused re-review found no remaining established P1/P2 in the production boundary or candidate workflow. Runtime and operator permissions are independently exercised; a source review is not SQL acceptance.

Final schema013 fingerprint is `D0A86EA0CACAB3D68F1188A1166227DFA5DE452572D1719CD55A3FA3C16F02C9`. It was measured from actual fresh13 DDL on an existing-helper-owned disposable database, with a separate connection reading `SqlSchemaDefinitionFingerprint`. The temporary diagnostic deliberately expected post-verification to reject the previous fingerprint, then measured the definition; one diagnostic case passed and owned disposal completed. The probe was removed. This is not a substitute for fresh/upgrade/admission migration tests. An earlier pre-inspection measurement was superseded, never promoted as the final schema pin.

## Verification

| Checkpoint | Actual result |
|---|---|
| Mode RED | 3 executed: 2 PASS, 1 expected failure for previously unsupported web5 mode, zero skipped |
| Endpoint RED | 7 executed: 2 PASS, 5 expected failures while the separate controller was absent, zero skipped |
| Focused mode/endpoint GREEN | 24/24 PASS, zero skipped |
| Final complete HTTP/wire/corpus checks | 548/548 PASS, zero failed/skipped |
| Exact existing CI SQL-free recovery validator filter | 69/69 PASS: recovery28, integrated14, receipt12, bridge12, crash-control3; zero failed/skipped |
| Final applicable local SQL owners | 167/167 PASS: Security116, Inventory19, Migrations32; zero failed/skipped; reconciled discovery/TRX |
| Hosted exact-candidate B04-B | 167/167 PASS: Security116, Inventory19, Migrations32; zero failed/skipped, all discovery/TRX guards complete; [run 41432523016](https://github.com/chung6a8m/MyDmsVn/actions/runs/41432523016) |
| Existing hosted SQL-free CI on source candidate | [PR run 41432529946](https://github.com/chung6a8m/MyDmsVn/actions/runs/41432529946) and [push run 41432518701](https://github.com/chung6a8m/MyDmsVn/actions/runs/41432518701) SUCCESS |
| Actual frontend-port local integration | 1/1 .NET opt-in wrapper and 1/1 child Vitest PASS; separate from hosted SQL and native browser evidence |

Local toolchain: Windows NT10.0.26300, SDK8.0.427 selected by global.json, Release/net48 with locked restore; affected builds had zero warnings. Actual designated SQL engine14.0.2140.2 Developer Edition64-bit, model compatibility140. All fixtures used the existing owned disposable-database helper. Post-batch inspection found zero remaining test databases, matching SQL sessions or CrashHost children. Raw logs/TRX and fixture connection data remain outside Git; task evidence publishes sanitized counts, hashes and ownership only.

Protected hosted proof used the exact source SHA for both control and tests, trusted Owner tag `web5-sql-candidate-20261102-2d72700-1`, run `41432523016` attempt1/job `125234350638`. SQL15.0.4382.1 Express64-bit, compatibility140, image `20261026.347.1`; sanitized artifact `13135152598` independently matched SHA256 `b0aed684d532047b2c19385adf5c03a0d73b443995193d3187bbdb98864f9598`. Final lifecycle was `Removed`, ownership verified, zero user databases/active owners and no quarantine. [B04-B evidence](20261102-b04b-candidate-sql.md#hosted-exact-candidate) owns the full provenance/count record.

The separately executed local bridge used backend `2d72700294227ef69f1a7a61a2969a6bfe2c58be` and frontend source now fixed at `08d437bf6f5b6d4e98a517243a33d241ca5fb759` (source unchanged after execution), consuming the actual frontend port at `b017da6`. The .NET wrapper and child Vitest each passed one case: six original COMMITTED results plus six audited NOT_EXECUTED keys, twelve keys each dispatched once, six operator decisions, eighteen v2 reads, two ordinary Get403 responses, one TestServer restart and an independent physical SQL oracle. Owned fixture disposal passed. This is actual frontend-port/TestServer/SQL integration, not native browser SQL or IIS evidence; paired frontend final-candidate acceptance remains pending in its owning plan.

## Acceptance and remaining scope

B02 is DONE / VERIFIED NONPRODUCTION (bounded) at the recorded source candidate after exact-candidate hosted B04-B PASS. B04 A+B is complete within the same bounded verification level. Final-head CI and merge disposition remain separately recorded in PR #91. B5-COMP-03/04 strict-only cases are NOT_APPLICABLE under ADR0015, not skipped required tests. B5-MIG-01/02 are applicable and passed because schema013 is implemented. B01 and B04-A historical proofs remain immutable prerequisites; this task does not replace their source pins or results.

Full B03 operator CLI, positive COMMITTED-result-unavailable support-case closure, retention/backup rehearsal, B05/F04 full IIS and attended UI, and B06/F05 production decisions remain separate. The maintenance API is not an attended CLI rehearsal. Browser same-key resend remains disabled and NOT APPROVED. No production deployment or archived WEB1–04/CEX work is selected.
