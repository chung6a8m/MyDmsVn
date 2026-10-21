# ADR 0005 — Test resource isolation

**Status:** Accepted resource-isolation principles; fixture strategy refined by [ADR 0010](0010-respawn-fixture-reuse-and-test-policy.md), 2026-10-18.
**Original date:** 2026-10-15.

## Decision that remains binding

Resource ownership determines isolation. A project name, SQL category or STA thread does not prove that mutable database/UI state can be shared safely. Test collections coordinate only their own assembly/process; cross-process SQL and GUI execution must therefore be coordinated explicitly by the operator. Current policy runs SQL projects sequentially and one GUI command at a time.

SQL tests use positively owned disposable databases on the explicitly designated non-production instance, finite waits, drained workers and verified cleanup. The local helper verifies live instance/allocation identity before drop. The removed external framework supplies no watchdog, lease or orphan cleanup; interrupted allocations need manual operator review. GUI tests interacting with controls, focus, message loops or input retain serialized resources and deterministic STA disposal. Pure tests without shared mutable state can retain normal xUnit behavior.

## Accepted fixture direction

The owner selected and accepted **Respawn fixture reuse** for eligible tests in the completed [RESP plan](../plans/20261018-002-respawn-fixture-reuse.md). One migrated database belongs to one collection and is reset/seeded before every test. Schema/principal/lifecycle exceptions keep fresh allocations. Existing SQL assembly guards remain; Catalog's shared fixture uses its explicit serialized collection because Catalog has no assembly guard. RESP is not a parallelism rollout.

The old rejection of reusable fixtures was based on the then-current reset evidence. It is superseded by the accepted reuse strategy; its original measurements remain historical facts in [History](../HISTORY.md). No old dual-TFM stress/benchmark requirement is silently reinstated by this ADR. Current verification and failure handling are defined by [TEST_POLICY](../TEST_POLICY.md) and the completed [RESP evidence](../RESPAWN_VERIFICATION.md#pr-36-review-follow-up).

Any later SQL parallelism rollout needs its own ownership/capacity/cleanup evidence and scope decision. It is outside the accepted BCF scope; use the [current execution index](../plans/README.md) for completed evidence and current scheduling.

