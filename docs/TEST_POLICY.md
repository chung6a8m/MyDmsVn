# Test policy — v1a-pro

**Effective:** 2026-10-20 (sole active BCI roadmap; existing execution safeguards retained). **Authority:** the owner's replacement test policy after complete SQL framework removal. This is the sole execution policy; [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage and [tests/README](../tests/README.md) owns commands. [RESP, L2DB and GUI-P5 are completed history](plans/README.md); [BCI](plans/20261020-001-business-compact-inline-goods-receipt.md) is the sole active roadmap and is not implemented.

## 1. Current execution model

Run the existing test projects directly with `dotnet test` or Test Explorer. There are ten active test projects, all targeting `net48`; six own SQL integration scenarios. Use Windows, the SDK selected by `global.json`, and .NET Framework 4.8 targeting/runtime support. AppCore and modern product targets remain PARKED / NOT RUN.

The shared SQL helper in the IntegrationTests project creates one fresh disposable database per call and is linked into the other owners that need it. Completed RESP work added Respawn 4.0.0, an owned application-schema reset profile, exclusive collection scopes, tracked workers/cleanup, terminal poison and the explicit `MYDMSVN_SQL_TEST_FIXTURE_MODE=fresh|reuse` setting. RESP-T03–T04 adopted all 20 original data-only candidates across Catalog/Inventory/Security. Accepted profiles default to reuse for absent/blank settings; explicit fresh uses the same seeds/assertions on new owned allocations. Dedicated exceptions keep their existing fresh helper and ignore the fixture setting; lazy Inventory/Security collection setup allocates nothing when only exceptions are selected. RESP-T05 accepted the candidate recorded in [verification](RESPAWN_VERIFICATION.md#pr-36-review-follow-up), and L2DB preserved that contract. The removed gate manifest, standalone SQL support projects, command wrappers and their tests are historical context only. BCI does not restore them.

## 2. Choose scope from the changed behavior

Before executing, identify the behavior, affected dependencies, projects, active TFM and selected cases. Use the smallest runnable scope that covers the risk; broaden when shared dependencies make that scope insufficient.

| Change | Required verification | Broaden when |
|---|---|---|
| Markdown only | Diff, links, commands against source, roadmap/status consistency and encoding | Source, script, project or executable configuration also changes |
| Pure domain/application logic | Focused behavior, validation and authorization cases; affected build | Shared pipeline, contract or actor/session semantics affect other callers |
| One repository or query module | Relevant real-SQL CRUD, projection, transaction and error cases plus affected Application tests | Shared UoW, mappings, provider configuration or cross-module transactions change |
| UoW, provider, connection ownership or shared mappings | Foundation and every affected SQL owner, rollback and relevant concurrency cases | A shared persistence cutover requires all six SQL owners |
| SQL helper, fixture/reset/seed or database ownership | Focused local helper/lifecycle tests and real-SQL failure boundaries | Shared behavior requires affected owners; changes to the accepted reuse contract require the relevant fresh/reuse comparison |
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

## 5. Reusable-database contract

The completed [RESP plan](plans/20261018-002-respawn-fixture-reuse.md) records implementation and [ADR 0010](adr/0010-respawn-fixture-reuse-and-test-policy.md) records the decision. T01–T04 implemented the following contract for the reviewed Catalog/Inventory/Security data cases, with reuse as default and explicit fresh preserved. RESP-T05 verified the full six-owner pair for the recorded candidate. Preserve this accepted contract:

- One xUnit v2 collection owns one uniquely allocated database per assembly/process/TFM. Keep existing SQL assembly serialization. Catalog retains no assembly guard; all of its current fixture consumers share the explicit Catalog SQL collection.
- Allocate and migrate once per fixture; create its checkpoint after schema creation. Reset and seed before every Fact/Theory invocation, including the first, with explicit method/row labels for useful diagnostics. Every original behavior and Theory row remains covered.
- Reset all mutable business, reference, assignment, binding and audit rows. Preserve the verified DbUp journal and ownership metadata. Review the complete schema boundary; do not ignore a difficult table to obtain a green reset.
- The full schema includes an append-only StockLedger trigger. A narrowly owned test-only profile must restore and verify that exact trigger before any test body, even for an empty ledger. Production scripts and trigger behavior stay unchanged.
- Seed deterministically after reset. Treat sequence resets as explicit test-profile operations; use returned IDs/tokens unless a specific identity contract is being tested. Rowversion, time and arbitrary database state do not rewind.
- Drain and dispose test readers, transactions, connections, application scopes, timers and concurrency workers before the next reset. Stop callbacks, actual workers and resource callbacks share a two-minute cooperative cleanup budget; invoke callbacks off-thread to bound synchronous blocking. Timeout fails the case and terminally quarantines its allocation before waking queued callers; later admission and teardown reject without verification/reset/drop, even after late completion. Retain the allocation for manual review; a timed-out wait does not stop SQL. Database reuse does not share a business UoW or transaction between tests.
- Reset, seed, schema verification or cleanup failure poisons the fixture. The affected test fails and later bodies cannot run on uncertain state. After proven worker/resource drain, verify schema/journal/permissions/trigger/ownership again while admission is held, including final/focused cases; preserve body plus verification errors. Teardown attempts positively owned cleanup only after that drain. Timeout quarantine retains the allocation instead of dropping over active work. No silent fresh retry or skip-to-pass fallback.

Schema/history/principal/destructive-lifecycle and unresettable cases retain dedicated fresh allocations. A `SqlConcurrency` category neither permits nor forbids reuse by itself: preserve separate sessions and the reviewed reset boundary. The initial plan conservatively keeps its listed command/race/fault cases fresh.

Respawn 4's reset method has no cancellation-token overload. Its implementation must use finite command timeouts and terminate/drain the actual owned reset operation before releasing fixture access. A timed-out `Task.WhenAny` wait alone does not stop SQL. This requirement does not imply that an external watchdog exists today.

## 6. Historical acceptance and the active BCI milestone

RESP, L2DB and [GUI-P5](plans/20261019-001-full-winforms-gui-p5-acceptance.md) are completed within their approved, candidate-specific scopes. Preserve their original counts, failed trials, cleanup evidence, attended sign-offs and explicit exceptions, including GUI-P5-T05 Warehouse125Dark Down NOT TESTED. Those records are historical evidence, not recurring execution queues or proof of a future candidate. The 139 Desktop GUI invocations NOT RUN at L2DB-T07 remain a historical fact; later GUI-P5 evidence does not relabel that old run.

**BCI focused work:** [Business Compact and inline Goods Receipt](plans/20261020-001-business-compact-inline-goods-receipt.md), BCI-T00–T06, is the sole active roadmap. Its [design](BUSINESS_COMPACT_UI.md) and [acceptance specification](BUSINESS_COMPACT_ACCEPTANCE.md) describe proposed behavior and required evidence; documentation does not implement inline editing or satisfy acceptance. Each implementation task uses affected build and focused tests for the behavior it changes. Cover edit-buffer commit/cancel and invalid input, stable draft-row identity, lookup query versus committed identity, dirty/navigation handling, session/version/permission guards, read-only and uncertain-Post locks, and incremental rendering without rebuilding a live editor. Broaden to relevant controls, ViewModels/session cases and SQL/package checks when their actual boundaries are affected; do not run every suite after each edit.

**BCI frozen-candidate acceptance:** reconcile the actual source inventory and executed Facts/Theory rows for the complete Desktop.Session and Desktop GUI projects on interactive Windows, active `net48`/x86. Verify the changed UI and workflows at **native 100% and 125% display scaling, in Light and Dark**; simulated control resizing is not native-DPI evidence. Use the real shipped package and actual Login for the required receipt/stock/security E2E, with independent SQL assertions and positively owned resource cleanup. Preserve bounded process handling, exclusive desktop use, keyboard/focus/popup behavior, accessibility and attended observations/sign-off required by [Business Compact acceptance](BUSINESS_COMPACT_ACCEPTANCE.md). Record exact source/package hashes and actual per-context results. A focused repair rerun does not erase a failed full run or establish an unfiltered pass; historical totals are not permanent discovery targets.

**Production release:** clean install, supported upgrade/recovery, the approved OS/SQL/legacy/signing/backup matrix, separate migration principals, all-ten-project release regression and signed G1–G5 remain [release prerequisites](DEFERRED_WORK.md#release-prerequisites), **UNACCEPTED / UNSCHEDULED**. They are not a second active plan and are not waived by GUI-P5 completion or BCI planning/acceptance. No former Final manifest/runner or blanket full suite after each edit is restored.

Acceptance belongs to the exact candidate and scope. A later relevant source, test, schema, fixture, dependency or build change invalidates the evidence it can affect; rerun that scope. A documentation correction does not invalidate unrelated runtime evidence. Missing, skipped, timed-out, aborted or unexplained mandatory cases are never PASS.

## 7. GUI execution

Use the existing Desktop test project directly on an interactive Windows session. Keep its `StaTest`, `WinFormsTestGuard`, assembly serialization, bounded waits, deterministic disposal and captured exception/DataError behavior. Run one GUI command at a time; no external GUI lease or launcher remains. Fatal/non-cooperative hang probes and packaging subprocess tests use the Desktop-owned `GuiOwnedProcess` helper with bounded execution and verified process-tree/output drain. An in-process STA timeout cannot provide that guarantee; uncertain cleanup blocks further GUI admission. This local helper is not a restored suite watchdog or external runner.

Automated STA control checks, bounded out-of-process packaged E2E with real Login, and attended packaged-user acceptance are three separate evidence types. GUI-P5 completed its bounded process foundation and approved acceptance scope; BCI must preserve those fixes and obtain fresh evidence for affected behavior on its own frozen candidate. Do not launch GUI suites for unrelated Markdown or DAL-only edits unless the active plan names a meaningful host/package check. Missing Windows, targeting packs or GUI access is BLOCKED / NOT RUN.

## 8. Evidence record

Record task/revision, changed behavior, selected project/TFM/filter, scope rationale, commands actually run, expected versus observed cases, pass/fail/skip/not-run counts, and duration where useful. Include SQL allocation/reset/seed/cleanup outcomes or manual leftover review when relevant. State missing resources and remaining acceptance blockers explicitly.

When a performance or fresh/reuse comparison is required by the changed behavior, use the same frozen candidate, cases, project order, machine/configuration and build. Report actual case outcomes, allocation/migration/reset/seed/drop counts and wall-clock observations. A single matched pair is an observation; a speedup claim needs measured improvement. Repeat only for an invalid run, relevant repair, concrete regression question or separately requested benchmark. Do not restore a runner/invocation-manifest system for measurement.

Keep raw TRX, Theory values, SQL identifiers, connection data and detailed diagnostics local. Publish reviewed sanitized summaries. No historical total, another revision's result or documentation-only review establishes current runtime or performance evidence.

## L2DB-T06 cutover note

T06 removes active RepoDb/Dapper dependencies and mechanically replaces test/fixture SQL calls with independent native SqlClient oracles. The accepted reset/seed/ownership/drain contract above remains fixed. [T06 evidence](L2DB_VERIFICATION.md#l2db-t06--transitional-provider-and-test-graph-cleanup) records separate review, case/row reconciliation and affected boundary reruns; it does not replace RESP history or broaden the subsequently completed T07 package/limited Local smoke to full P5 GUI or P6 acceptance.

