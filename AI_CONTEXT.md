# MyDmsVn — Current context for Codex

**Branch:** `v1a` · **Context reset:** 2026-10-18 · **Context base:** docs refresh `a5ee124bed72f2994c4ee712e660b91d0dc2f953`, followed by the owner's SQL framework removal.

## Goal and next work

Build a Windows desktop-first SME modular monolith. Local v1 is the current product architecture; HTTP v2 is deferred. Discussions with the owner are Vietnamese.

Exactly two plans are active:

1. [RESP — reusable SQL fixtures with Respawn](docs/plans/20261018-002-respawn-fixture-reuse.md): next work, not implemented.
2. [L2DB — persistence migration to LINQ to DB](docs/plans/20261018-001-linq2db-net48-persistence-migration.md): starts after RESP acceptance, not implemented.

The file numbering is chronological; execution order is defined by [the active index](docs/plans/README.md), not lexical filename sorting. Keep the accepted fixture/reset baseline fixed during the DAL migration so failures and timings can be attributed.

## Actual baseline

- Product, SQL, Desktop and all ten active test projects target `net48`. The .NET 8 SDK is a build dependency; no standalone SQL tool project remains.
- AppCore (`net8.0-windows`) and historical product .NET 8 cases are parked, outside the active release. Do not reactivate them for Respawn or LINQ to DB.
- Production persistence still uses RepoDb/Dapper with explicit UoW and DbUp. LINQ to DB is an approved destination, not installed implementation.
- SQL tests run directly through `dotnet test`/Test Explorer and a local fresh-database helper in IntegrationTests. The old gate manifest, standalone SQL support/runner projects, their tests and wrappers are removed. Respawn is not installed. The Respawn plan selects a net48-compatible package/API and validates it before migration.
- Modular test projects, NET8-PAUSE and SEC-REBUILD are implemented. Do not redo those projects.
- Permissions come from code-owned definitions, explicit user/role assignments and one configured database-bound superuser. Permission-sync and first-Administrator auto-grant flows are retired.
- P5 catalogs, receipt Draft/Post and ledger/balance/card workflows exist. P5 interactive acceptance and P6 operational/release gates remain open and deferred.

## Preserve

Explicit connection/transaction ownership; Application authorization; unknown/deprecated-key denial; current superuser protection; legacy password compatibility; atomic and idempotent posting; immutable posted receipts/ledger; transport-neutral contracts; least-privilege runtime SQL and administrator-only migrations.

[TEST_POLICY.md](docs/TEST_POLICY.md) is the new sole test-execution policy. Select focused tests by affected behavior; broaden for shared infrastructure and named acceptance milestones. Use ordinary project/category/method filters and actual TRX cases/outcomes. Only `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` configures SQL tests; explicit master/instance/allocation ownership checks remain local. Run SQL projects sequentially, review interrupted allocations manually and do not restore the removed framework.

## Known open issue

The checked-in release script still emits schema support `8..8` while runtime `SchemaCompatibilityPolicy.CurrentScripts` includes `001–009`. [Deferred work](docs/DEFERRED_WORK.md) records this release-blocking discrepancy; this documentation reset does not fix the script.

Read [AGENTS.md](AGENTS.md) first. [Documentation index](docs/README.md) links current references. [History](docs/HISTORY.md) is provenance only; historical totals and waivers do not prove either active plan has passed.
