# WEB5 B04-B exact-candidate SQL lane

This lane verifies the combined B02 production code, schema013, tests and runner at one reviewed commit. The [owning plan](../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04) and [candidate evidence](../evidence/web5/20261102-b04b-candidate-sql.md) own status. The [B04-A baseline runbook](WEB5_SQL_CI_BASELINE.md) retains its immutable source and original RED/two-GREEN proof.

## Admission and resource ownership

`.github/workflows/backend-web-sql-candidate.yml` accepts Owner tag pushes `web5-sql-candidate-*` only, with repository ID `1409766698`, actor and triggering actor `chung6a8m`. The `web5-sql-candidate` environment has custom deployment policy for that tag pattern only and no secrets. Both control and test checkouts use the exact `github.sha`, with persisted credentials disabled. The job has contents-read permission and no PR/fork trigger, arbitrary source input or downloaded executable artifact.

The shared SID/token-owned private LocalDB lifecycle and sanitized TRX/discovery guard remain the B04-A mechanisms. `TestSourceSha` now additionally admits the exact control SHA; its default remains the original immutable baseline. The candidate evidence requires both `controlSha` and `testSourceSha` to match the tag commit. No remote/shared SQL endpoint or application database is selected.

Keep owner execution sequential. Security has a 20-minute step bound and the complete job a 45-minute bound for the expanded retained crash/HTTP plus terminal matrix; each test command retains the 90-second hang watchdog and cleanup retains its two-minute outer bound. A bound does not establish rollback. Interrupted owners, uncertain process/SQL drain or missing evidence retain quarantine and fail the job. Never retarget an evidence tag, adopt a leftover database, bulk-delete by prefix or force-stop an unverified instance.

## Applicable owners

| Owner | Required classes |
|---|---|
| Security | `WebOperationJournalTests`, `WebOperationReadTests`, `WebOperationHttpSqlTests`, `WebPrincipalProvisioningTests`, `WebOperationResolutionTests`, `WebOperationResolutionPrincipalTests`, `WebOperationTerminalCrashTests` |
| Inventory | `WebOperationCommandTests`, `WebOperationTerminalCommandTests` |
| Migrations | `WebAuthMigrationTests`, `WebOperationResolutionMigrationTests` |

The two opt-in `Frontend_ports_*` wrappers are excluded from this lane and run separately against an explicitly selected frontend checkout. The mandatory WEB5 HTTP case remains included. The Inventory terminal owner must execute all twelve actual Theory rows, covering six commands in both fence orders. Reconcile discovered names and actual TRX identities/outcomes; all required classes must execute with zero failures or required skips. Source-estimated counts never replace those observations.

ADR0015 selects additive compatibility: strict-only B5-COMP-03/04 are NOT_APPLICABLE, not skipped tests. Schema013 is selected, so B5-MIG-01/02 are mandatory. Frozen WEB4 corpus tree `fcab4a1e7505b150a132ce2899002930e9f2a6ee` must remain unchanged.

## Execution and evidence

1. Review the complete candidate, including new test owners and selectors. Run actionlint and `.github/scripts/Test-Web5SqlEvidence.ps1`; verify the environment tag policy and absence of secrets.
2. Run the applicable local focused checks and inspect failures/owned cleanup. Commit and push the reviewed candidate. Create a fresh opt-in tag at that exact SHA; do not invoke unreviewed code with SQL authority.
3. Inspect the completed hosted run and bounded JSON artifact. Require every owner summary `complete=true`, exact candidate provenance, final lifecycle `Removed`, zero user databases/active owners and no quarantine.
4. Record run/job/tag/SHA, actual engine/image/compatibility, discovered/executed/passed/failed/skipped and ownership results in task evidence. Existing SQL-free PR CI remains independently mandatory. Raw logs/TRX, credentials, JWT/cookies and business payloads remain outside published artifacts.
5. Re-run the changed gate if executable source or selector changes. Before B02 acceptance, verify the final combined candidate and its merge tree. Browser, frontend port bridge, full IIS and production approval are separate evidence levels.

The prior intentional B04-A SQL oracle RED verifies the shared fail-closed guard; B04-B does not rewrite that historical result. Its exact-candidate positive evidence must come from this lane's actual applicable tests.
