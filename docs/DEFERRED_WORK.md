# Deferred work — outside the active roadmap

**Updated:** 2026-10-18. These requirements remain **OPEN / DEFERRED**. The owner has selected [RESP and L2DB](plans/README.md) as the only active plans. Do not start this backlog automatically. Deferral is not acceptance, a release waiver or permission to remove coverage.

Original detailed scope remains available in the [P5/P6 remainder](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261016-004-remaining-p5-acceptance-and-p6-hardening.md), [P6 operational checklist](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/archive/20261008-007-phase-6-local-hardening.md) and [P7 plan](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261008-008-phase-7-remote-api.md). The requirements below carry forward their unfinished work using current net48, SEC-REBUILD and test-policy decisions.

## 1. P5 interactive acceptance

### REMAIN-P5-GUI-01 — Catalogs and lookups

- [ ] On the actual packaged Login-first net48 host, exercise Products, Warehouses, Employees and Customers CRUD, active state, permissions, validation and error states.
- [ ] Verify rapid free-text debounce, cancellation/stale-response suppression, immediate explicit Refresh and page reset.
- [ ] Keep Goods Receipt open while Product/Warehouse/Employee changes or deactivates/reactivates; prove same-process `CatalogChangedMessage` reloads lookups, preserves historical selection and permits only active new selections. Reopened screens must fetch current data; no cross-process messenger guarantee.
- [ ] Record package/revision, OS/runtime, account/permission scenario, observations and redacted screenshots with PASS/FAIL/NOT RUN. Headless tests do not complete this attended check.

### REMAIN-P5-GUI-02 — Inventory workflow

- [ ] From the packaged authenticated host: create catalogs → create/save/reload a receipt Draft → Post → inspect balance/card → reject duplicate Post → reconcile ledger/balance.
- [ ] Exercise restricted/direct-denied accounts, Post-only versus Write/Read permissions, stale rowversion, UI validation, logout/another user and restart.
- [ ] Confirm failed/unauthorized Post leaves data unchanged and commit uncertainty triggers reload/state verification, not a blind retry. Record this separately from existing SQL/Application evidence.

## 2. P6 operational and release work

### REMAIN-P6-OPS-01 — T01–T04 operational gaps

- [ ] Verify clean-machine publish/install integrity, source/package fingerprints, current runtime/admin packages and absence of privileged credentials. Use current security administration/bootstrap, not the retired sync workflow.
- [ ] Validate runtime-principal least privilege, credential rotation, missing/malformed configuration, SQL offline, incompatible/partial/future schema, denied log path/full disk, active audit sink and safe diagnostics.
- [ ] Exercise actual install/upgrade/reinstall/uninstall, config/data preservation and limited-user execution. Ordinary desktop install/startup must not migrate or bootstrap security.
- [ ] Evaluate two-client-version/schema combinations before claiming mixed-version rollout. The current manifest is exact; unsupported combinations remain blocked and require stop/drain maintenance.

### RELEASE-MANIFEST-01 — Observed schema metadata discrepancy

- [x] Reconcile package/runtime schema metadata from one source: the shipped admin Infrastructure assembly's `SchemaCompatibilityPolicy.CurrentScripts`. A bounded x86 reader handles the net48/x86 assembly from a 64-bit publisher. Remove the old schema8/P3.1 pin and include the exact required script list.

