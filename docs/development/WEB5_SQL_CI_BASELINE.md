# WEB5 B04-A protected SQL baseline

The selected B04-A lane verifies existing WEB4 SQL owners on an isolated GitHub-hosted Windows VM. It does not implement B02, enable retry, deploy IIS or approve production. [Owning plan](../plans/20261102-001-web5-operational-safety-backend.md#5-b04--protected-sql-ci-split-into-b04-a--b04-b-web5-04) and [dated evidence](../evidence/web5/20261102-b04a-protected-sql-ci.md) own scope/results.

## Trust and immutable input

`.github/workflows/backend-web-sql-baseline.yml` accepts only explicit Owner tag pushes `web5-sql-baseline-*` in repository ID `1409766698`, with both actor and triggering actor `chung6a8m`. The `web5-sql-baseline` GitHub environment permits that tag pattern only. No PR/fork trigger, arbitrary source input, downloaded build artifact, environment secret or persistent SQL credential is used. `GITHUB_TOKEN` has contents-read only; both checkouts disable persisted credentials; actions are commit-pinned. Repository administrators remain part of the trust boundary.

The control checkout is the exact reviewed tag commit. The test checkout is immutable existing WEB4 source `fc36c879fdd2d46ee77d136bdb6f9e70f7191b64` (merged Roadmap05 documentation, before B01/B02 implementation). B04-B must separately review and pin the combined B02 test/source candidate. Existing `.github/workflows/backend-web.yml` and frontend CI are unchanged.

A tag push bootstraps the proposed lane without first merging it: GitHub only receives `workflow_dispatch` for workflows already on the default branch. See [GitHub workflow syntax](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax) and [environment deployment protection](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/control-deployments). The Owner must review the exact control SHA before creating a fresh opt-in tag. Do not retarget old evidence tags or rerun uncertain jobs as proof of cleanup.

## SQL instance and resource lifetime

The Windows2022 image provides LocalDB through Visual Studio's SQL LocalDB runtime component ([runner image inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)). `Web5SqlInstance.ps1` chooses installed SQL2019 LocalDB15 first, otherwise SQL2022 LocalDB16, refusing unreviewed versions. Actual engine/edition/image are recorded. This is a real SQL Express engine in a fresh private named user instance, **not evidence of running the SQL2017 engine itself**; the owned model database is set to compatibility140 before any test database allocation. See [Microsoft LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb?view=sql-server-2017). No remote/shared SQL endpoint or local developer database is accepted.

Each job gets a new hosted VM and randomly named private instance. The normal Windows runner SID owns it. Private state binds SID, run/attempt, control SHA, server identity, master creation identity and a random token written to a master extended property. The generated master connection uses integrated authentication, no pooling and no connection retry. It is masked, written only to `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` in job environment and never uploaded. The safe artifact exposes a SID hash, never the SID/token/connection string.

Existing `SqlTestDatabase` continues to allocate/drop individual databases with its identity/token checks. The CI helper does not run tests or drop databases. After each owner, it verifies exact instance identity, zero user databases, no other user SQL sessions/transactions and no testhost/migrator workers. The lifecycle persists Checking/Completing before observations or removal; interrupted phases cannot be reused. Before invoking an owner, YAML creates an active marker. It is removed only after complete discovery/TRX reconciliation (or recognition of the one expected RED assertion). Timeout, aborted/incomplete output, unexpected failure or uncertain drain leaves the marker/quarantine terminal; no next owner or stop/delete is permitted. A known ordinary RED assertion may still prove clean teardown.

Test commands have a 90-second VSTest hang watchdog, 10-minute owner-step bounds and a 30-minute job bound. Cleanup has a 2-minute outer bound. These bounds do not prove rollback or drain. Uncertain resources remain quarantined for the VM lifetime; GitHub's eventual VM disposal is containment, never a clean teardown PASS. Recovery is a separately reviewed fresh tag/VM. No bulk-prefix cleanup, force-stop, force DB drop, pool clearing, shared SQL permission grant or restored SqlTestHarness is introduced.

## Owner commands and truth gate

The YAML runs ordinary locked restore/build and `dotnet test` discovery/execution directly, sequentially:

| Owner | Required classes | Source-expanded expected cases (verify actual results) |
|---|---|---:|
| Security | WebOperationJournalTests, WebOperationReadTests, WebOperationHttpSqlTests, WebPrincipalProvisioningTests |22+24+17+16=79|
| Inventory | WebOperationCommandTests |7|
| Migrations | WebAuthMigrationTests |17|

The optional frontend-port bridge is excluded from Security; no browser/IIS work is claimed. Actual discovery names must match every TRX result name, counts must reconcile, every required class must execute, all required outcomes must pass and all skip/error/abort counters must be zero. Empty selectors, duplicate discovery, missing classes, false counters and unrelated failures fail closed. Count expectations above describe this pinned source; runtime evidence is authoritative.

For `web5-sql-baseline-red-*` only, the disposable checkout changes exactly one Inventory oracle from expected24m to25m. It selects that single six-command SQL test, requires actual Assert.Equal expected25/actual24 and keeps the job failed. No production/test source change is committed. Two subsequent GREEN tags select all three owners on the original pinned source; GREEN also requires a clean tracked baseline diff. Any missing required result or lifecycle evidence fails the final gate.

Raw restore/build/discovery/test logs and TRX stay in the ephemeral VM and are never uploaded/echoed. Only bounded count/class/recognition JSON and runner/SQL lifecycle provenance leave the VM. Unexpected failure messages are constant; diagnostic raw data is not an artifact. Do not upload SQL identifiers, provider exception bodies, JWT/cookies, dumps, user data or the private instance state.

## Operation

1. Review control workflow/scripts, run `pwsh -NoProfile -File .github/scripts/Test-Web5SqlEvidence.ps1` and actionlint; verify production, schema, locks and corpus remain unchanged.
2. Configure environment `web5-sql-baseline` with custom deployment policy for tags `web5-sql-baseline-*` only. Read back policy and confirm no secrets. Keep PR SQL-free CI separate.
3. Push the reviewed `codex/web5-b04a-sql-ci-foundation` candidate. Create a new Owner opt-in RED tag at the exact commit, then two distinct GREEN tags at the same commit. Push one tag and inspect its completed evidence before the next; never infer PASS from tag creation.
4. Record exact tag/control/test SHA, workflow run/job, image/engine/compatibility, discovery/TRX counts, known RED recognition and Removed/zero-resource lifecycle for each run. Retain only sanitized artifact JSON in evidence.
5. B04-A is verified only after RED and both GREEN results plus existing PR CI pass. B04-B and B04 completion remain pending. No same-key resend, terminal-negative protocol, IIS or production approval follows.
