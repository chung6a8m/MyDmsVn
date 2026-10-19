# Codex instructions — MyDmsVn / v1a

Applies to the whole repository. Human-facing discussion is Vietnamese; technical documentation and identifiers are generally English unless an established runbook is Vietnamese.

## First read — current roadmap (2026-10-19)

The owner-selected [execution roadmap](docs/plans/README.md) contains two **completed** infrastructure plans followed by two **new OPEN** acceptance plans:

1. [RESP — Respawn fixture reuse](docs/plans/20261018-002-respawn-fixture-reuse.md) — **COMPLETE**, accepted against PR #36 merge `1e24df4`; preserve reset/seed/lifecycle proof and positively owned DB cleanup.
2. [L2DB — LINQ to DB persistence on net48](docs/plans/20261018-001-linq2db-net48-persistence-migration.md) — **COMPLETE in plan scope**, PR #37 merge `42d94ba`; active LINQ/native cutover and T07 narrow correctness/package/Local smoke accepted. [T07 evidence](docs/L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke).
3. [GUI-P5 — full WinForms GUI tests and P5 attended acceptance](docs/plans/20261019-001-full-winforms-gui-p5-acceptance.md) — **NEXT / OPEN**; existing full GUI suite, bounded subprocess handling, actual packaged Login E2E and two P5 walkthroughs.
4. [REL/P6 — Local v1 production release acceptance](docs/plans/20261019-002-local-v1-p6-release-acceptance.md) — **OPEN / NOT READY**; clean install, SQL/recovery, release matrix, full regression and G1–G5 sign-off. Preparatory tasks may overlap GUI-P5, but final G4 needs GUI-P5.

**Important:** L2DB-T07's 581 passing invocations **exclude** the 139 Desktop.GUI invocations (NOT RUN at that frozen revision). Its restricted packaged Local smoke does not close full GUI/P5/P6 gates. `RELEASE-MANIFEST-01` metadata inconsistency is already repaired (schema9/scripts001–009); full VM install/upgrade/recovery/sign-off is not. New plan checkboxes are open until actual evidence exists.

## Documentation authority

Read in order: (1) this file and [AI_CONTEXT](AI_CONTEXT.md), (2) [plan index](docs/plans/README.md) and active task, (3) sole [TEST_POLICY](docs/TEST_POLICY.md), [TEST_STRATEGY](docs/TEST_STRATEGY.md), [tests/README](tests/README.md), (4) relevant [Architecture](docs/ARCHITECTURE.md), [Persistence](docs/PERSISTENCE.md), [Contracts](docs/CONTRACTS.md), [Security](docs/SECURITY.md), [Data model](docs/DATA_MODEL.md), [PRD](docs/PRD.md) and runbooks. [History](docs/HISTORY.md) records superseded design/evidence; it is not an active policy.

Source code/actual project graph and explicit owner decisions override stale historical examples. Point out contradictions instead of silently reviving old modules/runner/permissions. Plan documentation alone is not runtime verification.

## Current code invariants

- Product, SQL, Desktop and all **ten** active test projects target `net48`. Shipped Windows x86 desktop `MyDmsVn.Desktop.App.exe` and separate admin `MyDmsVn.Server.DbMigrator.exe` are the only active release packages. AppCore `net8.0-windows` remains PARKED / NOT SHIPPED; the newer SDK is tooling, not product target.
- Local WinForms invokes Desktop.Application interfaces via in-process Local adapter and Server.Application. Domain/Application remain independent of Infrastructure/WinForms/HTTP. Public DTOs stay transport-neutral.
- Current persistence is LINQ to DB for Catalog/Inventory plus typed native SqlClient for other SQL consumers; explicit UoW owns one connection/current transaction, typed `unitOfWork.Repository<TRepository>()` resolves repository interfaces and no ambient/static/global UoW, hidden connections, fallback ORM or uncontrolled nested transactions are permitted. DbUp owns scripts/schema.
- Posting modifies receipt status, append-only ledger and derived balances atomically; optimistic rowversion and idempotent concurrent retries remain. Reconciliation and real SQL sessions are required independent business checks.
- Code-defined permissions require a registered Active key and current active identity; unknown/deprecated deny before superuser, configured superuser must match protected DB binding, ordinary direct Deny wins over role grants. No old permission sync, first-Administrator automatic grants or privileged client bootstrap.
- Preserve PasswordHash/PasswordSalt and algorithm-aware verification; legacy release support/exclusion requires explicit verified choice. Do not guess algorithms or include secrets in logs, package, screenshots or source.
- Admin-only migration/maintenance and limited SQL runtime identities stay separate. Desktop startup does read-only schema preflight and accepts current scripts001–009; admin migrator admits Empty/Compatible, **not** old-prefix auto upgrade. Local direct-SQL is an explicit security trust limitation.

## Implementation and test protocol

1. Declare current plan/task, exact changed behavior, target/provisioning requirements and smallest affected test scope.
2. Inspect actual checked-in source, csproj/lock files, package/GUI code and project tests before proposing new tools. No blanket upgrades, generic fallback DAL or unapproved AppCore reactivation.
3. Add tests for behavior/error boundaries; run focused build/project/filter during each edit. Expand for shared UoW/schema/security/fixture/GUI changes, and run full release matrix only for milestone acceptance. [TEST_POLICY](docs/TEST_POLICY.md) governs.
4. Use ordinary `dotnet test`/Test Explorer, direct projects and actual TRX. The old `tests/test-gates.v1.json`, `SqlTestHarness`, SQL runner/tests, approval files, global lease and manifest fingerprint tooling were intentionally **removed**. Do not recreate them.
5. SQL: configure explicit designated non-production `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective master, run SQL projects sequentially, verify database identity/ownership token before cleanup, drain workers/connections before reset/drop, quarantine uncertain allocations. Never bulk-drop by prefix or use application databases.
6. GUI: require interactive Windows session, STA serialized forms, `WinFormsTestGuard` ThreadException/DataError capture, bounded waits and disposal. `StaTest` cannot kill a noncooperative STA; fatal/hang/real-package probes must use a GUI-owned bounded subprocess and verified child cleanup. No background modal hangs; never equate form creation or test-session injection with real Login.
7. Record commit/package/hash, environment, selected/discovered/executed Facts/Theory rows, pass/fail/skip/blocked/timeouts, resource ownership/cleanup, sanitized log/screenshot evidence, tester/date and remaining risks. Do not mark deferred/open/release gates accepted based on prose or old totals.

## Editing and release boundaries

Follow `.editorconfig` and `.gitattributes`: Markdown UTF-8 without BOM with CRLF in the working tree (Git-normalized blob); C# uses its declared BOM/CRLF. Preserve Vietnamese diacritics and pinned vendor/submodule files. Do not commit credentials, local connections, proprietary raw TRX/SQL IDs or unredacted screenshots.

GUI-P5 and REL must preserve production contracts rather than implement P7/HTTP, new stock features, MSI/auto-updater, new migration framework or net8 reactivation. Record owner choices for SQL/Windows support, signing, legacy conversion and recovery without guessing. Missing Windows/SQL/resources or skipped mandatory cases = BLOCKED / NOT RUN. P5 GUI acceptance and P6 G1–G5 remain OPEN until evidenced and signed; v1 is **not ready for production**.
