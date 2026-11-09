# WEB6 B03 Maintenance & Recovery Rehearsal Implementation Plan

> **CLOSED BOUNDED WITH OWNER_ACCEPTED_EXCEPTION — Owner 2026-11-09.** This is a historical implementation record, not an active execution queue. [Owner closeout](../../evidence/web6/20261109-b03-maintenance-recovery.md#owner-bounded-closeout-20261109) retains historical 342/344 FAIL, full 12-selection candidate NOT ACCEPTED and protected hosted SQL NOT RUN with explicitly accepted bounded exceptions. R2e 9/9 and R2f ATTEMPT_05 1/1 remain bounded PASS only. Self-hosted encrypted witness infrastructure is DEFERRED / UNSCHEDULED. The original steps/checkboxes below are retained for provenance; they do not authorize implementation, rerun, cleanup, tags or dispatch. B05 needs a separately selected prerequisite audit, explicit B03 dependency waiver and paired frontend F02/F03 readiness. Manual resend OFF; staging NOT ACCEPTED; production NO-GO.

**Goal:** Deliver a guarded schema13 compaction CLI, durable sanitized audit/case evidence, COMMITTED-result-unavailable reconciliation, and an owned backup/restore/epoch rehearsal without widening runtime or B02 operator authority.

**Architecture:** Extend the isolated net48 operator executable with separate maintenance commands and a dedicated database-CONTROL connection boundary. Keep the EXPIRED journal tombstone as the canonical same-transaction SQL mutation record, place operator/correlation evidence in a private primary-plus-backup file store, and keep BACKUP/RESTORE inside an ownership-bound test fixture rather than the CLI. The six tasks are ordered because parsing/authority feeds evidence, evidence feeds execute/reconciliation, and all of them feed the final restore rehearsal and protected candidate workflow.

**Tech Stack:** C# 12 / .NET Framework 4.8, Microsoft.Data.SqlClient 7.0.2, xUnit/VSTest, SQL Server schema13, PowerShell/GitHub Actions `windows-2022`.

**Spec:** `docs/superpowers/specs/2026-11-09-web6-b03-maintenance-recovery-design.md`

## Global Constraints

- Base is merged backend `main@4d0970751d7446eda9294e9354bcf6ed8093bace`; preserve exact schema13 and frozen WEB4/WEB5 corpora.
- The B03 maintenance identity is unique, short-lived and granted database `CONTROL` only on the exact owned disposable database; B02 operator and web runtime remain denied.
- Do not add schema14, a stored procedure, a SQL audit table, a permanent maintenance role, runtime DI/controller/admin endpoint or dependency upgrade.
- The exact EXPIRED tombstone is the canonical durable in-SQL compaction record; external evidence owns operator/correlation/delivery state.
- Never retry compaction, reconciliation or mutation automatically. Commit/readback/audit uncertainty is `UNKNOWN/ESCALATE` and requires independent inspection.
- Never synthesize result bytes, versions, line IDs, browser settlement, unlock, resend or a new operation ID.
- Evidence excludes credentials, connection strings, JWT/cookies, raw request/result bytes and business payload; retention is 90 days with a separately owned backup copy and exact-manifest attended purge only.
- Backup/restore touches only newly allocated ownership-proven source, clone, backup, data and log paths. Historical quarantines and unrelated IIS resources are never enumerated for deletion.
- Manual resend remains OFF; staging remains NOT ACCEPTED; production remains NO-GO.

## Review Focus

- Evidence-root reparse/ACL identity changes between validation and write must fail closed before mutation; `MaintenanceEvidenceStore_revalidates_identity_before_every_write_and_copy` in Task 2 pins this.
- Disk-full/partial external audit after known SQL Commit must report committed-with-audit-delivery-failure, never rollback or retry; `Known_commit_with_failed_outcome_delivery_requires_escalation_without_second_compaction` in Task 3 pins this.
- Superseded reconciliation must reject cross-actor, cross-epoch, reused-target and non-monotonic journal rows; `Superseded_reconciliation_requires_same_owned_monotonic_committed_lineage` in Task 4 pins this.
- Retention must use manifest UTC plus exact identity, not local date, prefix or enumeration; `Purge_requires_expired_primary_and_backup_manifests_with_exact_hashes` in Task 2 pins this.
- Partial restore/credential/cleanup failure must quarantine only the exact run-owned resource and keep writer admission closed; `Restore_failure_keeps_admission_closed_and_retains_exact_owned_quarantine` in Task 5 pins this.

---

### Task 1: Maintenance command parsing, authority and read-only compaction preview

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/CompactOptions.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorCompactionResult.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorCompactionApplication.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/SqlMaintenanceConnectionFactory.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorCompactionClient.cs`
- Create: `deployment/sql/Provision-MyDmsVnWebOperationMaintenance.sql`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationMaintenanceOperatorTests.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/Program.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/OperatorExitCode.cs`

**Interfaces:**
- Produces: `CompactOptions : IOperatorTargetOptions`; `IOperatorCompactionClient.PreviewAsync(CompactOptions, CancellationToken)`; `OperatorCompactionResult` with `Eligible`, `NoChange`, `Completed`, `Unknown`, and `AuditDeliveryFailed` outcomes; `IMaintenanceSqlConnectionFactory.OpenAsync(IOperatorTargetOptions, CancellationToken)`.
- Consumes: existing `SqlWebOperationMaintenance`, schema13 `WebOperationJournal`, `OperatorExitCode` and explicit environment/database targeting conventions.

- [ ] **Step 1: Add parser/application RED tests**

  Add `Compact_help_is_sql_free`, `Compact_modes_and_exact_confirmation_are_strict`, `Compact_rejects_noncanonical_identity_before_SQL`, `Compact_preview_reports_only_eligible_or_no_change`, and `Compact_preview_never_calls_execute`. Assert exact options: command, mode, environment, database, current/stored epoch, actor, operation GUIDv4, fingerprint, correlation GUIDv4, case ID and timeout 1..25; execute confirmation binds every field.

- [ ] **Step 2: Run parser/application RED**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter FullyQualifiedName~WebOperationMaintenanceOperatorTests --logger "trx;LogFileName=b03-task1-red.trx"`

  Expected: FAIL because `CompactOptions`, `OperatorCompactionApplication` and compaction client contracts do not exist.

- [ ] **Step 3: Implement parser, result and preview routing**

  Implement `compact-preview` and `compact-execute` parsing in `CompactOptions`; expose `ConfirmationText`; route both commands in `Program.Main`. `OperatorCompactionApplication.RunAsync` must preserve existing exit-code meanings and print only bounded labels. `SqlOperatorCompactionClient.PreviewAsync` reads the exact journal row inside an explicit rollback transaction and returns `Eligible` only for elapsed COMMITTED+bytes, `NoChange` only for an exact EXPIRED tombstone, and denied/unknown otherwise; it must not call `CompactAsync`.

- [ ] **Step 4: Add maintenance-principal SQL RED tests**

  Add `Maintenance_provisioning_is_exact_database_control_and_distinct_from_runtime_and_operator`, `B02_operator_and_runtime_cannot_preview_or_compact`, `Maintenance_factory_rejects_server_control_roles_other_database_and_unexpected_identity`, and `Release_preview_uses_only_the_dedicated_maintenance_environment`. The test-owned login/user must be dropped by exact recorded identity in `finally`.

- [ ] **Step 5: Run principal RED**

  Run the Task 1 class with SQL category `SecurityProvisioning` and the designated owned `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`.

  Expected: FAIL because maintenance provisioning and principal verification do not exist; no mandatory SQL skip is accepted.

- [ ] **Step 6: Implement the short-lived principal boundary**

  `Provision-MyDmsVnWebOperationMaintenance.sql` accepts pre-existing distinct `MaintenanceUser`, `OperatorUser` and `WebRuntimeUser`, requires exact schema13, grants database `CONTROL` directly only to `MaintenanceUser`, and grants no server/role/runtime authority. `MaintenanceSqlConnectionFactory` uses `MYDMSVN_WEB_OPERATION_MAINTENANCE_CONNECTION_STRING`, `_DATABASE_USER`, and `_LOGIN`; it verifies exact database/login/user, `CONTROL=1`, server-control/admin roles=0, and rejects runtime/operator identity overlap.

- [ ] **Step 7: Run Task 1 GREEN and B02 regressions**

  Run the complete Task 1 class plus `WebOperationOperatorInspectTests` and `WebOperationOperatorDecisionTests` sequentially. Expected: all discovered cases pass, zero fail/skip/retry; preview SQL snapshot is byte-equal.

- [ ] **Step 8: Commit Task 1**

  Commit: `feat(web6): add guarded compaction preview`

### Task 2: Private evidence store, backup copy and retention

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/MaintenanceEvidenceRecord.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/MaintenanceEvidenceStore.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/MaintenanceEvidencePolicy.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationMaintenanceEvidenceTests.cs`

**Interfaces:**
- Produces: `MaintenanceEvidencePolicy(primaryRoot, backupRoot, TimeSpan retention)` fixed to 90 days; `IMaintenanceEvidenceStore.WriteAttemptAsync`, `WriteOutcomeAsync`, `FinalizeAndBackupAsync`, and `PurgeExpiredAsync`; immutable `MaintenanceEvidenceReceipt` carrying case ID, event/manifest hashes and delivery state.
- Consumes: validated `CompactOptions`/case identity from Task 1 and Windows ACL/file identity primitives.

- [ ] **Step 1: Add evidence-policy RED tests**

  Add tests for absolute distinct roots, protected current-user/Administrators ACL, non-reparse ancestry, create-new case directory, canonical UTF-8/no-secret JSON, append ordering, primary/backup reciprocal identifiers and exact SHA-256 manifest verification. Add `MaintenanceEvidenceStore_revalidates_identity_before_every_write_and_copy` by replacing a validated child with a reparse/foreign identity before the next action.

- [ ] **Step 2: Run evidence RED**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter FullyQualifiedName~WebOperationMaintenanceEvidenceTests --logger "trx;LogFileName=b03-task2-red.trx"`

  Expected: FAIL because the store and policy do not exist.

- [ ] **Step 3: Implement atomic sanitized evidence**

  Implement create-new event files and a final manifest using bounded DTO fields only: version, UTC, command, hashed environment/database/login, actor, abbreviated operation/correlation, epochs, fingerprint hash, phase, transaction certainty, readback and delivery. Revalidate root/case ownership, ACL and reparse status immediately before every write/copy. Flush each file before returning its hash; never overwrite an existing event or manifest.

- [ ] **Step 4: Add retention/purge RED tests**

  Add `Purge_requires_expired_primary_and_backup_manifests_with_exact_hashes`, non-expired/clock-skew denial, missing/changed backup denial, prefix/path traversal denial, and partial-copy retention. Use an injected UTC clock; age is measured from final manifest UTC, not filesystem timestamps.

- [ ] **Step 5: Implement backup and attended purge contract**

  `FinalizeAndBackupAsync` creates a new backup case, copies verified event bytes, writes reciprocal identifiers and verifies all hashes. `PurgeExpiredAsync` accepts exact primary path, backup path, case ID and both manifest hashes; it deletes nothing unless both are 90+ days old and still identity/hash matched. It never enumerates siblings or derives targets from prefixes.

- [ ] **Step 6: Run Task 2 GREEN**

  Run the complete evidence class twice from clean unique roots. Expected: all pass, no leaked files/processes, zero secrets in captured output.

- [ ] **Step 7: Commit Task 2**

  Commit: `feat(web6): add bounded maintenance evidence store`

### Task 3: Single-attempt compaction execute and certainty lifecycle

**Files:**
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorCompactionExecution.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorCompactionClient.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/OperatorCompactionApplication.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationMaintenanceOperatorTests.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md`

**Interfaces:**
- Produces: `OperatorCompactionExecution.RunAsync` and an internal transaction result that distinguishes pre-Commit denied/cancelled, unverified cleanup, Commit unknown, known Commit, matched readback and post-Commit audit delivery failure.
- Consumes: `SqlWebOperationMaintenance.CompactAsync`, `CompactOptions`, `IMaintenanceEvidenceStore`, and maintenance connection factory from Tasks 1–2.

- [ ] **Step 1: Add certainty-boundary RED tests**

  Add pure tests for attempt-audit failure before mutation; mutation failure with rollback/close/dispose combinations; cancellation after mutation but before Commit; Commit throw; known Commit plus readback mismatch; known Commit plus failed outcome/final-manifest/backup delivery; matched Commit/readback/delivery; and exact single call to `CompactAsync`. Add `Known_commit_with_failed_outcome_delivery_requires_escalation_without_second_compaction`.

- [ ] **Step 2: Run certainty RED**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter "FullyQualifiedName~WebOperationMaintenanceOperatorTests&Category!=SqlServer" --logger "trx;LogFileName=b03-task3-red.trx"`

  Expected: FAIL because the compaction execution lifecycle does not exist.

- [ ] **Step 3: Implement execution lifecycle**

  Re-preview, durably write attempt evidence, start one ReadCommitted transaction, call `CompactAsync` once with the correlation and attempt callback, require changed rows exactly 1, check cancellation immediately before Commit, then Commit. Attempt all phase-appropriate cleanup. After known Commit, open a fresh connection and verify exact EXPIRED/NULL bytes/unchanged identity before outcome evidence and backup. Map unverified pre-Commit cleanup and Commit/readback uncertainty to UNKNOWN; map known Commit plus delivery failure to `AuditDeliveryFailed` with no retry.

- [ ] **Step 4: Add real SQL compaction RED tests**

  Add exact eligible transition, second-attempt `NoChange`, not-elapsed/wrong actor-key-epoch-fingerprint/disposition denial, audit failure rollback, explicit rollback, lock timeout, cancellation, concurrent writer/compactor serialization, immutable tombstone, no terminal-negative audit, byte-equal business tables, and Release executable tests under the temporary maintenance principal.

- [ ] **Step 5: Run SQL RED then implement the minimal SQL client behavior**

  Run the Task 3 SQL cases sequentially against one newly owned fixture per case. Expected RED must name the missing execute path. Implement only the client/application/readback needed by those tests; do not change `SqlWebOperationMaintenance` authorization or schema.

- [ ] **Step 6: Run Task 3 GREEN and B02 regressions**

  Run the complete maintenance operator class plus B02 inspect/decision classes; record discovery/TRX counts and hashes. Expected: zero fail/skip/retry and verified fixture cleanup.

- [ ] **Step 7: Commit Task 3**

  Commit: `feat(web6): execute audited result compaction`

### Task 4: COMMITTED-result-unavailable reconciliation

**Files:**
- Create: `src/MyDmsVn.Server.Infrastructure/Operations/SqlWebOperationReconciliationMaintenance.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/ReconcileCommittedOptions.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorReconciliationResult.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/OperatorReconciliationApplication.cs`
- Create: `tools/MyDmsVn.WebOperation.Operator/SqlOperatorReconciliationClient.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebOperationReconciliationOperatorTests.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/Program.cs`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md`

**Interfaces:**
- Produces: `SqlWebOperationReconciliationMaintenance.ReconcileAsync(int actor, Guid epoch, Guid operation, byte[] fingerprint, CancellationToken)` returning `WebOperationBusinessReconciliation`; CLI outcomes `BUSINESS_COMMITTED_RESULT_UNAVAILABLE`, `BUSINESS_COMMITTED_RESULT_UNAVAILABLE_SUPERSEDED`, or `UNKNOWN/ESCALATE`.
- Consumes: maintenance connection/evidence store, schema13 journal, Sales/Goods Receipt source tables, `StockLedger`, `StockValuationEntries`, `StockBalances`, and stable operation-kind/target mapping.

- [ ] **Step 1: Add parser and pure reconciliation RED tests**

  Add strict `reconcile-committed` input/help/output tests, no confirmation/mutation mode, no raw data output, and evidence-delivery failure behavior. Assert that result DTOs contain only bounded counts/booleans/hashes and never response bytes/version/line IDs.

- [ ] **Step 2: Add six-kind SQL oracle RED tests**

  Seed Sales Create/Update/Post and Receipt Create/Update/Post COMMITTED rows whose result is expired/unavailable. Assert current source header/line identity, Posted state where applicable, stock/ledger/valuation/COGS balance, actor/epoch/kind/target/fingerprint, and zero mutation. Add contradictions for missing header/line, wrong Posted state, ledger/valuation/balance mismatch and deleted source.

- [ ] **Step 3: Add superseded-lineage RED tests**

  Add same-target later COMMITTED update/post success, then `Superseded_reconciliation_requires_same_owned_monotonic_committed_lineage` covering cross-actor, cross-epoch, earlier/equal completion time, wrong module/target, target reuse ambiguity, CANCELLED/INDETERMINATE later rows and contradictory current state. Only exact later lineage may produce the superseded outcome.

- [ ] **Step 4: Run reconciliation RED**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter FullyQualifiedName~WebOperationReconciliationOperatorTests --logger "trx;LogFileName=b03-task4-red.trx"`

  Expected: FAIL because reconciliation types/query/client do not exist.

- [ ] **Step 5: Implement read-only reconciliation**

  Use fixed parameterized SQL selected by the six enum kinds; no dynamic identifiers from input. Require COMMITTED/EXPIRED identity and unavailable result, derive the durable document target from journal result/target fields, verify current physical invariants, and separately query later COMMITTED same-actor/same-epoch/same-module/same-target rows ordered by Completion UTC and operation ID. Roll back the explicit read transaction and write only sanitized case evidence.

- [ ] **Step 6: Run Task 4 GREEN and no-mutation oracle**

  Run the full reconciliation class plus existing Web operation read/resolution and six-command HTTP SQL regressions. Compare byte snapshots before/after every reconciliation. Expected: zero fail/skip/retry and all owned fixtures cleaned.

- [ ] **Step 7: Commit Task 4**

  Commit: `feat(web6): reconcile unavailable committed results`

### Task 5: Owned backup/restore, epoch rotation and cleanup rehearsal

**Files:**
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6MaintenanceRecoveryFixture.cs`
- Create: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web6MaintenanceRecoveryTests.cs`
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/Web5BackupFixture.cs` only if a generic ownership check must be reused without changing existing semantics
- Modify: `tests/MyDmsVn.Server.Infrastructure.Security.Tests/WebRecoveryPilotControl.cs` only for a fixed ownership-bound clone action; never accept arbitrary SQL or database names

**Interfaces:**
- Produces: a test-only `Web6MaintenanceRecoveryFixture` with exact source/clone/file manifests, writer-admission state, single clone epoch rotation, principal provisioning, physical-oracle capture and ownership-proven cleanup/quarantine result.
- Consumes: existing `SqlTestDatabase`, `Web5BackupFixture`, schema13 migrator, Tasks 1–4 commands and the fixed recovery control protocol.

- [ ] **Step 1: Add ownership/admission RED tests**

  Add refusal of source=clone, system/unowned names, existing/reparse backup/data/log paths, mismatched creation identity, second epoch rotation, writer admission before rotation, arbitrary control actions and cleanup without exact manifests.

- [ ] **Step 2: Add end-to-end restore RED test**

  `Copy_only_backup_restores_B03_source_to_distinct_owned_clone_and_rotates_epoch_before_admission` must seed compacted and reconciliation cases, drain source, COPY_ONLY+CHECKSUM backup, restore to distinct paths, verify schema/physical oracle, rotate clone epoch once, provision fresh runtime/operator/maintenance identities, deny old epoch/session/grant, admit one current bounded operation, re-run compact/reconcile inspection, drain and clean exact resources.

- [ ] **Step 3: Add failure/quarantine RED tests**

  Add backup checksum failure, restore failure, principal provisioning failure, epoch CAS failure, stale writer attempt, process/session drain timeout and `Restore_failure_keeps_admission_closed_and_retains_exact_owned_quarantine`. Assert no cleanup of historical or sibling resources.

- [ ] **Step 4: Run recovery RED**

  Run: `dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --filter FullyQualifiedName~Web6MaintenanceRecoveryTests --logger "trx;LogFileName=b03-task5-red.trx"`

  Expected: FAIL because the B03 fixture/rehearsal does not exist.

- [ ] **Step 5: Implement the smallest ownership-bound fixture**

  Reuse existing private-directory and disposable-name checks. Keep BACKUP/RESTORE SQL inside the fixture owner; commands receive only generated exact identities. Record UTC recovery phases without claiming service RPO/RTO. Writer admission remains closed until clone oracle, epoch rotation and principal provisioning all pass.

- [ ] **Step 6: Run Task 5 GREEN plus existing backup/recovery regressions**

  Run B03 recovery, `Web5BackupSafetyTests`, `Web5BackupRestoreTests` and `WebRecoveryPilotResultTests` sequentially. Expected: zero fail/skip/retry; success removes exact owned source/clone/files, failure reports exact quarantine without broad cleanup.

- [ ] **Step 7: Commit Task 5**

  Commit: `test(web6): rehearse maintenance backup recovery`

### Task 6: Protected candidate workflow, evidence and whole-branch review

**Files:**
- Create: `.github/workflows/backend-web6-b03-sql-candidate.yml`
- Create: `docs/evidence/web6/20261109-b03-maintenance-recovery.md`
- Modify: `.github/scripts/Test-Web5SqlEvidence.ps1`
- Modify: `docs/evidence/web6/README.md`
- Modify: `docs/development/WEB6_BACKEND_ACCEPTANCE.md`
- Modify: `docs/development/WEB6_CODEX_HANDOFF.md`
- Modify: `docs/plans/20261103-001-web6-recovery-quality-backend.md`
- Modify: `tools/MyDmsVn.WebOperation.Operator/README.md`

**Interfaces:**
- Produces: Owner-tag-triggered exact-HEAD `web6-b03-sql-candidate-*` workflow and a candidate evidence record with exact source, counts, TRX/artifact hashes, principal capability, process/session/database/file cleanup and retained gaps.
- Consumes: final Task 1–5 test classes, existing `web5-sql-candidate` environment/private LocalDB lifecycle and evidence assertion scripts.

- [ ] **Step 1: Add workflow guard RED**

  Extend `Test-Web5SqlEvidence.ps1` to require exact tag/repository/Owner/environment guards, locked restore/Release net48 build, explicit allowlisted test classes, exact discovered/executed counts, zero fail/skip/retry, Release CLI restricted-principal smoke, unchanged candidate source, sanitized-only artifacts and always-run owned cleanup. Run it and observe RED because the B03 workflow is absent.

- [ ] **Step 2: Implement the protected workflow**

  Mirror the accepted B02b lifecycle: checkout control and candidate at `github.sha`, private SID-owned LocalDB, SQL-login capability, Windows PowerShell lifecycle Check, exact evidence JSON and ownership-proven removal. Add separately owned primary/backup evidence roots under runner temp and require both drained/removed or quarantined explicitly. Do not upload raw TRX/logs, credentials, database names or paths.

- [ ] **Step 3: Run complete local candidate verification**

  Run locked restore, Release/net48 build, exact `--list-tests`, each focused class sequentially with TRX, B02 regressions, backup/recovery regressions, evidence guard and post-run process census. Verify every fresh fixture/file completed owned cleanup. Record actual counts and SHA-256 values; no retry may be used to manufacture PASS.

- [ ] **Step 4: Update workflow exact counts and candidate evidence**

  Pin the observed discovery counts in workflow assertions. Evidence state remains `IMPLEMENTED / LOCAL SQL-VERIFIED CANDIDATE; PROTECTED CI NOT RUN / BLOCKED` until a separately authorized exact-HEAD tag succeeds. B6-OPS-05 and B6-MNT-01..03 remain BLOCKED pending hosted evidence and Owner acceptance.

- [ ] **Step 5: Run final docs/format/source checks and commit**

  Run evidence guard, `git diff --check`, Markdown UTF-8 no BOM/CRLF working-tree checks, YAML LF checks, frozen-corpus hashes and `git status`. Commit: `docs(web6): record B03 maintenance candidate`.

- [ ] **Step 6: Obtain independent whole-branch review**

  Review merge-base..HEAD against this plan/spec with focus on principal separation, transaction certainty, evidence path TOCTOU, superseded lineage, restore admission and cleanup. Fix confirmed P0/P1/P2 with focused RED→GREEN tests and rerun affected suites. Do not merge, create protected tags/environments, enable resend, or mark acceptance without explicit Owner authorization.
