# WEB6 B03 maintenance and recovery candidate (2026-11-09)

**IMPLEMENTED / FULL CANDIDATE BLOCKED; PROTECTED CI NOT RUN / BLOCKED.** The historical 344-case candidate remains FAIL (342 passed / 2 failed). [Subsequent remediation](#subsequent-remediation-20261109) independently accepted R2e NativeSafety and the R2f ATTEMPT_05 focused SQL ownership-manifest gate only. B6-OPS-05 and B6-MNT-01..03 remain BLOCKED pending complete candidate/protected evidence and Owner acceptance. Manual resend OFF; staging NOT ACCEPTED; production NO-GO. This record does not grant an attended operator, hosted, frontend, service or production acceptance.

[Roadmap](../../plans/20261103-001-web6-recovery-quality-backend.md) | [selected implementation plan](../../superpowers/plans/2026-11-09-web6-b03-maintenance-recovery.md) | [design](../../superpowers/specs/2026-11-09-web6-b03-maintenance-recovery-design.md) | [acceptance](../../development/WEB6_BACKEND_ACCEPTANCE.md) | [operator runbook](../../../tools/MyDmsVn.WebOperation.Operator/README.md).

## Candidate and environment

This section and the frozen execution/hash tables below describe the historical failed candidate, not the later remediation candidates.

- Runtime source: `52768a16cea5bd4ca6875e9baa746ba7fbd64088`, including Tasks 1-5 and their review corrections. Task 6 adds an optional test-only environment guard and eight pure cases to the existing maintenance class, plus workflow, evidence guard, sandbox lifecycle and documentation. The final frozen build includes that guard; its exact source-file and assembly hashes are below. A future protected tag must identify the complete reviewed Task 6 HEAD.
- Branch: `codex/web6-b03-maintenance-recovery`; selected base `4d0970751d7446eda9294e9354bcf6ed8093bace`.
- Vendor gitlink: `433e5b53f979e37cc6b69bad6dfcde0975c7ecaf`, unchanged.
- Frozen Git trees: WEB4 `fcab4a1e7505b150a132ce2899002930e9f2a6ee`; WEB5 resolution `43c922fd9fded72118555d29e3a068762984a985`, unchanged.
- Windows NT 10.0.26300.0; SDK 8.0.427; net48 target, installed .NET Framework release 534296; VSTest 17.11.1 x64; designated SQL Server 14.0.2140.2.
- Existing designated nonproduction SQL environment has explicit effective `master`; credentials, generated database/login names, ownership tokens, raw TRX and paths remain local. No alternate SQL endpoint or historical quarantine was adopted.
- All SQL tests run sequentially in the existing Security.Tests owner, Release/net48, under ordinary `dotnet test`. No automatic test or mutation retry; no source changes between the final class executions.

## Implemented boundary

Tasks 1/3 add separate short-lived maintenance SQL authority, exact schema13/owned target and effective identity checks, read-only preview and one-key result compaction. Runtime/B02 operator principals remain unable to compact; no schema14, HTTP admin service, runtime permission expansion or migration-authority fallback is introduced. The EXPIRED/NULL-result tombstone remains the canonical SQL compaction record, with no terminal-negative audit row or journal DELETE. Pre-Commit uncertainty, unknown Commit and known Commit with failed readback/delivery remain distinct fail-closed outcomes. Release EXE tests inspect actual restricted SQL-login connections, exact tombstone metadata and unchanged business tables.

Task 2 provides private primary/backup evidence, protected current-operator/Administrators ACL, reparse/creation-identity/handle verification, bounded sanitized events, verified matching manifests, exact 90-day retention and attended exact-manifest purge. Deferred Minor: reject default DateTimeOffset before evidence creation. It remains open; this record does not silently fix or waive it. Same-volume private directories do not establish second-volume, power-loss or privileged-administrator resistance.

Task 4 reconciliation is read-only and independently checks source, full affected ledger histories, valuation, frozen Sales COGS and Q/V. Current positive proof is restricted to SalesPost/ReceiptPost. All four current Draft Create/Update kinds remain UNKNOWN: schema13 retains no original rowversion/line witness after result loss. A Draft baseline may be SUPERSEDED only through complete unambiguous retained-committed lineage ending in immutable Post. This cannot exclude unjournaled Local/admin delete-recreate and does not reconstruct historical response/version/line IDs. Neither outcome SETTLES, unlocks or enables browser resend.

Task 5 uses owned COPY_ONLY/CHECKSUM backup, validated restore into a separate clone, exact physical paths and schema13 snapshots, one clone epoch CAS before admission, separate restored principals and stale-epoch/session/grant rejection. The admitted current operation is **restricted-runtime journal Admit+Complete only**. Full restored HTTP/Sales/Receipt business handlers and service RPO/RTO are NOT RUN. Recorded phase times are fixture timing, not service availability measurements.

Four deterministic damaged-media, restore-set, provisioning and epoch-CAS failures preserve quarantine/admission-closed before assertions. TEST_CLEANUP_ONLY requires a single-use, exact-run/exact-fault private capability after all required assertions. It is test hygiene; original `workflowSuccess=0` stays false. Unexpected uncertainty or failed assertions never gain cleanup authorization merely because a later drain succeeds.

## Final local execution

Locked restore and Release/net48 build:

```powershell
$project='tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
dotnet restore $project --locked-mode
dotnet build $project -c Release -f net48 --no-restore -p:UseSharedCompilation=false
```

For each class below, sequentially, `$filter='FullyQualifiedName~MyDmsVn.Server.Infrastructure.IntegrationTests.'+$class`:

```powershell
dotnet test $project -c Release -f net48 --no-build --no-restore --list-tests --filter $filter
dotnet test $project -c Release -f net48 --no-build --no-restore --filter $filter --settings $privateRunsettings --blame-hang-timeout 180s --blame-hang-dump-type none --logger "trx;LogFileName=$class.trx" --results-directory $privateResults
```

The WebOperationHttpSqlTests row uses only these two exact FullyQualifiedName selectors joined by `|`: `Operator_first_cancelled_blocks_delayed_original_and_resend_for_all_six_commands` and `Original_first_commit_remains_authoritative_over_waiting_operator_for_all_six_commands`. Web5BackupRestoreTests uses explicit process-local `MYDMSVN_WEB5_BACKUP_TEST_ENABLE=1` and a newly owned private backup parent admitted for the current user plus local SQL-service SID. Its exact creation/ACL and final emptiness are verified before nonrecursive removal.

| Selection | Discovered / executed | Pass / fail / skip | TRX SHA-256 |
|---|---|---|---|
| `WebOperationMaintenanceOperatorTests` | 89 / 89 | 89 / 0 / 0 | `f0229fbb4c454e48bd776a31e5aa222cc0ddfca4f8df45c6165a2bac2584949e` |
| `WebOperationMaintenanceEvidenceTests` | 33 / 33 | 33 / 0 / 0 | `5217ceaee48e3daf8c49dd47270f0cf0b5fc343c76d7da628ff9be316576a16e` |
| `WebOperationReconciliationOperatorTests` | 21 / 21 | 21 / 0 / 0 | `e542b9ee5bf0c4f08da44c1a4296e9e479e161ea91b637593b5b9178222b21a0` |
| `Web6MaintenanceRecoveryTests` | 28 / 28 | 28 / 0 / 0 | `2e395d048473141c94fd946c70d4db00a0561012b3083c1c48713b0f2fbdd766` |
| `WebOperationOperatorDecisionTests` | 59 / 59 | 59 / 0 / 0 | `4ca05ed68e0e4cdde2061b3d2ea00845de217eb48fd9e3c7ef3d3d6c7de891d4` |
| `WebOperationOperatorInspectTests` | 40 / 40 | 40 / 0 / 0 | `cbde5b1db787a79637d16f9be8c97f0c5e7a988eeb934d47fc5f7ad663ab6c88` |
| `WebOperationResolutionTests` | 24 / 24 | 24 / 0 / 0 | `a27078a0983f62feba5fc781c1f0b664bf2c4af63f045674e99e042ec20811a8` |
| `WebOperationTerminalCrashTests` | 2 / 2 | 2 / 0 / 0 | `2a1abeeb8a460a1e9a44383507f313d9c0454b35f981d61ec6a9def474db4671` |
| `Web5BackupSafetyTests` | 2 / 2 | 2 / 0 / 0 | `0d239018380fae05a73780d7102313df020653b40258950e000f3b3c978e446e` |
| `WebRecoveryPilotResultTests` | 43 / 43 | 43 / 0 / 0 | `6ef95fdc609f49f52959e4a470598e0acbdac366325faa2e9b8524e5ed8d2e5d` |
| `Web5BackupRestoreTests` | 1 / 1 | 1 / 0 / 0 | `57144ef3801622bcdcf6d695247d140d1e46be3ded3f365234d590d35a18e8a9` |
| `WebOperationHttpSqlTests` | 2 / 2 | 0 / 2 / 0 | `cffdec22f5c79d87e45349c2373e6952bcd45142008e48bf2e559dd979ac99dc` |

**Final: 344 discovered/executed; 342 passed, 2 failed, 0 skipped, 0 retries. FAIL / BLOCKED.** The first race arrangement failed before dispatch; the second refused admission after cleanup poison. The six-kind matrix is not proved. Earlier passing iterations do not replace this failure.


The first eleven final discovery/TRX pairs are independently reconciled by `Assert-Web5SqlEvidence.ps1`: exact name multiset, unique case/execution IDs and definitions, allowlisted class coverage, nonempty total, zero fail/skip/error/timeout/abort/notRunnable/pending and Completed run outcome. The last TRX contains two genuine failures and was inspected separately after native stderr stopped the local harness. No missing or skipped SQL case establishes PASS. Earlier Task 1-5 RED/fix runs remain local historical evidence. Task 6 first ran the baseline 336/336 and later 89/89 with the added environment guard; these are iteration history, not a mixed-binary final candidate. The final table uses one frozen assembly set; its last selection failed and the candidate is blocked.

| Final frozen artifact/source | SHA-256 |
|---|---|
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/bin/Release/net48/MyDmsVn.Server.Infrastructure.Security.Tests.dll` | `d3bb0106a66a07b8d3d08e14f3dda526a38d4e27104d9b2823a9418e82223a88` |
| `src/MyDmsVn.Server.Infrastructure/bin/Release/net48/MyDmsVn.Server.Infrastructure.dll` | `b918d3d7927dfcb310e0b1abe3d3a56e2063dec92a6377870fe1303005fd68c6` |
| `tools/MyDmsVn.WebOperation.Operator/bin/Release/net48/MyDmsVn.WebOperation.Operator.exe` | `f74d7af55d023c3e6059c09f20d693d8e7dae282281bccc19a57fb8a6e1cfc21` |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationMaintenanceOperatorTests.cs` | `44e3d3b43a7bcff8e08011f64fbe26d0f9b195cb649c59d144e82c9746a72682` |
| `src/MyDmsVn.Server.DbMigrator/Scripts/013_WebOperationTerminalResolution.sql` | `c0141cfb0be994906ebd1ddb2b27f2432d62e114b7e2b7d33d98deaaed8eb588` |

No shipping publish/package or deployment was produced. These hashes bind the failed final candidate, not a passing release.


## Protected workflow and ownership

[The B03 workflow](../../../.github/workflows/backend-web6-b03-sql-candidate.yml) requires Owner actor and triggering actor, exact repository ID, tag-only `web6-b03-sql-candidate-*` push and protected `web5-sql-candidate` environment. Control and candidate both check out `github.sha`; locked restore, Release/net48 build, exact class counts, actual Release restricted-principal EXE smoke, four separate injected-fault cleanup outcomes, unchanged candidate source and zero retries are mandatory. No workflow was dispatched or tagged during this task.

It reuses the accepted private SID-owned LocalDB lifecycle and requires SQL-login capability. Windows PowerShell runs lifecycle Check/Complete to preserve JSON identity strings. Exact-source artifact/TRX hashes and sanitized bounded count/capability/lifecycle JSON alone are uploadable; raw logs, TRX, connection data, database/login names, tokens and filesystem paths are withheld.

The workflow owns one exact private outer sandbox and two protected, non-overlapping subroots via creation identity, current SID, protected ACL, random token, run/attempt/source manifest and non-reparse checks. Parent dotnet/MSBuild/VSTest TEMP/TMP and NuGet scratch remain in `tool-scratch` through restore, build, discovery and tests. Generated private runsettings inject TEMP/TMP=`evidence` into testhost, whose existing Release EXE launcher passes those exact values. The optional test guard checks canonical TEMP/TMP/Path.GetTempPath, opens the root through the existing handle-based ACL/reparse/identity policy, verifies the child environment and emits exactly one bounded marker after the restricted Release smoke. Missing/duplicate proof fails the workflow. Existing fixtures retain their own creation-token checks and GUID primary/backup pairs inside `evidence`; the smoke verifies delivered manifests and cleanup. There are no unused dummy roots. Containment supplements fixture ownership.

The evidence subroot must be empty before and after every selected test command, including cleanup of the separately owned backup-regression parent. Only after all tooling drains is the complete tool-scratch inventory sealed with relative name/type, creation identity, ACL/non-reparse state and file length/SHA-256. This is tool hygiene, never candidate evidence. No further tool writes are admitted. Always-run cleanup revalidates the entire sealed set and each exact entry, deletes only recorded entries non-recursively, verifies both subroots empty and rechecks identities before removing the outer root. Unknown children or changes quarantine the exact root; no prefix deletion, process kill or adoption is permitted. SQL cleanup independently requires owned database/session/transaction drain. TEST_CLEANUP_ONLY cannot make failed recovery workflow state successful.

Local environment-guard RED was 7 failed / 1 passed (the stub ignored mismatch/ACL); GREEN was 8/8. Initial local sandbox diagnostics correctly quarantined tool scratch when restore/build or dotnet test used the evidence temp root. The runsettings separation corrected that boundary. A scoped 89/89 probe subsequently verified one environment marker and exact empty evidence; its local harness initially shadowed a summary-path variable when dot-sourcing the evidence assertion script. Correcting that local binding allowed fresh drain/seal/cleanup without rerunning tests. The final complete run below uses the corrected harness. These local simulations do not execute protected CI.

Guard RED: after extending `Test-Web5SqlEvidence.ps1`, execution failed with `B03 protected workflow is required.` before the workflow existed. The earlier workflow revision passed 90 guard assertions. The later short-root/preflight guard is RED (`B03 short-path preflight gate missing.`); current workflow wiring remains incomplete and must not be dispatched. Earlier syntax/format/link checks do not establish the later source revision as verified. The final SQL candidate nevertheless failed. This is local workflow validation, not hosted lifecycle execution. Whole-branch independent review is separately required and is not claimed by this implementer's self-review.

## Cleanup and remaining gates

The final failure retained one new database and its outer sandbox/active marker. No cleanup, retry, revoke or SQL mutation occurred after STOP. The existing crash fixture kept its original allocation name/token/creation tuple only in testhost memory; sanitized TRX persisted none. A current token cannot substitute for the missing original comparison.

Read-only investigation identified one allocation in the failed case's creation window. Its current name/ID/create-date/token are internally consistent; observations show schema13, zero journal/terminal-audit rows, zero target sessions, transactions, locks/application locks, worker processes and new server principals. The sandbox's original creation/ACL identity still matches. This establishes current quiescence only, not retrospective ownership or cleanup authority. No application-lock acquisition/fence transaction was attempted; arrangement failed before an operation key existed.

Two earlier diagnostic tool-only sandbox quarantines also remain. The intermediate successful 89-case probe removed its separate 12-entry sealed tool inventory and sandbox; that does not authorize another root's cleanup. Historical WEB5 quarantines remain untouched.

The historical FileLoadException reported the net48 260-character full-path / 248-character directory constraint, without preserving the exact loader path. Evidence-root length was 86 versus baseline TEMP 35; longest remaining sandbox path 143 and source artifact path 225. At that checkpoint, short-root remediation and 240 full-path / 220 directory admission budgets were proposals. Later R2e verified the bounded native short-root/dependency-preflight lifecycle described below; it did not rerun or validate the failed full SQL race candidate. Workflow preflight-before-SQL integration remains incomplete, and no historical cleanup authority follows.


Provider/network/process-crash compaction proof is NOT RUN and remains an explicit gap, not PASS; no new B03 Owner gap acceptance is recorded. The inherited B02 accepted provider gap is not transferable acceptance. B03 attended operator rehearsal, protected hosted CI, full recovered HTTP/business service, service RPO/RTO, frontend native/IDB, full IIS and unperformed Narrator remain NOT RUN or separately owned. Two historical SQL quarantines and frozen WEB4/WEB5 evidence were untouched. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.


## Subsequent SQL-free safety STOP

The 344-row table above binds the failed frozen candidate. This subsection records the earlier safety STOP, before the accepted remediation below. The test-infrastructure writer was added to verify the live original SQL allocation before sealing a private versioned ownership manifest, and the crash supervisor records it before migration/scenario/child launch. It includes original name/server/ID/create-date/token, UTC, canonical SHA-256 and native file/root identity/ACL; no credentials or connection string. Normal exact manifest removal requires same-server database absence. No automatic cleanup-from-manifest is provided. The new SQL lifecycle test was NOT RUN at this earlier checkpoint; R2f ATTEMPT_05 subsequently verified its focused supervised route.

Three earlier SQL-free safety attempts each executed 9 rows, with 8 passed and the ACL-drift restoration case failed. Initial restored security objects did not mark sections modified; the third used fresh descriptors but requested SACL restoration and failed with SeSecurityPrivilege unavailable. All three exact synthetic control cases and short parents remain quarantined; those attempts allocated or mutated no SQL. Original independent pre-mutation bytes/hash/descriptor were not persisted outside those cases, so the conditional LOCAL_TEST_CLEANUP_ONLY proof was not satisfied. No fourth attempt or cleanup occurred at that checkpoint. An in-memory-only Owner|Group|Access descriptor round trip then passed 6 assertions; the correction was still a proposal at that time. Later separately authorized remediation does not change those failures or their retention.

The exact net48 testhost HTTP load/dispose and owned CrashHost launch/load/drain preflight passed in each earlier attempt under the 50-character evidence prefix. This narrowed the path diagnosis but did not prove the failed SQL race fixed. At that STOP, source verification and full-candidate review remained blocked, and the recorded process census was zero candidate workers. The three synthetic safety quarantines, failed SQL candidate and two tool-only diagnostic roots remain retained. Later failed remediation attempts also retain their recorded resources; none is adopted or treated as successfully cleaned by a subsequent pass.

<a id="subsequent-remediation-20261109"></a>

## Subsequent remediation and current blocker — 2026-11-09

Independent reviews accepted two later, separately pinned bounded gates. Their results do not replace the historical 342/344 candidate failure or establish a new full candidate:

| Remediation | Independently accepted result | Boundary |
|---|---|---|
| R2e `NativeSafety` | 9 discovered/executed/passed; 0 fail/skip/retry. The exact ACL result supplied 60 ordered markers, and all seven immutable checkpoints, materialized chain and removal receipt verified. Candidate/checkpoint roots were absent and candidate processes were zero. | SQL-free native ACL/ownership/dependency-preflight lifecycle only. Seven NativeSafety checkpoints do not satisfy the separate 28-phase B03Candidate profile. The later PowerShell 7 codec-audit incompatibility remains recorded; it does not invalidate the independently verified Windows PowerShell evidence or establish cross-runtime compatibility. |
| R2f `ATTEMPT_05` | One authorized invocation; 1 discovered/executed/passed, 0 fail/skip/retry. Independent Intent/Original/Complete records, native witness receipt, fresh parameterized exact-name SQL absence, subject-control absence, exact candidate-root removal and process drain verified. | Focused SQL ownership-manifest lifecycle only. Three witness records and private controls remain retained; earlier attempts/quarantines remain untouched. |

R2f's administrative effective-master allocation preflight is **not restricted-runtime or restricted-operator proof** (`restrictedRuntimeProof=false`). The existing restricted Release CLI and restored restricted-runtime journal Admit+Complete proofs retain their own scopes; no privilege claim transfers from the allocator.

**Full 12-selection / 28-checkpoint B03Candidate and protected workflow remain BLOCKED.** The current GitHub-hosted design keeps private witness/state on ephemeral runner storage. Its final sanitized JSON upload and complete-only checkpoint digests provide no reviewed durable private Intent/Original destination or acknowledged delivery before CREATE and the marker probe. Hashes cannot reconstruct lost private records; raw SQL identifiers, ownership tokens, connection data, private paths and TRX must not be uploaded to close that gap. Exact-Fact-only supervision wiring, strict full-result reconciliation and preflight-before-SQL integration remain incomplete. No complete candidate or protected run has been accepted after remediation.

The Owner has authorized **design only** for a self-hosted runner with a persistent encrypted evidence volume. That storage/runner design is **NOT IMPLEMENTED / NOT VERIFIED**. It must define retained ownership, independent key/access custody, durable acknowledgment before SQL side effects, runner-loss recovery and fail-closed retention without retry or adoption. Design authorization does not authorize provisioning, workflow/source behavior changes, runtime/SQL execution, cleanup, tags, dispatch or release.

All historical SQL/filesystem quarantines and failed-attempt resources retain their original no-cleanup disposition. Provider/network/process-crash compaction proof remains NOT RUN; the B02 provider exception is not B03 acceptance. Deferred Minors remain open: default-DateTimeOffset rejection before evidence creation, valid-length invalid-UTF8 codec coverage and canonical-root token-mismatch coverage. Full recovered HTTP/business service, service RPO/RTO, attended operator acceptance and separately owned frontend/IIS/Narrator gates are unchanged. B6-OPS-05 and B6-MNT-01..03 remain BLOCKED; manual resend OFF, staging NOT ACCEPTED, production NO-GO.

## Pre-PR source-only correction wave (2026-11-09)

The tracked checkpoint adapter now decodes directory O/G/A with `DirectorySecurity` and file O/G/A with `FileSecurity`. Sandbox state v3 stores handle-derived identity, ACL hash, creation time, length and binary content hash. Cleanup pins the drive, original root and every manifest object parent-first before deleting any entry, verifies complete membership, then deletes those same handles child-first. It refuses replacement, foreign membership, unsupported prior state and quarantined state; it never deletes by path or grants cleanup authority to retained resources. Native execution of this corrected cleanup is NOT RUN; historical native hashes remain historical.

Focused Windows PowerShell 5.1 evidence: actual tracked descriptor RED 5 passed / 5 failed, GREEN 10/10; sandbox path-delete RED followed by injected memory GREEN 39/39; shared common guard RED followed by default 15/15 PASS. The B03 workflow explicitly selects `-B03`; its short-path preflight refusal remains expected, with a separate unconditional durable-private-witness blocker pending reviewed integration. This preserves unrelated WEB5/B02/B04 guard scope and does not enable B03 dispatch. Existing tracked parser/path/checkpoint checks passed 32/40/193/29 respectively. Release/net48 Security.Tests build succeeded with 0 warnings/errors; four SQL-free memory classes discovered/executed/passed 168/168/168, failed/skipped 0/0. Native adapter compilation succeeded without invoking native IO.

The four earlier `Test-Web6R2f*` private-controller regression scripts were excluded from the tracked package and retained as byte-identical local-only snapshots; their prior results remain local historical evidence. The new descriptor, sandbox cleanup and evidence-profile regressions use tracked source and run in a clean checkout. No private run-once/controller, witness records, ownership identifiers, credentials or local artifacts were published. No SQL, native deletion, retained-resource cleanup or protected workflow ran in this wave. Full B03Candidate, durable private storage and protected acceptance remain BLOCKED; manual resend OFF, staging NOT ACCEPTED, production NO-GO.
