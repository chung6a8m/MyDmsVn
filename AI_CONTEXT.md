# MyDmsVn — Current context for Codex

**Branch:** `v1a` · **Context reset:** 2026-10-18 · **Context base:** docs refresh `a5ee124bed72f2994c4ee712e660b91d0dc2f953`, followed by the owner's SQL framework removal.

## Goal and next work

Build a Windows desktop-first SME modular monolith. Local v1 is the current product architecture; HTTP v2 is deferred. Discussions with the owner are Vietnamese.

Exactly two plans are active:

1. [RESP — reusable SQL fixtures with Respawn](docs/plans/20261018-002-respawn-fixture-reuse.md): RESP-T00–T05 accepted for the exact local working-tree baseline; full sequential fresh/reuse each 265 passed, zero failed/skipped, successful cleanup and observed suite improvement; default reuse and explicit fresh verified. L2DB-T00 now binds the accepted bytes to PR #36 merge `1e24df4c4ad5243963da82d808da19f12278242b`; see [binding/inventory](docs/L2DB_VERIFICATION.md).
2. [L2DB — persistence migration to LINQ to DB](docs/plans/20261018-001-linq2db-net48-persistence-migration.md): L2DB-T00–T01 complete: source baseline bound and internal net48 session/package ownership verified. T02 three-slice pilot decision is NO-GO ([evidence](docs/L2DB_VERIFICATION.md#l2db-t02--three-slice-pilot-no-go)); the missing-target reader gap is repaired with strict User/Role parity ([repair](docs/L2DB_VERIFICATION.md#l2db-t02-blocker-repair--l2db-pilot-01)); successful least-privileged package startup is still required before GO/T03–T06. Production DAL migration is not implemented. Accepted RESP is committed in PR #36; freeze its fixture behavior.

The file numbering is chronological; execution order is defined by [the active index](docs/plans/README.md), not lexical filename sorting. Keep the accepted fixture/reset baseline fixed during the DAL migration so failures and timings can be attributed.

## Actual baseline

- Product, SQL, Desktop and all ten active test projects target `net48`. The .NET 8 SDK is a build dependency; no standalone SQL tool project remains.
- AppCore (`net8.0-windows`) and historical product .NET 8 cases are parked, outside the active release. Do not reactivate them for Respawn or LINQ to DB.
- Production persistence still uses RepoDb/Dapper with explicit UoW and DbUp. The internal LINQ to DB session/package seam is installed and verified; T02 adapters remain test-only, with rollout blocked by its NO-GO decision.
- SQL tests run directly through `dotnet test`/Test Explorer and a local fresh-database helper in IntegrationTests. The old gate manifest, standalone SQL support/runner projects, their tests and wrappers are removed. RESP-T01 pins Respawn 4.0.0 in the test graph and verifies an owned full-schema reset profile with SqlClient 7.0.2 on net48. RESP-T02 adds exclusive collection scopes, tracked cleanup/poisoning and the test-only MYDMSVN_SQL_TEST_FIXTURE_MODE setting. T03–T04 adopt Catalog 5, Inventory 7 and Identity/Security 8 original data-only cases. Accepted profiles now default to reuse; explicit fresh remains supported. Schema/principal/bootstrap/trigger/contention exceptions keep independent fresh databases, including mixed classes through lazy collection initialization. RESP-T05 accepts the exact local candidate in docs/RESPAWN_VERIFICATION.md; the repaired accepted bytes are now bound to PR #36 merge by L2DB-T00; no GUI/product release acceptance is implied.
- Modular test projects, NET8-PAUSE and SEC-REBUILD are implemented. Do not redo those projects.
- Permissions come from code-owned definitions, explicit user/role assignments and one configured database-bound superuser. Permission-sync and first-Administrator auto-grant flows are retired.
- P5 catalogs, receipt Draft/Post and ledger/balance/card workflows exist. P5 interactive acceptance and P6 operational/release gates remain open and deferred.

## Preserve

Explicit connection/transaction ownership; Application authorization; unknown/deprecated-key denial; current superuser protection; legacy password compatibility; atomic and idempotent posting; immutable posted receipts/ledger; transport-neutral contracts; least-privilege runtime SQL and administrator-only migrations.

[TEST_POLICY.md](docs/TEST_POLICY.md) is the new sole test-execution policy. Select focused tests by affected behavior; broaden for shared infrastructure and named acceptance milestones. Use ordinary project/category/method filters and actual TRX cases/outcomes. Only `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` configures SQL tests; explicit master/instance/allocation ownership checks remain local. Run SQL projects sequentially, review interrupted allocations manually and do not restore the removed framework.

## Known open issue

The release script now reads schema support/required scripts from the shipped runtime policy (001–009) using an x86 metadata helper. Focused packaging tests pass; clean-commit artifact verification is pending in [L2DB evidence](docs/L2DB_VERIFICATION.md#l2db-t02-startup-follow-up--runtime-identity-and-schema-manifest). Runtime startup still needs a suitable explicitly authorized identity; T02 has no GO yet.

Read [AGENTS.md](AGENTS.md) first. [Documentation index](docs/README.md) links current references. [History](docs/HISTORY.md) is provenance only; historical totals and waivers do not prove either active plan has passed.