**Closed narrowly in L2DB-T02 startup follow-up:** clean commit `d80130300f39ccbf716a1c5df627d3550e2bd0a5` package verified schema9 / ordered001–009, sourceTreeDirty=false and all224 file hashes. Focused packaging tests passed4/0/0, including an x86 policy fixture and missing-policy rejection. [Evidence](L2DB_VERIFICATION.md#clean-commit-package-reconciliation--release-manifest-01-closed). Historical schema8 artifacts remain historical; this closes metadata only. Runtime startup, attended business workflows and the remaining P6 release work are not accepted by this result.

### REMAIN-P6-T05 — Packaged GUI smoke

- [ ] Run real Login, ordinary-user/direct-deny and current superuser workflows, all catalogs, receipt Draft/edit/reload/Post, balance/card, duplicate rejection, logout/relogin and restart.
- [ ] Verify stale receipt tokens, filter/lookup/messenger behavior and reconciliation after both success and failure. Preserve no-modal exception/DataError handling and bounded startup/SQL-failure behavior.
- [ ] Record real controls/STA automation separately from attended screenshots/observations and strong backend assertions. Test every shipped target; today that is net48. AppCore is NOT SHIPPED until separately reactivated.

### REMAIN-P6-T06 — Backup and failure recovery

- [ ] Define verified SQL Server version/edition/topology and authentication/network support matrix; distinguish untested environments.
- [ ] Assign backup owner, storage/integrity/privacy checks and expected RPO/RTO; successfully restore a pre-upgrade backup into a **separate safe database** before recovery rehearsals.
- [ ] Define a supported upgrade/recovery path and exercise stop/drain, maintenance principal, single-migrator locking, partial migration after an earlier script commit, crashed/killed/concurrent migrator and old client left open during upgrade. The current migrator accepts only Empty/Compatible and rejects older prefixes as `Migration.UnsupportedExistingDatabase`; ordinary rerun is not a supported completion path for them.
- [ ] Record forward-fix versus explicit restore decision paths, partial rollout/downgrade restrictions and the risk of losing writes after the recovery point. Do not rewrite DbUp history or restore over later writes as an automatic fix.
- [ ] Exercise SQL offline/timeout/network interruption during reads and Post, including unknown commit outcome. Prove responsive UI, bounded read retries/cancellation, disposed UoWs, no duplicate movement and successful reconciliation after reconnect.
- [ ] Produce sanitized restore transcript, observed recovery time/data-loss window, operational connectivity/disk/journal/audit/reconciliation checklist and support runbook.

### REMAIN-P6-T07 — Nonfunctional and regression coverage

- [ ] Validate duplicate/concurrent Post, concurrent receipts on one balance key, UpdateDraft-versus-Post, rowversion, deactivation race, unique constraints, fault rollback and reconciliation including missing/extra zero rows.
- [ ] Exercise reproducible large catalog/receipt/card datasets, paging/order, rapid filters, background lookup refresh, navigation/disposal and slow/offline SQL; record volumes, environment and timings without inventing performance claims.
- [ ] Verify busy/empty/error/field-validation UX, cancellation/retry boundaries, no modal hangs, current identity/permission/rehash/redaction behavior, contracts, UoW/migrations and production DI/audit.
- [ ] Run the release's full active build/test/SQL and guarded GUI matrix on a frozen candidate under [TEST_POLICY](TEST_POLICY.md). Keep skipped/blocked cases visible; no Linux-only or developer-build inference of installed WinForms support.

### REMAIN-P6-T08 — Identity, support and owner sign-off

- [ ] Resolve legacy login support: approved real algorithm specifications/vectors with end-to-end verifier/rehash/failure evidence, **or** an explicit owner-approved exclusion and safe conversion/reset procedure. Fresh BCrypt tests do not establish legacy compatibility.
- [ ] State Local direct-SQL trust limits, same-process messenger, absent outbound sales/accounting/returns/valuation/offline sync/HTTP, and unsupported SQL/Windows topologies.
- [ ] Review actual release-date runtime/dependency support and update policy, package hashes/schema range, signing/trust-chain choice, upgrade/recovery notes and support ownership. Do not silently retarget.
- [ ] Complete all G1–G5 evidence and owner/date sign-off. Historical P4.1 idle-observation and NET8-PAUSE duration waivers do not close these gates.

| Gate | Required proof | State |
|---|---|---|
| G1 Deployment/schema | Accurate package/schema metadata, clean install, upgrade/no-op, old/new/unknown/partial schema, migration guard and recovery | OPEN |
| G2 SQL/data | Least privilege, no privileged desktop secrets, verified backup restore, concurrency/rollback/reconciliation | OPEN |
| G3 Security/diagnostics | Current SEC-REBUILD, non-NoOp audit, correct post-commit behavior and redacted correlated logs | OPEN |
| G4 Desktop/regression | Both pending P5 checks, packaged GUI and nonfunctional coverage on every shipped target | OPEN |
| G5 Identity/release | Current bootstrap/Login, approved legacy support or exclusion, support/runtime decision and release matrix/sign-off | OPEN |

Each row needs revision/artifact, OS/architecture/TFM, SQL version, method/command, actual result, sanitized evidence, tester and date. Until mandatory rows have evidence, v1 is not production-release accepted.

## 3. P7 — HTTP and remote client

- [ ] Decide the supported server/runtime matrix and follow [REACTIVATE_NET8](REACTIVATE_NET8.md) or a separately approved runtime plan. The parked product graph and the SDK used for net48 builds do not establish API readiness.
- [ ] Add Server.Api composition and endpoints for supported catalogs, receipts, stock inquiries and identity, reusing Application handlers and the accepted persistence implementation.
- [ ] Add bearer authentication, HTTPS, 401/403, Application permission enforcement, session expiry/revocation and safe status/ApiResponse/error mapping.
- [ ] Publish OpenAPI contracts with pagination/filter/order, decimal/UTC and error examples.
- [ ] Implement HttpApiClient with cancellation, bounded network handling, token/session-expiry UX and no Desktop dependence on SQL infrastructure.
- [ ] Prove Local/HTTP parity using the same DTO/JSON/validation/permission/draft/post/stock scenarios; SQL credentials and libraries stay in the server deployment, not the remote Desktop package.
- [ ] Provide deployment, correlation, upgrade/version and support guidance before declaring HTTP support.

## Cancelled tooling proposal — historical context

The owner completely removed the old SQL test framework on 2026-10-18. Its unimplemented STAB-T03/T04 fingerprint/manifest-v2 redesign is therefore **CANCELLED / SUPERSEDED**, not open deferred work. [Original proposal](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/20261017-001-testgate-stabilization.md) and [History](HISTORY.md) retain provenance only.

RESP adds fixture reuse inside existing SQL test projects and uses ordinary project/filter commands and test output. Do not recreate the removed framework as an active plan, a deferred prerequisite or a renamed support project.
