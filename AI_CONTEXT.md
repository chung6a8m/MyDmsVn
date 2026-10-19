# MyDmsVn — Current context for Codex

**Branch:** `v1a` · **Refreshed:** 2026-10-19 · **Planning source:** v1a PR #37 merge `42d94baea1655dddbd46f971e806f84401cd1c63` plus the new GUI-P5/REL documents. This file is a context handoff, **not** runtime release acceptance.

## Goal and actual work queue

Build a modular, Windows desktop-first SME Local v1: Product/Warehouse/Employee/Customer catalogs, Goods Receipt Draft/Post, stock ledger/balances/card and configured-superuser security. Human conversation is Vietnamese; most technical documents/identifiers are English. HTTP/P7, AppCore .NET 8 and expanded sales modules remain deferred.

The [execution index](docs/plans/README.md) has four ordered plans:

1. **RESP — DONE/accepted:** [Respawn test fixture reuse](docs/plans/20261018-002-respawn-fixture-reuse.md), PR #36 merge `1e24df4`. Owns test-only reusable SQL fixture/reset/seed, dedicated fresh exceptions, worker drain/poison/positive cleanup. [RESP evidence](docs/RESPAWN_VERIFICATION.md#pr-36-review-follow-up).
2. **L2DB — DONE/accepted in its scope:** [net48 LINQ to DB persistence](docs/plans/20261018-001-linq2db-net48-persistence-migration.md), PR #37 merge `42d94ba`. Active Catalog/Inventory use LINQ to DB, Identity/Security/operations use typed native SqlClient; RepoDb/Dapper dependencies are gone from active product/test graph; DbUp and contracts stay unchanged. T07: 581 passing non-GUI invocations, clean package and limited authenticated packaged Local smoke; [evidence](docs/L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke).
3. **GUI-P5 — NEXT / OPEN:** [Full WinForms automated tests and P5 attended acceptance](docs/plans/20261019-001-full-winforms-gui-p5-acceptance.md). T00 inventory/topology and T01 bounded reliability are complete ([T01 evidence](docs/GUI_P5_VERIFICATION.md)); current Desktop153/153 and Session108/108 x86 results are task-scoped, not final P5 acceptance. Next T02: cover all in-scope UI paths, build real packaged-login E2E with independent SQL oracles and complete `REMAIN-P5-GUI-01` and `-02` attended walkthroughs.
4. **REL/P6 — OPEN / release NOT READY:** [Local v1 operational/release acceptance](docs/plans/20261019-002-local-v1-p6-release-acceptance.md). Clean-VM deployment, SQL/runtime least privilege, schema/client upgrade boundaries, backup/restore, failure recovery, audit/diagnostics, full ten-owner net48 regression, support/legacy/signing decisions and named G1–G5 sign-off. Prep may overlap GUI-P5; final REL G4 requires GUI-P5.

No GUI-P5 or REL task was executed by creating these plans. **L2DB-T07 left all 139 Desktop GUI invocations NOT RUN (conditional) at its frozen revision**. Its narrow real Local smoke is not the full GUI/P5/P6 release gate. Release metadata mismatch `RELEASE-MANIFEST-01` is **closed narrowly** for packaged schema9/scripts001–009, not a full release PASS.

## Current product and tests

- Product, both shipped packages and all **ten active** test projects use `net48`; desktop and separate admin migrator publish x86 Windows directories. `MyDmsVn.Desktop.AppCore` / `net8.0-windows` is parked outside solution/release. The .NET 8 SDK resolves build tooling only.
- Clean Architecture, explicit UoW repository resolution and single connection/transaction ownership, MediatR, ErrorOr, validation, SQL Server, DbUp, LINQ to DB plus native SqlClient. No ambient/static UoW/provider fallback. Desktop.Local dispatches Application in process; it is not a trusted remote-service security boundary.
- Permissions come from code-defined Active keys, deliberate SQL role/user assignments and a single configured database-bound superuser. Unknown/deprecated keys deny even for superuser; ordinary direct-user Deny overrides role grants. PasswordHash/PasswordSalt and approved legacy algorithm boundary remain. P3.1 auto-sync/auto-Administrator granting is retired.
- P5 Draft/Post writes receipt, append-only ledger and balances atomically. Duplicate/concurrent Post cannot double-count; stale rowversion fails. Independent SQL sessions prove concurrency; no GUI mock establishes SQL atomicity.
- Respawn reusable fixtures default to reuse for selected data scenarios, explicit `MYDMSVN_SQL_TEST_FIXTURE_MODE=fresh` is retained. Reset/seed/verify only inside positively owned disposable databases, with worker drain and terminal quarantine; schema, principal, trigger and destructive lifecycle exceptions remain fresh. Do not turn a release rehearsal database into a Respawn fixture.
- The former SQL harness, runner, test-gates manifest, wrappers and approval-file flow are **deleted**. Run ordinary `dotnet test` / Test Explorer by project/`FullyQualifiedName`/category, serial SQL owners with `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` on operator-approved nonproduction `master`. Missing configuration/skipped SQL = BLOCKED, not PASS.
- GUI: `tests/MyDmsVn.Desktop.Tests` uses STA, `WinFormsTestGuard` and assembly serialization; fatal/hang probes require bounded subprocess because `StaTest` cannot kill a noncooperative STA. GUI-P5-T01 replaces the unbounded packaging child wait with suspended/job-owned subprocesses, finite execution and verified tree/pipe drain; owned probes exercise modal/crash/hang paths. Real installed GUI requires interactive Windows.
- Production Desktop startup only accepts exact compatible schema scripts001–009 and does not run DbUp. Admin migrator admits Empty/Compatible only; older 001–008 prefixes are **not** supported upgrade/resume paths. P6 must validate safe stop/drain and correct metadata rather than claim mixed-version upgrade.

## Execution, priorities and acceptance

First read [AGENTS](AGENTS.md), [plan index](docs/plans/README.md), the current task, [TEST_POLICY](docs/TEST_POLICY.md) and [tests/README](tests/README.md). Use [TEST_STRATEGY](docs/TEST_STRATEGY.md) for behavior coverage. Perform focused tests during edits, broaden only for shared risk and named milestones; GUI-P5/REL acceptance explicitly require full relevant suites and real packaged Windows evidence. Maintain safe test DB ownership and cleanup; never bulk-drop a prefix or invent PASS from a developer build.

Report affected implementation, exact source revision, selected/discovered/executed test cases and Theory rows, result/skip/blocked counts, Windows/TFM/package/SQL details, redacted UI/log evidence, resource cleanup and blockers. Release G1–G5 must have dated sign-off; earlier P4.1 idle-observation and NET8-PAUSE benchmark waivers do not remove release blockers.

## Active links

[GUI-P5](docs/plans/20261019-001-full-winforms-gui-p5-acceptance.md) · [REL/P6](docs/plans/20261019-002-local-v1-p6-release-acceptance.md) · [Release packaging](docs/LOCAL_RELEASE_PACKAGING.md) · [Schema runbook](docs/SCHEMA_COMPATIBILITY_AND_MIGRATION_RUNBOOK.md) · [Security](docs/SECURITY.md) · [Deferred P7/net8/modules](docs/DEFERRED_WORK.md) · [History](docs/HISTORY.md).
