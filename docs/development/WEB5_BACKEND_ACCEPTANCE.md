# WEB5 backend acceptance matrix — operational safety and delivery

**Owner selected 2026-11-02. All B01–B06 PLANNED / NOT RUN.** Scenario IDs are requirements, **not** runnable test names or evidence. [Owning plan](../plans/20261102-001-web5-operational-safety-backend.md) · [protocol](../design/WEB5_OPERATION_SAFETY_PROTOCOL.md) · [handoff](WEB5_CODEX_HANDOFF.md) · [WEB4 historical evidence](../evidence/web4/README.md).

## Evidence levels

| Source | Proves | Does not prove |
|---|---|---|
| SQL-free HttpHost/validators + Windows CI | Strict contract/parser/error and selected regression | Transaction or full IIS |
| Actual owned SQL in Security/Inventory/Migrations | Journal/business atomics, grant and schema behavior | Browser UX or process death |
| Real killed test-only child process + independent SQL connection | Crash/Commit outcome classification, same-key fence | Customer production readiness |
| Real full IIS + fresh SQL + published React, Chromium/Edge | Actual deployment/runtime/proxy/business behavior | Attended IME/Narrator unless separately run |
| Operator rehearsal on disposable DB | Maintenance, tombstone, backup/restore, audit outcomes | A missing/dry-run command as PASS |
| Attended Windows/Edge/IME/AT | Candidate-specific input/accessibility | All environments by extrapolation |

## Traceable mandatory gate IDs

| ID | Owner | Required assertion |
|---|---|---|
| B5-CR-01 | B01 | External kill before SQL Commit across all six commands: no half-journal, source, ledger or valuation |
| B5-CR-02 | B01 | At Commit uncertain outcome, classify by fresh SQL truth instead of assuming rollback from timeout/child death |
| B5-CR-03 | B01 | Committed then host killed before HTTP response: protected result survives restart and no second business effect |
| B5-CR-04 | B01 | Delayed original and explicit same-key replay from independent workers: serialization and exactly one effect |
| B5-CR-05 | B01 | Changed fingerprint/target/version/actor/epoch or revoked permission: deterministic denial, no new effects/leak |
| B5-CR-06 | B01 | Expired tombstones, malformed keys and session revocation fail closed, no fresh-key bypass |
| B5-CR-07 | B01 | Kill/process tree and SQL resource ownership finite; uncertain teardown quarantines, never bulk prefix cleanup |
| B5-NEG-01 | B02 | Operator negative races late original under SAME transaction-owned actor/key lock: either COMMITTED or durable CANCELLED, never both |
| B5-NEG-02 | B02 | Row absence, status404, lock timeout, epoch/fingerprint mismatch and corrupt input never prove NOT_EXECUTED |
| B5-NEG-03 | B02 | Terminal tombstone + audited operator decision commit atomically; all later original/replay attempts rejected before business |
| B5-NEG-04 | B02 | COMMITTED, EXPIRED and result unavailable never converted to negative or edit unlock |
| B5-NEG-05 | B02 | Versioned read uses current authenticated actor/session/grant/epoch/fingerprint; cross-actor no disclosure |
| B5-NEG-06 | B02 | Explicit schema/migration/principal/trigger compatibility, frozen web4-v1 corpus, Desktop exact10, legacy11/web4 exact12 |
| B5-NEG-07 | B02 | Operator process death and lost audit/commit acknowledgement require independent reread; denied writes/no partial state |
| B5-OPS-01 | B03 | Exact-key operator CLI inspect/dry-run, authorized maintenance identity only, no runtime privilege or leaked secrets |
| B5-OPS-02 | B03 | Scoped compaction eligible only, permanent tombstone/epoch/fingerprint; audit failure rolls back |
| B5-OPS-03 | B03 | Consistent backup/restore clone and rotate epoch before re-admission, stale browser/key denied |
| B5-OPS-04 | B03 | Ordinary restart preserves epoch, partial clone/rotation failure stops admission |
| B5-OPS-05 | B03 | Human operator rehearses COMMITTED/EXPIRED/NOT_EXECUTED/INDETERMINATE ticket outcomes and cleanup |
| B5-CI-01 | B04 | Existing hosted secret-free Windows/net48 PR CI remains PASS, selected tests nonempty |
| B5-CI-02 | B04 | Protected isolated SQL lane repeatable, nonzero selected tests, 0 required skip, forced red failure blocks |
| B5-CI-03 | B04 | No fork secrets, sensitive artifact or unowned DB cleanup; quarantined uncertainty fails gate |
| B5-IIS-01 | B05 | Actual W3SVC/WAS full IIS and ASP.NET/net48, trusted TLS, exact artifact hashes, static assets/CSP nonce |
| B5-IIS-02 | B05 | Wrong mode/schema/principal/stale epoch fails closed; no legacy/unkeyed fallback |
| B5-IIS-03 | B05 | Proxy trusted headers, malicious Forwarded spoof, cookie/Origin/CSRF/refresh and dependency/XSS review |
| B5-IIS-04 | B05 | Shared atomic multiworker limiter proven OR enforced documented single-worker topology (not fake two-worker PASS) |
| B5-IIS-05 | B05 | Fresh full IIS + real SQL + separate Chromium/Edge, new recovery and existing stock/COGS physical oracle/cleanup |
| B5-IIS-06 | B05 | Audit destination/retention/access, restore/rollback and support procedures actually rehearsed |
| B5-REL-01 | B06 | Exact candidate source/tree/build/dependencies/schema/principal hashes and gate outcomes reconciled |
| B5-REL-02 | B06 | Operator/attended/front-end evidence, recovery/rollback and residual risks reviewed |
| B5-REL-03 | B06 | Explicit Owner production GO/NO-GO; otherwise NOT APPROVED |

## Commands and execution rules

Use [tests README](../../tests/README.md) and inspect actual project/TFM names each time. Examples only, **do not execute SQL from this docs-only activation**:

~~~powershell
dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger "trx;LogFileName=web5-host.trx"

# SQL: authorized disposable nonproduction DB only; tests run sequentially.
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter "FullyQualifiedName~WebOperationJournalTests|FullyQualifiedName~WebOperationHttpSqlTests|FullyQualifiedName~WebOperationReadTests" --logger "trx;LogFileName=web5-security.trx"
~~~

The dedicated SQL connection is `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` targeting approved nonproduction master. Existing SQL owners include Security `WebOperationJournalTests`, `WebOperationHttpSqlTests`, `WebOperationReadTests` and provisioner; Inventory `WebOperationCommandTests`; migration `WebAuthMigrationTests`. Do not run a broad project SQL suite unintentionally. Reconcile discovered/executed/passed/failed/skipped by actual TRX IDs; a 0-case filter is FAILURE. Drain fixture processes before teardown; verify owned allocation/SID; keep secrets, raw TRX/SQL identifiers, HAR, tokens and user data out of Git.

## Acceptance rules

B01 requires actual OS process kill, not just HTTP drop. B02 requires physical permanent terminal negative with late-original race (otherwise BLOCKED and UNKNOWN remains locked). B03 requires executable operator commands and rehearsal, not runbook only. B04 SQL CI needs an approved isolated trusted runner; absent environment = BLOCKED/NOT RUN. B05 actual full IIS cannot inherit IIS Express PASS. B06 cannot proclaim production GO without a dated explicit Owner signoff. Evidence owners append a dated `docs/evidence/web5/YYYYMMDD-bxx-...md` with SHA/runner/oracle/cleanup and RED→GREEN corrections; the [plan](../plans/20261102-001-web5-operational-safety-backend.md) owns state, not this scenario list.
