# REL — Local v1 P6 hardening and production release acceptance

**Created:** 2026-10-19 · **Branch:** `v1a` · **Status:** PLANNED / OPEN, **NOT READY FOR PRODUCTION**. Planning does not constitute an acceptance waiver.
**Planning baseline:** `42d94baea1655dddbd46f971e806f84401cd1c63` after RESP/L2DB completion. Requires [GUI-P5](20261019-001-full-winforms-gui-p5-acceptance.md) acceptance for final G4. Independent P6 preparatory work may proceed in parallel, but final release cannot.

## 1. Purpose, scope and evidence authority

Close remaining P6 operational and release acceptance: `REMAIN-P6-OPS-01`; `REMAIN-P6-T05` to `T08`; and owner sign-off for **G1–G5**. Retain behavior from the actual net48 Local host, [PRD](../PRD.md), [release packaging](../LOCAL_RELEASE_PACKAGING.md), [schema/migration runbook](../SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md), [SQL security configuration](../CONFIGURATION_AND_SQL_SECURITY.md), [security runbook](../SECURITY_ADMINISTRATION_RUNBOOK.md), [diagnostics runbook](../LOCAL_DIAGNOSTICS_AND_TROUBLESHOOTING.md), [TEST_POLICY](../TEST_POLICY.md) and the current source. Older [P6 archived design](https://github.com/chung6a8m/MyDmsVn/blob/c7ad32a4b0040f87417973eabd6c12757c54f7d5/docs/plans/archive/20261008-007-phase-6-local-hardening.md) is a requirements inventory only; superseded execution instructions do not apply.

**Completed, narrowly:** RESP fixture reuse accepted; L2DB-T00–T07 changed active persistence to LINQ to DB/native SqlClient and accepted scoped correctness/package/limited Local smoke. The `RELEASE-MANIFEST-01` schema mismatch was fixed with packaged schema 9 and script list 001–009 (clean-commit reconciliation). Do not repeat or reopen that fixed metadata bug absent a new regression. This did **not** establish install/upgrade/recovery/full GUI/production release acceptance.

## 2. Immutable release-scope facts and safety rules

- Active shipped artifacts from [Publish-LocalRelease.ps1](../../deployment/release/Publish-LocalRelease.ps1): `desktop-net48-win-x86/MyDmsVn.Desktop.App.exe` and **separate admin-only** `admin-migrator-net48-win-x86/MyDmsVn.Server.DbMigrator.exe`. Both are `net48`/x86 publish-directories; AppCore `net8.0-windows` is PARKED / NOT SHIPPED. The .NET 8 SDK is a build tool, not a shipped product runtime.
- Current runtime schema accepts **exact script history 001–009** and its invariant/artifact checks. The migrator admits **Empty** and **Compatible** state; an older 001–008 prefix currently fails as `Migration.UnsupportedExistingDatabase`. It has **no supported general upgrade/resume for older prefixes**. Never claim an upgrade path that the implementation refuses, rewrite DbUp journal, downgrade blindly or force mixed-client access. Use stop/drain maintenance or explicitly implement/test a future upgrade path under a separate approved change.
- Local desktop accesses SQL directly with restricted runtime authority. Administrator migrations, superuser/bootstrap and principal provisioning use an independent maintenance identity; desktop must never ship that credential or silently migrate. Local privilege constraints reduce but do not remove the hostile-client trust limitation.
- Code-defined permissions, deliberate grants/direct Deny, one configured and protected superuser, legacy password algorithm boundary and separate SQL/security audit semantics remain authoritative. Do not restore first-Administrator auto grants, persisted permission sync or fake “Administrator role = superuser.”
- Use **approved non-production** disposable databases, approved Windows VMs and permissioned SQL Server. A recovery drill has its own verified safe database and restored backup, **not** a reusable Respawn fixture. SQL tests remain positively owned, serial and direct `dotnet test`. No gate manifest, SQL runner, approval-file, cross-process lease, test-framework resurrection, production reset or prefix-only deletion.
- A PASS is tied to **exact candidate commit, build, package hashes and environment**. Changes to relevant source/schema/dependencies/package require focused re-evidence; unrelated Markdown edits do not invalidate runtime results.

## 3. Release acceptance tasks

### REL-T00 — Define release candidate, supported matrix and owner decisions

- [ ] Open `docs/LOCAL_V1_RELEASE_MATRIX.md` with one row per **claimed** Windows version/build/architecture, .NET Framework 4.8 state, x86 dependency, SQL Server version/edition/topology, authentication/network/TLS and installer/deployment variant. Every claim is marked VERIFIED, UNSUPPORTED or NOT TESTED. Do not advertise untested SQL/Windows combinations or treat SQL dialect `v2012` selection as a SQL support guarantee.
- [ ] Choose/record required supported prior-client and schema upgrade combinations, supported downtime/stop-drain policy, release candidate version, support/patch owner, security update/lifecycle policy, distribution/trust/signing policy, backup owner/RPO/RTO, legacy password A/B decision and acceptable known limitations. Distinguish a decision from evidence that the decision is deliverable.
- [ ] Review exact current source packaging, admin command/configuration precedence, SQL grants, schema9 manifest, startup failure codes and owner-approved feature exclusions. Create an explicit mapping from every G1–G5 row to tasks, scenarios, operators and evidence.
- [ ] Maintain a release blocker register. If essential Windows/SQL/tester/backup resources are unavailable, label BLOCKED / NOT RUN and **do not** convert a scope decision into a test PASS.

**Exit:** reviewed supported matrix, owners, decisions and acceptance/rollback boundaries; no claim of release readiness.

### REL-T01 — Reproducible package and clean installation (G1/G5)

- [ ] Freeze a clean commit, use locked restore and the current [publisher](../../deployment/release/Publish-LocalRelease.ps1) in Release, record resolved SDK, net48 targeting, emitted x86 desktop/admin directories, version/sourceCommit/clean-tree marker, schema support/001–009 and **all** file SHA-256/lengths. Refuse missing, mismatched or tampered files, mismatched runtime/schema policy and unexpected admin/migration assets in desktop.
- [ ] On **clean approved Windows VM(s) without Visual Studio or SDK**, install/copy actual publish-directory deployment, configure secured runtime SQL settings outside binaries, provision separately with admin identity, start as **limited user**, log in and exercise the relevant smoke. Inspect missing/native DLL, redirects, printer/UI dependencies only if actually used, log ACLs and required .NET 4.8 runtime.
- [ ] Prove versioned-directory install, same-schema supported update or side-by-side replacement, restart, repair/reinstall, uninstall/shortcut cleanup, config/log/database preservation and no unintended elevation. The baseline is not MSI/Onova/auto-update; do not invent a new installer feature as proof.
- [ ] Verify signer/trust-chain policy if signing is required; if unsigned distribution is approved, record explicit accountable owner decision, threat implications and distribution controls. Package checksums alone do not prove publisher identity.

**Exit:** sanitized clean-machine install/upgrade/uninstall transcript per claimed target; positive package/source/metadata check; otherwise G1 OPEN.

### REL-T02 — Schema, release skew and controlled migration (G1/G2)

- [ ] Test packaged Desktop read-only preflight and maintenance CLI separately: Empty DB, exact Compatible DB/no-op, older 001–008 prefix, missing/unknown/future/forked journal, partial schema/triggers/constraints, wrong/system database and denied migration permission. Inspect safe codes and prove **zero business mutation on preflight failure**.
- [ ] Exercise exclusive migration owner/lock, two competing admin invocations, killed/crashed migrator and failure after an earlier script committed, using specially created safe disposable databases. Existing `WithTransactionPerScript()` cannot guarantee all-script atomicity; document and verify safe stop/recovery behavior instead.
- [ ] Test two installed **client versions** only where supported artifacts exist; establish actual DB/client compatibility. If no older release can operate on schema9, record **unsupported mixed-version access** and require stop/drain maintenance. A legacy 001–008 upgrade must stay BLOCKED until a separate supported path exists; don't declare it “passed” by refusal.
- [ ] Review backup preflight, operator authority, grant ordering and runtime vs admin credential/config separation. Ordinary startup/install must not DbUp/bootstrap or alter journal/permissions.

**Exit:** observed schema/version decision matrix, tested current-state/no-op/refusal/migration-fault behavior, no unsafe mixed client and clear supported upgrade boundary.

### REL-T03 — Runtime least privilege, credentials and diagnostics (G2/G3)

- [ ] Validate actual restricted SQL runtime account against all four catalogs, receipt Draft/Post, stock inquiry and security administration/import; independently assert **denied** DDL, DbUp journal changes, protected binding, principal/role escalation and ungranted data access. Privilege denial leaves business tables unchanged.
- [ ] Exercise missing/invalid configuration, secret file ACL, credential rotation/restart, SQL auth vs Windows auth only if claimed, TLS/network/certificate invalidity, SQL unavailable and incomplete schema on an installed limited-user desktop. The maintenance identity cannot leak into runtime installation, parameters, logs or diagnostics.
- [ ] Test runtime audit/log sink positive events and redacted correlation for login, Denied, Post-success after durable commit, Post failure and security administration/import. Distinguish workstation file audit (best effort, not tamper proof) from atomic SQL `SecurityAuditEvents` for admin mutations.
- [ ] Exercise denied log folder, locked sink, exhausted disk/quota and sink failure **after** a committed business operation, plus startup fail policy: bounded/nonmodal, accurate user-facing state (do not falsely report rollback), meaningful support code and no sensitive payload. Record fallback, loss-of-audit risk and recovery steps.
- [ ] Scan real release artifacts, configured VM and exported sanitized logs for passwords, hash/salt, raw SQL parameters, full connection strings, auth/session data and privileged credential artifacts.

**Exit:** runtime principal negative/positive proof, safe credential lifecycle and diagnostics/audit fault evidence; otherwise G2/G3 OPEN.

### REL-T04 — Verified backup restore and operational failure recovery (G1/G2)

- [ ] Assign a DBA/operator, backup location/security/integrity/retention, expected RPO/RTO and a **pre-upgrade backup** for a designated rehearsal database. Successfully **restore into a separate safe database** and independently check identity/schema/ledger/business state before any failure drill.
- [ ] Rehearse notify → stop/drain clients and in-flight Post → lock and migrate under maintenance principal → schema/history/grant checks → deploy matched client → smoke/reconcile → reopen. Exercise failure after a committed script, crash/killed/concurrent migrator and an old client left open.
- [ ] Prove and document a supported **forward-fix or operator-authorized restore** path, measured recovery time and data-loss window, downgrade and partial-rollout restrictions. Never re-run unsupported older-prefix upgrades blindly, edit journal as a fix, restore over later writes automatically or suggest DB restore is lossless.
- [ ] Rehearse SQL offline, timeout, network interruption during safe read and Post (including **unknown commit outcome**): bounded responsive UI, cancellation/connection/UoW drain, reload authoritative receipt, no automatic mutating retry, zero duplicate movement and reconciled balances after reconnect.
- [ ] Deliver a sanitized operator transcript/runbook for backup validation, migration failure, connection/permission/space/journal/audit health, stock reconciliation, restore decision and support escalation.

**Exit:** real restored backup + reproducible failure/recovery evidence, actual RPO/RTO observations and safe data outcomes; no proof = G2 OPEN.

### REL-T05 — Nonfunctional, regression and complete release test matrix (G2/G4)

- [ ] Define repeatable large catalogs/receipts/stock-card datasets, expected query ordering/paging, interaction/DPI/theme and measurable response/busy/timeout boundaries. Record machine, SQL size/version, query/result sizes and wall times; do not invent throughput/latency promises.
- [ ] Verify concurrent/duplicate Post, two receipts against one balance key, Draft-vs-Post conflict, stale rowversion, deactivation/unique races, injected rollback and ledger/balance reconciliation including missing/extra zero rows, against real isolated SQL with independent sessions. Reuse accepted RESP only for eligible SQL tests; migration/principal/contention exceptions stay separately isolated.
- [ ] On the final **frozen commit**, use direct locked-restore `dotnet test` of all **ten active net48 test projects**: six SQL owners sequentially, Application, Architecture, Desktop.Session, and full Desktop.GUI on an interactive Windows session. GUI-P5 supplies full packaged E2E + attended sign-offs. Reconcile source-discovered Facts/Theory rows against actual TRX pass/fail/skip/abort and cleanup; missing SQL env, GUI or any mandatory row blocks release.
- [ ] Re-run only affected scope for local fixes under TEST_POLICY; after a relevant candidate change, update the affected release evidence before sign-off. Keep independent GUI/backend oracle conclusions separate. Do not recreate the removed Final gate/runner/test manifest.

**Exit:** complete reproducible release matrix for supported target(s), no mandatory skip/unknown result, successful resource cleanup and measured nonfunctional evidence; G4 requires GUI-P5-T06.

### REL-T06 — Legacy identity, support, risk review and sign-off (G3/G5)

- [ ] **Explicit owner choice:** A. approved real legacy hash algorithm/specification and sanitized vectors, tested verifier/rehash/failure paths; **or** B. documented exclusion of legacy-account login with safe controlled conversion/reset and accepted customer impact. Fresh BCrypt tests **do not** establish legacy compatibility; unsupported algorithms fail closed.
- [ ] Record actual .NET Framework 4.8/Windows/dependency lifecycle and patch ownership for the release date, supported SQL/auth/topology, distribution/signature decision, support contact, rollout/change/rollback instructions and production incident ownership.
- [ ] Disclose Local direct-SQL trust limitation; same-process-only `CatalogChangedMessage`; no outbound sales/orders, invoicing, returns, valuation, offline sync or HTTP adapter; no AppCore/net8 package.
- [ ] Assemble `docs/LOCAL_V1_RELEASE_VERIFICATION.md` with current candidate commit/artifact hashes, support matrix, all G1–G5 scenario rows, commands/outcomes, package/environment/version, attended GUI/SQL/recovery evidence, limitations and acceptance/blocker decision.
- [ ] Obtain **named product/security/DBA/release owner and date** sign-off for each gate, accepted feature exclusions and remaining risk. An exception may reduce the *advertised support scope*, but cannot silently convert a mandatory data-safety, security or restore gate to PASS.
- [ ] Mark v1 production-release accepted **only** when all supported mandatory rows are verifiably passed and owners approve. Otherwise publish explicit **NO-GO / NOT READY** with blockers; do not hide blocked/skipped cases or change status on documentary intent alone.

**Exit:** signed/approved release evidence and all G1–G5 PASSED, or NO-GO with open blockers.

## 4. Gate matrix / release evidence requirements

| Gate | Mandatory proof and tasks | Initial state |
|---|---|---|
| **G1 — Deployment/schema** | T00–T02,T04: clean VM install/upgrade, verified manifest and runtime schema contract, separate guarded migrator, unsupported-version refusal, controlled migration failure/restore | OPEN |
| **G2 — SQL/data** | T02–T05: least privilege, privilege negative tests, verified **separate-database backup restore**, concurrency, atomic Post/rollback and reconciliation | OPEN |
| **G3 — Security/diagnostics** | T03,T06: current SEC-REBUILD grants/binding, safe Login/security administration, redacted correlation, non-NoOp sinks, post-commit/SQL audit behavior | OPEN |
| **G4 — Desktop/regression** | [GUI-P5](20261019-001-full-winforms-gui-p5-acceptance.md) T00–T06 and REL-T05: both P5 attended IDs, full automated GUI and actual packaged E2E, regression/slow/offline SQL on all shipped targets | OPEN |
| **G5 — Identity/release** | T00,T01,T06: bootstrap/login, legacy A or approved B, support/dependencies/trust-chain policy, version/package/source evidence, risk and owner/date sign-off | OPEN |

Each scenario in the release verification matrix must record: gate/task, supported configuration, candidate SHA/manifest/file hash, environment (OS/build/arch/TFM/.NET/SQL version), actual command/procedure and observed result, number of selected/discovered/executed tests where applicable, PASS/FAIL/BLOCKED/NOT RUN, sanitized evidence link, cleanup, operator and timestamp. Raw TRX, SQL IDs, secrets and screenshots containing personal data remain local and redacted before sharing.

## 5. Implementation order and release-stop rules

**Recommended sequencing:** REL-T00 can begin immediately; T01–T03 and GUI-P5 foundational work can progress independently on separate approved resources. T04 needs safe backup/restore ownership and a deliberate rehearsal DB. REL-T05 final frozen validation follows completed GUI-P5 acceptance and relevant P6 fixes. REL-T06 final signature follows all gates. Freeze/re-run only where changed behavior invalidates evidence.

**No-go** when: any required GUI/SQL test not run or skipped; credential/admin privilege leaks; uncertain ownership/process cleanup; unsupported upgrade being advertised; an unverified restore; unreconciled Post movement; corrupted/unknown schema accepted; missing audit/secret protection; or a missing accountable legacy/support decision. Historical P4.1 idle observation and NET8-PAUSE benchmark waivers do not waive any of these gates.

## 6. Exclusions and decisions to collect from the owner

The plan does **not** implement P7 HTTP/remote, reactivate net8, add MSI/autoupdater, replace DbUp, reintroduce permission synchronization or test runner, or authorize production database rehearsals. Scope options to approve before releasing (no default “support all” assumption):

1. **Legacy accounts:** A verified legacy migration/rehash; or B explicit no-legacy-login policy with conversion/reset and sign-off.
2. **Distribution trust:** signed executable/package with verified chain; or reviewed unsigned versioned-directory distribution with accepted controls and risk.
3. **OS/SQL support:** enumerate exact Windows builds, SQL Server versions/editions, authentication and network topologies; unverified variants remain unsupported.
4. **Service/recovery:** named DBA/support ownership, backup cadence and RPO/RTO goals, acceptable maintenance downtime and mixed-client policy.

Record the actual decision in T00; only successful T01–T06 verification can turn it into supported release scope.
