# SQLPERF-T04 fixture-reuse decision

**Date:** 2026-10-15  
**Revision reviewed:** `e53b5ff`  
**Decision:** Do not introduce database fixture reuse. Preserve one guarded disposable database per SQL test.

## Evidence

T01/T03 measurements show migration and database creation remain the largest measured setup stages, while the adopted T02/T03 candidate improved full-suite wall-clock by about 9–10%. This met the condition to evaluate T04, but it did not make fixture reuse safe.

The integration assembly contains 31 test classes and 210 xUnit methods before theory expansion. The source audit found 169 `SqlServerFact` methods and 41 fact/theory methods that are either pure tests or fail-closed harness tests. The SQL classes are not clean read-only fixtures:

| Category | Representative classes | State that prevents a safe generic reset |
|---|---|---|
| Migration/schema | `DatabaseMigrationTests`, `SchemaCompatibilityIntegrationTests`, `PermissionRegistryMigrationTests` | migration journal, schema objects, constraints, indexes, triggers, sequences and expected failed/partial upgrades |
| Security/provisioning | administrator, identity, permission synchronization and SQL-principal tests | users, roles, direct denies, permission tombstones, SQL principals and audit state |
| Multi-session concurrency | goods-receipt and concurrency-gate tests | live sessions, application locks, row/key locks, test triggers and failure cleanup |
| Committing storage/query | catalog, inventory, repository and transaction tests | committed rows, identities/sequences, ledger/balance and audit fields |
| Pure/non-SQL | password, mapping, policy and harness guard tests | no database exists to reuse; including these in a SQL fixture gives no benefit |

No SQL-backed class is both materially expensive and limited to pure reads or transaction-rollback-only behavior. A reset capable of restoring rows, identities, schema, permissions, triggers, migration history, ownership markers and sessions would duplicate the migration/schema verifier and still need adversarial equivalence proof. A blanket `DELETE`/`TRUNCATE`, ambient singleton, or shared database would violate the plan safeguards.

## Adversarial assessment

A fixture pilot would need to prove recovery after a failed assertion, abandoned transaction, cancellation, connection leak, failed reset, schema mutation and permission mutation before the next test starts. The current per-test guarded database lifecycle already supplies that boundary. There is no narrower fixture candidate whose reset surface is smaller than recreating the database, so an implementation pilot would add risk without a defensible isolation model.

## Result

- Rejected fixture reuse and shared/global mutable databases.
- Retained fresh guarded database ownership, durable journaling and assembly-level serialization.
- No production code, SQL migration, authorization behavior or test assertion changed.
- Proceed to SQLPERF-T05 focused categories, which improves developer iteration without weakening the unfiltered release gate.

The 20–30% engineering goal remains unmet; safety takes precedence over an additional speculative setup optimization.
