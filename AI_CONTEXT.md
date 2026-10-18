# MyDmsVn — Current context for Codex

**Branch:** `v1a` · **Context reset:** 2026-10-18 · **Source assessed:** `c7ad32a4b0040f87417973eabd6c12757c54f7d5`.

## Goal and next work

Build a Windows desktop-first SME modular monolith. Local v1 is the current product architecture; HTTP v2 is deferred. Discussions with the owner are Vietnamese.

Exactly two plans are active:

1. [RESP — reusable SQL fixtures with Respawn](docs/plans/20261018-002-respawn-fixture-reuse.md): next work, not implemented.
2. [L2DB — persistence migration to LINQ to DB](docs/plans/20261018-001-linq2db-net48-persistence-migration.md): starts after RESP acceptance, not implemented.

The file numbering is chronological; execution order is defined by [the active index](docs/plans/README.md), not lexical filename sorting. Keep the accepted fixture/reset baseline fixed during the DAL migration so failures and timings can be attributed.

## Actual baseline

- Application, SQL tests, Desktop and harness tests execute on `net48`. The shared harness is `netstandard2.0`; runner and runner tests alone execute on `net8.0`.
- AppCore (`net8.0-windows`) and historical product .NET 8 cases are parked, outside the active release. Do not reactivate them for Respawn or LINQ to DB.
- Production persistence still uses RepoDb/Dapper with explicit UoW and DbUp. LINQ to DB is an approved destination, not installed implementation.
- SQL tests currently allocate fresh disposable databases; Respawn is not installed. The Respawn plan selects a net48-compatible package/API and validates it before migration.
- Modular test projects, NET8-PAUSE and SEC-REBUILD are implemented. Do not redo those projects.
- Permissions come from code-owned definitions, explicit user/role assignments and one configured database-bound superuser. Permission-sync and first-Administrator auto-grant flows are retired.
- P5 catalogs, receipt Draft/Post and ledger/balance/card workflows exist. P5 interactive acceptance and P6 operational/release gates remain open and deferred.

## Preserve

Explicit connection/transaction ownership; Application authorization; unknown/deprecated-key denial; current superuser protection; legacy password compatibility; atomic and idempotent posting; immutable posted receipts/ledger; transport-neutral contracts; least-privilege runtime SQL and administrator-only migrations.

[TEST_POLICY.md](docs/TEST_POLICY.md) is the new sole test-execution policy. Select focused tests by affected behavior; broaden for shared infrastructure and named acceptance milestones. Existing runners still enforce their checked-in manifests until RESP updates them. A new plan ID in Markdown does not create a runnable manifest task.

## Known open issue

The checked-in release script still emits schema support `8..8` while runtime `SchemaCompatibilityPolicy.CurrentScripts` includes `001–009`. [Deferred work](docs/DEFERRED_WORK.md) records this release-blocking discrepancy; this documentation reset does not fix the script.

Read [AGENTS.md](AGENTS.md) first. [Documentation index](docs/README.md) links current references. [History](docs/HISTORY.md) is provenance only; historical totals and waivers do not prove either active plan has passed.
