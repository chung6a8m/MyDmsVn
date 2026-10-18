# Development plan — v1a

**Updated:** 2026-10-18. This roadmap records the owner's documentation/context reset. The canonical implementation queue is [docs/plans/README.md](plans/README.md).

## Two active workstreams

| Order | Work | Deliverable | Completion condition |
|---:|---|---|---|
| 1 | [RESP](plans/20261018-002-respawn-fixture-reuse.md) | net48-compatible Respawn fixtures, deterministic per-test reset/seed, dedicated exceptions, local collection lifecycle and useful fixture-operation evidence | All RESP tasks accepted, required SQL cases retained, reset/ownership failure behavior verified and comparable timing reported |
| 2 | [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) | LINQ to DB persistence behind existing Application/UoW/DTO contracts | Pilot, module migration, cleanup and acceptance complete on the accepted RESP baseline |

Both plans are **NOT IMPLEMENTED** at this reset. RESP is first because it changes the test environment on the existing production DAL; L2DB then changes the DAL while test isolation remains stable. A correctness or compatibility failure is resolved in its owning task before progressing.

## Established implementation

P0–P4 foundations and Local Login/session exist. P5 catalogs, receipts, posting, ledger/balances/card and their automated tests exist. NET8-PAUSE, modular test projects, SEC-REBUILD and the merged stabilization fixes are already implemented. [History](HISTORY.md) retains source revisions and acceptance qualifications without keeping those plans in the active queue.

The current product and all ten active test projects target `net48`. Tests run directly through `dotnet test`/Test Explorer with a local fresh-database helper; the former standalone SQL framework, manifests and wrappers are removed. AppCore/product .NET 8 is parked. Current production persistence is RepoDb/Dapper; DbUp owns SQL migrations.

## Deferred product and tooling work

[DEFERRED_WORK.md](DEFERRED_WORK.md) preserves the unfinished P5 interactive checks, P6 operational/release requirements, P7 HTTP scope, and runtime reactivation. Deferral changes priority, not completion status. P4.1's accepted partial idle-expiry observation and NET8-PAUSE's waived duration comparison remain limited to their original decisions.

RELEASE-MANIFEST-01's schema metadata discrepancy is now closed narrowly by L2DB-T02's source fix and clean-commit d8013030 artifact verification. Runtime startup still requires an explicitly authorized suitable identity; P5/P6 release readiness remains deferred.

## Execution and evidence

[TEST_POLICY.md](TEST_POLICY.md) replaces the previous test execution policy and gate matrix. Each task names its affected projects, test behavior, active TFM, runnable commands, resource requirements and expected evidence. Focused work stays focused; shared fixture/UoW/SQL changes and the two plan acceptance milestones receive broader real-SQL validation. GUI and performance work are tied to concrete plan requirements.

Use source-verified project/filter commands and actual TRX results. Preserve local database ownership, bounded cleanup and test serialization; absent configuration skips SQL facts and cannot satisfy acceptance. The removed framework is not a prerequisite for either plan, and its old manifest-v2 redesign is cancelled. Neither active implementation plan has new runtime acceptance from this baseline simplification.

For each completed task, record revision, scope, commands, selected/discovered/executed cases, pass/fail/skip/not-run results, cleanup and any blocked work. Historical evidence never replaces evidence for changed behavior.
