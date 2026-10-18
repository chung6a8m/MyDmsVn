# Test policy — v1a

**Effective:** 2026-10-18. **Authority:** the owner's replacement test policy and complete removal of the former SQL test framework. This is the sole execution policy; [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage and [tests/README](../tests/README.md) provides current commands. The active roadmap is [RESP, then L2DB](plans/README.md).

## 1. Current execution model

Run the existing test projects directly with `dotnet test` or Test Explorer. There are ten active test projects, all targeting `net48`; six own SQL integration scenarios. Use Windows, the SDK selected by `global.json`, and .NET Framework 4.8 targeting/runtime support. AppCore and modern product targets remain PARKED / NOT RUN.

The shared SQL helper in the IntegrationTests project currently creates one fresh disposable database per call. It is linked into the other owners that need it. Respawn is not installed; reuse, reset profiles and an explicit fresh/reuse switch remain RESP implementation work. The removed gate manifest, standalone SQL support projects, command wrappers and their tests are historical context only. Neither active plan restores them.

## 2. Choose scope from the changed behavior

Before executing, identify the behavior, affected dependencies, projects, active TFM and selected cases. Use the smallest runnable scope that covers the risk; broaden when shared dependencies make that scope insufficient.

| Change | Required verification | Broaden when |
|---|---|---|
| Markdown only | Diff, links, commands against source, roadmap/status consistency and encoding | Source, script, project or executable configuration also changes |
| Pure domain/application logic | Focused behavior, validation and authorization cases; affected build | Shared pipeline, contract or actor/session semantics affect other callers |
| One repository or query module | Relevant real-SQL CRUD, projection, transaction and error cases plus affected Application tests | Shared UoW, mappings, provider configuration or cross-module transactions change |
| UoW, provider, connection ownership or shared mappings | Foundation and every affected SQL owner, rollback and relevant concurrency cases | L2DB final cutover requires all six SQL owners |
| SQL helper, fixture/reset/seed or database ownership | Focused local helper/lifecycle tests and real-SQL failure boundaries | Shared behavior requires affected owners; RESP acceptance requires all six and a matched fresh/reuse comparison |
| Migration/schema/security principal | Dedicated fresh-database migration, schema and privilege tests | Shared schema or compatibility affects other SQL owners |
| Pure ViewModel/session logic | Focused non-GUI cases with deterministic clock/actor inputs | Actual controls, binding, focus, STA or host composition change |
| WinForms/host/release package | Relevant STA/GUI tests and packaged smoke required by the plan | A release milestone requires the broader shipped-host matrix |

Do not automatically run `dotnet test MyDmsVn.sln`, full SQL, full GUI or a full repository regression after each edit, every task or every code PR. Preserve meaningful real-SQL behavior; mocks cannot establish SQL transactions, privileges, locking or trigger semantics. A separately mergeable change still needs its affected verification scope.

## 3. Direct commands and results

Use an existing project plus a source-verified `FullyQualifiedName` or `Category` filter. Current SQL categories are `FastSQL`, `MigrationSchema`, `SecurityProvisioning` and `SqlConcurrency`. Categories select responsibility; they do not establish whether a case is pure code, reusable or safe to parallelize. A case can have more than one category.

Restore locked dependencies and build the current revision before using `--no-restore` or `--no-build`. For example, a focused Catalog SQL check is:

```powershell
$project = 'tests/MyDmsVn.Server.Infrastructure.Catalog.Tests/MyDmsVn.Server.Infrastructure.Catalog.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
dotnet test $project -c Release -f net48 --no-restore `
  --filter 'Category=FastSQL' --logger 'trx;LogFileName=catalog.trx' `
  --results-directory ('TestResults/cat-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Catalog SQL tests failed.' }
```

This example requires the SQL environment in section 4. Omit `--filter` for a complete project pass. For multiple SQL owners, invoke their projects sequentially. Do not run competing SQL or GUI commands concurrently in the same checkout or test environment. There is no cross-process lease or global scheduling service; xUnit collection coordination applies only inside its own assembly/process.

Review the selected source cases and actual TRX outcomes, including Theory rows, when judging a run. `--list-tests` can help check a selector, but discovery does not execute SQL and is not acceptance. A successful exit alone does not establish coverage if required cases were missing or skipped. Record the actual case/count/outcome differences and investigate omissions; do not invent a manifest or fingerprint gate to replace the removed framework.

## 4. SQL environment and ownership

Configure `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` explicitly for an operator-designated non-production SQL Server with effective database `master`. The account must be able to create and drop its disposable databases; particular principal/maintenance/DMV scenarios can require additional test-instance privileges documented by their source. Never substitute the product runtime or migration connection. Do not print or commit connection strings.

Missing configuration causes `SqlServerFact` cases to skip. That is useful for local non-SQL work and is **BLOCKED / NOT RUN** for required SQL coverage. Invalid configured input or connection/ownership failure is a test failure, not a reason to select another instance silently. No approval JSON file or external preflight command is required.

The local helper rejects attachment/user-instance options and restricts allocation to generated `MyDmsVn_Test_` names. It records the actual instance and allocation identity (`database_id`, creation time and a random `MyDmsVn.TestOwnership` extended-property token), and revalidates them before dropping. Helper connections use `Pooling=false`; disposal does not clear unrelated pools. Missing or changed ownership causes refusal. A name prefix alone never authorizes deletion.

