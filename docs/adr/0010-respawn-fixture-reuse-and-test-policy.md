# ADR 0010 — Respawn fixture reuse and replacement test policy

**Status:** Accepted decision, selected 2026-10-18; RESP-T00–T05 implemented and accepted in recorded scope. [Current evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up).
**Scope:** Active net48 integration-test fixture policy; originally planned on `v1a`, retained on `v1a-pro`.
**Completed plan:** [RESP](../archive/20261023-pre-ui-next/plans/20261018-002-respawn-fixture-reuse.md). Use the [current execution index](../plans/README.md) for new work; completed BCI/BCF remain historical evidence for their own scopes.
**Execution policy:** [TEST_POLICY](../TEST_POLICY.md).

## Original context — 2026-10-18

This paragraph records the pre-implementation state; it is not current product or roadmap status. The completed RESP/L2DB evidence supersedes its pending-state statements.

The owner chose fixture reuse with Respawn and the existing LINQ to DB migration as the two active roadmap plans. The prior fresh-database strategy and test execution rules accumulated duplicate, stale instructions. Current product code still uses RepoDb/Dapper and SQL tests still create fresh allocations. The owner then removed the standalone SQL framework, gate manifest, runner, related tests and wrappers completely. Tests run directly with a local owned-database helper; reuse remains proposed.

## Decision

The order and acceptance conditions below record the original migration decision, now completed. Retain their ownership/reset/policy invariants; do not restart either plan.

1. Execute RESP first on the current DAL, then execute L2DB using the accepted fixture baseline. Separate reset/provider changes so failures and measurements remain attributable.
2. Reuse one migrated, owned disposable database within a named xUnit v2 collection. Reset and deterministically seed before every test; dispose test-owned transactions/connections/workers before the next reset.
3. Retain fresh allocations for schema/migration/history/principal/destructive-lifecycle tests and other cases without a proven complete reset. Reuse classification follows state mutations, not a category label alone.
4. Preserve local positive ownership, explicit non-production master configuration, finite waits, drained workers and guarded cleanup. Run SQL projects sequentially and retain existing assembly guards; Catalog's reusable collection must explicitly serialize its consumers. No external approval file, lease, watchdog or automatic orphan recovery remains. Interrupted or ambiguous allocations need manual operator review.
5. Replace the previous duplicated test execution documents with one TEST_POLICY. Choose scope by affected behavior/dependencies. Full SQL is required at RESP and L2DB acceptance because they change shared infrastructure; it is not automatically required after every edit, task or code PR.
6. Keep fixture/reset support inside existing test projects. Use direct project/filter commands, ordinary TRX case outcomes and small fixture-operation observations. Do not restore standalone support/runner projects, gate/invocation manifests or a reporting/reconciliation framework. Their unimplemented manifest-v2 redesign is cancelled.

## Compatible package and API

Respawn **4.0.0** is the explicit compatibility candidate: its [published package](https://www.nuget.org/packages/Respawn/4.0.0) contains net461/netstandard2.0 assets. The [maintainer's v5 migration guidance](https://github.com/jbogard/Respawn/releases) explains that .NET Framework consumers can remain on 4.0; later checked releases target netstandard2.1. Do not choose a newer incompatible package or reactivate net8 to accommodate it.

Use v4 `Checkpoint.Reset(DbConnection)`, not modern `Respawner.CreateAsync/ResetAsync` examples. With the verified dbo schema, `SchemasToInclude = new[] { "dbo" }` and `TablesToIgnore = new[] { "SchemaVersions" }` preserve the journal: v4 filters table names, not schema-qualified names. [Pinned v4 source](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/Checkpoint.cs), [SQL adapter](https://github.com/jbogard/Respawn/blob/4788e2c51535919221de03edc90d354f4fd7d02f/Respawn/SqlServerDbAdapter.cs).

The actual net48 host must validate this package with the existing Microsoft.Data.SqlClient 7.0.2 central pin, including runtime dependencies/binding redirects. Framework asset compatibility is not proof that the combination has executed successfully. No floating versions or broad package upgrades.

## Reset boundary

Respawn caches schema-derived SQL in its checkpoint; checkpoint/database/schema ownership is immutable after initialization. Preserve the migration journal and allocation ownership metadata only; clear/reseed mutable business, reference, security assignment/binding and audit data.

The current full schema has `TR_StockLedger_AppendOnly`, including in Catalog fixtures. A narrowly owned test-only reset profile must restore and verify that trigger before a test can run. An ignored ledger is not a complete reset. Sequence/identity contracts need explicit validation; rowversion is not rewound. Unreviewed schema drift, reset/seed failure or undrained workers poison the fixture and block subsequent test bodies.

Use xUnit2 collection `IAsyncLifetime` for async allocation/migration/disposal. Each Fact/Theory invocation explicitly enters the reset/seed adapter, with a non-sensitive method/row label for diagnostics, and drains its test scope in `finally`. Parameterless lifecycle initialization does not provide Theory-row arguments; no ambient identity or fingerprint registry is required. [xUnit shared-context guidance](https://xunit.net/docs/shared-context) and [parallelism guidance](https://xunit.net/docs/running-tests-in-parallel) distinguish in-collection serialization from assembly/process concurrency.

## Evidence and consequences

RESP validates contamination, journal/seed/trigger integrity, ownership, failed reset/cleanup and retained case identity on net48. Compare explicit fresh/reuse modes on the same frozen candidate with the same cases and reported create/migrate/reset/seed/drop timings. Accepted eligible profiles default to reuse after RESP acceptance; fresh mode remains explicit and required for excluded cases. No automatic fallback and no speedup claim without measured improvement; a single pair is not a statistical benchmark.

Reuse reduces repeated setup for eligible tests but introduces a larger failure boundary: a poisoned collection cannot safely continue. Fresh exception tests retain their existing isolation. The complexity is limited to helpers and adapters inside existing SQL test projects; production migrations, schema, permissions, DTOs and UoW behavior are unchanged by RESP.

## Supersession

This decision supersedes the old rejection of fixture reuse and ADR 0006's execution-policy proposal. The later owner instruction also removes the old executable framework; it is not retained as a transition requirement. [ADR 0005](0005-test-isolation-and-bounded-parallelism.md) retains its resource-isolation principle as refined here. Existing measured results and owner exceptions remain in [History](../HISTORY.md). GUI-P5 later completed its approved scope. [Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) remain unaccepted/unscheduled, and [P7/HTTP](../DEFERRED_WORK.md#out-of-scope-directions) remains out of scope.

