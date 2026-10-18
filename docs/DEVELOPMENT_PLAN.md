# Development plan — v1a

**Updated:** 2026-10-18. This roadmap records the owner's documentation/context reset. The canonical implementation queue is [docs/plans/README.md](plans/README.md).

## Two active workstreams

| Order | Work | Deliverable | Completion condition |
|---:|---|---|---|
| 1 | [RESP](plans/20261018-002-respawn-fixture-reuse.md) | net48-compatible Respawn fixtures, deterministic per-test reset/seed, dedicated exceptions, correct runner lifecycle accounting | All RESP tasks accepted, required SQL cases retained, reset/ownership failure behavior verified and comparable timing reported |
| 2 | [L2DB](plans/20261018-001-linq2db-net48-persistence-migration.md) | LINQ to DB persistence behind existing Application/UoW/DTO contracts | Pilot, module migration, cleanup and acceptance complete on the accepted RESP baseline |

Both plans are **NOT IMPLEMENTED** at this reset. RESP is first because it changes the test environment on the existing production DAL; L2DB then changes the DAL while test isolation remains stable. A correctness or compatibility failure is resolved in its owning task before progressing.

## Established implementation

P0–P4 foundations and Local Login/session exist. P5 catalogs, receipts, posting, ledger/balances/card and their automated tests exist. NET8-PAUSE, modular test projects, SEC-REBUILD and the merged stabilization fixes are already implemented. [History](HISTORY.md) retains source revisions and acceptance qualifications without keeping those plans in the active queue.

The current runtime matrix is `net48` for product, SQL, Desktop and harness tests; `netstandard2.0` for the shared harness; `net8.0` for runner tooling. AppCore/product .NET 8 is parked. Current production persistence is RepoDb/Dapper; DbUp owns SQL migrations.

## Deferred product and tooling work

[DEFERRED_WORK.md](DEFERRED_WORK.md) preserves the unfinished P5 interactive checks, P6 operational/release requirements, P7 HTTP scope, runtime reactivation and optional manifest-v2 work. Deferral changes priority, not completion status. P4.1's accepted partial idle-expiry observation and NET8-PAUSE's waived duration comparison remain limited to their original decisions.

The release script's schema `8..8` versus runtime `001–009` discrepancy is recorded there. This reset changes documentation only and does not repair packaging or certify release readiness.

## Execution and evidence

[TEST_POLICY.md](TEST_POLICY.md) replaces the previous test execution policy and gate matrix. Each task names its affected projects, test behavior, active TFM, runnable commands, resource requirements and expected evidence. Focused work stays focused; shared fixture/UoW/SQL changes and the two plan acceptance milestones receive broader real-SQL validation. GUI and performance work are tied to concrete plan requirements.

Existing strict runners and manifests remain code until deliberately adapted. Their preflight, SQL approval, ownership and watchdog checks must still pass when those tools are used. Do not edit fingerprints or skip cases only to make a run green. This documentation change requires static/link/consistency checks; it does not require or claim Windows/SQL/GUI execution.

For each completed task, record revision, scope, commands, selected/discovered/executed cases, pass/fail/skip/not-run results, cleanup and any blocked work. Historical evidence never replaces evidence for changed behavior.