Keep finite connection/SQL timeouts, deterministic disposal and drained concurrency workers. After a failed cleanup, preserve both the original and cleanup failures and report the unresolved allocation. A killed testhost can leave a database behind: there is no durable ownership journal, automatic orphan reconciliation or process-tree watchdog in the removed setup. An operator must review leftovers manually on the designated test instance; never use a prefix-only bulk-drop script or adopt a leftover as a new fixture.

## 5. Planned reusable-database contract

The [RESP plan](plans/20261018-002-respawn-fixture-reuse.md) owns implementation and [ADR 0010](adr/0010-respawn-fixture-reuse-and-test-policy.md) records the decision. The following is the required destination, not current delivered behavior:

- One xUnit v2 collection owns one uniquely allocated database per assembly/process/TFM. Keep existing SQL assembly serialization. Catalog has no assembly guard today; its shared consumers must all join an explicit collection.
- Allocate and migrate once per fixture; create its checkpoint after schema creation. Reset and seed before every Fact/Theory invocation, including the first, with explicit method/row labels for useful diagnostics. Every original behavior and Theory row remains covered.
- Reset all mutable business, reference, assignment, binding and audit rows. Preserve the verified DbUp journal and ownership metadata. Review the complete schema boundary; do not ignore a difficult table to obtain a green reset.
- The full schema includes an append-only StockLedger trigger. A narrowly owned test-only profile must restore and verify that exact trigger before any test body, even for an empty ledger. Production scripts and trigger behavior stay unchanged.
- Seed deterministically after reset. Treat sequence resets as explicit test-profile operations; use returned IDs/tokens unless a specific identity contract is being tested. Rowversion, time and arbitrary database state do not rewind.
- Drain and dispose test readers, transactions, connections, application scopes, timers and concurrency workers before the next reset. Database reuse does not share a business UoW or transaction between tests.
- Reset, seed, schema verification or cleanup failure poisons the fixture. The affected test fails, later bodies cannot run on uncertain state, and teardown still attempts positively owned cleanup. No silent fresh retry or skip-to-pass fallback.

Schema/history/principal/destructive-lifecycle and unresettable cases retain dedicated fresh allocations. A `SqlConcurrency` category neither permits nor forbids reuse by itself: preserve separate sessions and the reviewed reset boundary. The initial plan conservatively keeps its listed command/race/fault cases fresh.

Respawn 4's reset method has no cancellation-token overload. Its implementation must use finite command timeouts and terminate/drain the actual owned reset operation before releasing fixture access. A timed-out `Task.WhenAny` wait alone does not stop SQL. This requirement does not imply that an external watchdog exists today.

## 6. Acceptance for the two plans

**RESP:** execute every case in all six SQL projects on `net48`, including the local helper/reset/lifecycle checks affected by the change. Prove no contamination, preserved journal/ownership/trigger/seed state, failed initialization/reset behavior and real multi-session semantics. Verify that accepted eligible profiles default to reuse and explicit exceptions remain fresh.

Compare the same frozen candidate and SQL cases once in explicit test-only `fresh` mode and once in `reuse` mode, using the same project order, machine/configuration and build. Report actual case outcomes, allocation/migration/reset/seed/drop counts and wall-clock observations. Lightweight fixture/test output can record those operations; do not restore a versioned runner/invocation-manifest system. A single matched pair is an observation; any speedup claim needs actual improvement. Repeat only for an invalid run, relevant repair, concrete regression question or separately requested benchmark.

**L2DB:** use the accepted RESP baseline unchanged. Require all six SQL owners, affected Application/architecture/session coverage, and the plan's net48 package/host smoke. Preserve rollback, rowversion, locking, permissions and stored procedures. Individual module edits still use focused checks; final breadth follows from replacing shared persistence.

Acceptance belongs to the exact candidate and scope. A later relevant source, test, schema, fixture, dependency or build change invalidates the evidence it can affect; rerun that scope. A documentation correction does not invalidate unrelated runtime evidence. Missing, skipped, timed-out, aborted or unexplained mandatory cases are never PASS.

## 7. GUI execution

Use the existing Desktop test project directly on an interactive Windows session. Keep its `StaTest`, `WinFormsTestGuard`, assembly serialization, bounded waits, deterministic disposal and captured exception/DataError behavior. Run one GUI command at a time; no external GUI lease or launcher remains. Any future fatal or non-cooperative hang probe must own a bounded subprocess and terminate/drain it; an in-process STA timeout cannot provide that guarantee. Current packaging subprocess tests have no suite watchdog, so a hung process requires an explicit stop and incomplete-result report.

Automated STA checks and attended packaged-user acceptance are separate evidence. Do not launch GUI suites for unrelated Markdown or DAL-only edits unless the active plan names a meaningful host/package check. Missing Windows, targeting packs or GUI access is BLOCKED / NOT RUN.

## 8. Evidence record

Record task/revision, changed behavior, selected project/TFM/filter, scope rationale, commands actually run, expected versus observed cases, pass/fail/skip/not-run counts, and duration where useful. Include SQL allocation/reset/seed/cleanup outcomes or manual leftover review when relevant. State missing resources and remaining acceptance blockers explicitly.

Keep raw TRX, Theory values, SQL identifiers, connection data and detailed diagnostics local. Publish reviewed sanitized summaries. No historical total, another revision's result or documentation-only review establishes current runtime or performance evidence.
