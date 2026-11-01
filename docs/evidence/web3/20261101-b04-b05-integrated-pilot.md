# WEB3-06 — B04/B05 integrated SQL and HTTPS pilot evidence

**Date:** 2026-11-01 (Asia/Bangkok). **State:** DONE (bounded) / Owner accepted and PR #78 merged; backend WEB3 companion COMPLETED (bounded). The Owner explicitly authorized bounded backend tests/fixture/oracle/documentation plus designated nonproduction SQL and existing IIS Express HTTPS, with separate fresh Chromium and Edge fixtures. That initial execution authorization did not itself grant acceptance or merge; the subsequent Owner decision is recorded in section 7. Production remains NOT APPROVED. The [backend plan](../../plans/20261101-002-web3-goods-receipt-inventory-http.md) owns B04/B05; [frontend evidence](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/evidence/web3/README.md) owns F06 results.

## 1. Candidate, scope and implementation

| Identity | Exact revision |
|---|---|
| Backend baseline | `e86335add61cc2d1e936cadad019b96053e4b9ca` |
| Backend test/fixture candidate | `82f235248b1b570d41ae42f5d8cffd53f8b9f46e`, branch `codex/web3-06-integrated` |
| Initial Web production/pilot checkout | `67f317d52aaad49c77bb84e4593c6cbce6e05e1a` |
| Intermediate Web pilot synchronization candidate | `b4ebd405e1eefa40b754c11cba2b0f8b661a7e3d` (both browsers passed; later assertion review correction below) |
| Latest Web pilot review-corrected candidate | `d2e416f0a99bafe3851b61bbc90771fb10af9907` (test-only change; final Chromium and Edge verified) |
| Web F06 test/CI source | `73f560e20ffeefc0ee32bf1d327ebce7c8bc918c` |
| Unchanged Web runtime baseline | `7626a2fb6d9fe30ffad85c60b622e343fdf20502` |

The backend candidate changes tests and the [integrated pilot runbook](../../development/WEB3_INTEGRATED_PILOT_RUNBOOK.md), with no product runtime, schema, principal-provisioning grant, business valuation or authentication change. Existing owned `WebHostIisExpressTests` gains an explicit integrated mode, ordinary feature-scoped actors and two initially empty warehouse/product pools. Legacy Sales/Receipt modes and keys remain available. The complete Posted ID union is validated before scenario partitioning; the oracle checks source/ledger/value companions, actors/timestamps, posting order, exact balances, frozen Sale cost/profit and warehouse isolation. No new runner, supervisor timeout, TLS bypass or mutation replay is added.

Focused existing SQL tests were strengthened: same-Receipt concurrent Post now checks value/valuation/actor/version alongside quantity; HTTP invalid 0/201-line cases, replacement 200-line ordering/identity/version/actor and no-stock-side-effect assertions augment the existing 1/200-line and precision cases. Product handlers and queries are unchanged.

Source review reported no blocking finding before live execution. Review is separate from runtime acceptance and does not establish a live PASS.

## 2. Actual completed verification

The coordinator restored locked dependencies and built the selected owners in Release/net48 before `--no-build` execution. Initial Security build failed on xUnit analyzer **xUnit2031** for `Assert.Single(rows.Where(...))`; replacing it with the predicate overload resolved that test-code issue. Subsequent Security build was clean. This compiler/analyzer failure is not a failed SQL or browser case. No artificial failing test stub was introduced for the new result parser.

Completed TRX files were inspected and actual rows reconciled:

| Invocation | PASS | FAIL | SKIP | Scope / local result |
|---|---:|---:|---:|---|
| Pure result-oracle controls | 26 | 0 | 0 | 14 integrated + 12 existing Receipt rows; `TestResults/web3-06-pure-oracles/pure-oracles.trx` |
| Focused Inventory SQL | 54 | 0 | 0 | Selected valuation/transaction/query/concurrency rows; `TestResults/web3-06-inventory/inventory.trx`; runner approximately 3m29s |
| Security HTTP/SQL + restricted provisioning | 6 | 0 | 0 | Five `WebGoodsReceiptHttpSqlTests` + one principal case; `TestResults/web3-06-security/security.trx`; runner approximately 45s |

SQL owners ran sequentially. The SQL-free parser checks do not allocate SQL and are not counted as SQL acceptance. These totals are **86 executed rows, of which 60 are SQL rows**; they are neither a full backend suite nor the live pilot/browser count. The existing `SqlServerFact` required cases were not skipped. Successful SQL completion includes the owners' awaited resource disposal; no unresolved cleanup failure was reported for these completed invocations. Live cleanup is recorded separately below.

Commands (from backend checkout; explicit configured nonproduction master input retained without printing its value):

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~WebIntegratedPilotResultTests|FullyQualifiedName~WebReceiptPilotResultTests' --logger 'trx;LogFileName=pure-oracles.trx' --results-directory TestResults/web3-06-pure-oracles

$inventoryFilter = Get-Content TestResults/web3-06-inventory/filter.txt -Raw
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --no-build --filter $inventoryFilter --logger 'trx;LogFileName=inventory.trx' --results-directory TestResults/web3-06-inventory

dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter 'FullyQualifiedName~WebGoodsReceiptHttpSqlTests|FullyQualifiedName~WebPrincipalProvisioningTests.Separate_web_grants_work_and_desktop_cannot_read_auth_or_migrate' --logger 'trx;LogFileName=security.trx' --results-directory TestResults/web3-06-security
```

The ignored Inventory filter file contains the following exact selector (retained here so the invocation is reproducible without local artifacts):

```text
FullyQualifiedName~InventoryValuationWriterTests|FullyQualifiedName~SalesReceiptPostTests.Maximum_quantity_and_value_can_be_fully_depleted_exactly|FullyQualifiedName~SalesReceiptPostTests.Tiny_residual_and_zero_value_allocate_by_line_then_fully_drain|FullyQualifiedName~SalesReceiptPostTests.Failure_after_each_write_rolls_back_all_tables_version_and_audit|FullyQualifiedName~SalesReceiptPostTests.Posted_cost_is_immutable_after_later_backdated_receipt_and_audit_failure|FullyQualifiedName~SalesReceiptPostTests.Paid_and_gift_duplicates_freeze_one_average_and_exact_independent_movements|FullyQualifiedName~InventoryValuationConcurrencyTests.Reconciliation_exclusive_gate_serializes_both_writer_orderings|FullyQualifiedName~InventoryValuationConcurrencyTests.Reversed_product_lines_lock_numeric_product_order_before_stock|FullyQualifiedName~InventoryValuationConcurrencyTests.Reversed_multi_key_writers_wait_on_first_key_and_preserve_both_values|FullyQualifiedName~InventoryValuationConcurrencyTests.Gate_timeout_rolls_back_and_never_replays_post|FullyQualifiedName~InventoryValuationConcurrencyTests.Deadlock_victim_rolls_back_the_post_without_implicit_retry|FullyQualifiedName~SalesReceiptConcurrencyTests.Receipt_and_sale_use_actual_serial_posting_order|FullyQualifiedName~SalesReceiptConcurrencyTests.Sales_and_reconciliation_share_exclusive_barrier_in_both_orders|FullyQualifiedName~SalesReceiptConcurrencyTests.Reversed_lines_lock_all_sorted_stock_keys_before_any_mutation|FullyQualifiedName~SalesReceiptConcurrencyTests.Deadlock_victim_rolls_back_sales_and_never_automatically_retries|FullyQualifiedName~SalesReceiptConcurrencyTests.Competing_sales_cannot_both_consume_insufficient_combined_stock|FullyQualifiedName~GoodsReceiptCommandIntegrationTests.Concurrent_posts_for_same_receipt_commit_exactly_once|FullyQualifiedName~GoodsReceiptCommandIntegrationTests.Stale_or_denied_post_leaves_status_ledger_and_balances_unchanged|FullyQualifiedName~GoodsReceiptCommandIntegrationTests.Update_that_locks_first_wins_and_post_rejects_the_stale_snapshot|FullyQualifiedName~GoodsReceiptCommandIntegrationTests.Post_that_locks_first_wins_and_update_cannot_replace_posted_lines|FullyQualifiedName~InventoryValuationQueryTests|FullyQualifiedName~InventoryQueryIntegrationTests
```

The 54 Inventory rows exercise receipt value companions and individual rounding/tie/maximum bounds; zero/tiny residual/full depletion; frozen paid/gift duplicate Sales costs and later-backdated Receipt immutability; failed write rollback; Shared/Exclusive reconciliation in both orders; deterministic Receipt/Sale serial order; sorted multi-key locking; deadlock/timeout with no replay; one-effect concurrent Receipt Post; stale/denied and Update/Post races; nullable/invalid average, timestamp boundaries and inventory query paging/history. Theory rows count individually. The six Security rows exercise actual restricted HTTP→Application→SQL parity, independent permissions/revocation, ack-without-Read, invalid input/200-line replacement, valuation failure rollback and Web/desktop principal separation.

These focused tests establish their specific boundaries. They do not substitute for the new live same-key browser scenario or current-candidate attended input.

## 3. Prepared production artifact and environment

The coordinator observed: Windows build **26300**, .NET SDK **8.0.427**, `net48`, VSTest **17.11.1**, Node **22.14.0**, npm **11.6.0**, IIS Express **10.0.26013.1000**, Playwright **1.64.1**, Chromium **156.0.8078.4**, Edge **156.0.4314.58**. Sanitized metadata from the explicitly designated SQL input: SQL Server **14.0.2140.2**, RTM, Developer Edition (64-bit). No server/database address or connection string is published.

Fresh combined publication: `TestResults/web3-06-host-20261101-a/published`. All nine copied Web files matched selected `dist` byte-for-byte. Existing localhost TLS certificate/binding was trusted; no machine trust/binding or SQL grant change was made.

| Published file | SHA-256 |
|---|---|
| `bin/MyDmsVn.Server.HttpHost.dll` | `D233ACFB61865A0D0E8E4D326A22A77629A8C5FA22459CCB9BDF37A6BBFFAFEB` |
| `wwwroot/assets/index-Lu1N_ZH7.js` | `CE6AE5C640DCDF772ACA8002BF961D0678DE5BC790DA814258593D5ECD1248A0` |
| `wwwroot/assets/index-hppZpgkH.css` | `0FFA79226FD7AF662B08B7B3D41189CCC47D4736563942B4C41CA3E4E1465AC1` |

The host uses schema 11 and a separate SID-tracked restricted runtime SQL login. The owner fixture alone creates/removes its generated database after ownership-token/instance/database checks, with bounded child-process drain. Browser credentials/manifest/signing key remain outside served Web assets. Neither these prepared artifacts nor version inspection establish live acceptance.

## 4. Live invocation record — final candidate verified; prior results preserved

Selector:

`Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation`

The integrated mode selects three real-browser spec files: original Sales, original Receipt and new integrated scenarios. The runbook owns the input/result contract. Actual browser counts and one .NET wrapper row per invocation are recorded separately below.

| Invocation / Web pilot source | Browser result | Wrapper result | Independent physical oracle | Owned cleanup |
|---|---|---|---|---|
| Chromium attempt 1 / `67f317d` | **18 PASS, 0 FAIL/SKIP**, 39.2s | **1 PASS, 0 FAIL/SKIP**, 70.648s | **PASS** | **PASS** |
| Edge attempt 1 / `67f317d` | **18 PASS, 0 FAIL/SKIP**, 42.6s | **1 PASS, 0 FAIL/SKIP**, 69.732s | **PASS** | **PASS** |
| Chromium attempt 2 / `b4ebd40` | **18 PASS, 0 FAIL/SKIP**, 37.6s | **1 PASS, 0 FAIL/SKIP**, 62.895s | **PASS** | **PASS** |
| Edge attempt 2 / `b4ebd40` | **18 PASS, 0 FAIL/SKIP**, 45.6s | **1 PASS, 0 FAIL/SKIP**, 68.943s | **PASS** | **PASS** |
| Chromium attempt 3 / `d2e416f` | **18 PASS, 0 FAIL/SKIP**, 37.8s | **1 PASS, 0 FAIL/SKIP**, 62.164s | **PASS** | **PASS** |
| Edge attempt 3 / `d2e416f` | **18 PASS, 0 FAIL/SKIP**, 47.3s | **1 PASS, 0 FAIL/SKIP**, 70.727s | **PASS** | **PASS** |

Chromium attempt 1 executed all 18 selected browser cases: eight Sales, seven Receipt and three integrated cases. The wrapper returned PASS only after its independent physical SQL oracle and awaited cleanup. Local TRX: `TestResults/web3-06-integrated-chromium-attempt1/integrated-chromium-attempt1.trx`; sanitized browser output: `TestResults/web3-06-host-20261101-a/iis-7796b2e74d6640b29926a91842e9c6db/browser-output.log`. Safe screenshots were copied to the TRX directory's `browser-artifacts/` before Edge execution. Post-run inspection observed no 44321/18081 listeners, no IIS/testhost workers and removed manifest/public certificate PEM. The owned run directory retains only application configuration, browser results and sanitized output. No unresolved cleanup failure was reported.

```powershell
$env:MYDMSVN_WEB_PILOT_PROJECT = 'chromium'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-chromium-attempt1.trx' --results-directory TestResults/web3-06-integrated-chromium-attempt1
```

Edge attempt 1 used the same initial source/artifact and a separate fresh owned database/login/host. Actual results were 18/18 browser and 1/1 wrapper PASS, with no failed/skipped case and the independent SQL oracle passed. TRX: `TestResults/web3-06-integrated-edge-attempt1/integrated-edge-attempt1.trx`; sanitized output: `TestResults/web3-06-host-20261101-a/iis-8a88e7c69d6547f587a6c08243a297aa/browser-output.log`. Safe screenshots were retained in its result directory's `browser-artifacts/`. The wrapper awaited cleanup before success; post-run inspection observed no IIS/testhost worker, no 44321/18081 listeners and removed manifest/public PEM, matching Chromium's cleanup observations.

```powershell
$env:MYDMSVN_WEB_PILOT_PROJECT = 'edge'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-edge-attempt1.trx' --results-directory TestResults/web3-06-integrated-edge-attempt1
```

The explicit existing publish/browser/TLS/nonproduction inputs were set process-locally according to the runbook; no secret value is reproduced. The two initial passes are 36 executed browser invocations of the same 18 selected cases, plus two wrappers, not 36 distinct business scenarios.

### Screenshot synchronization follow-up

Review of the initial screenshots found that capture immediately after tab activation still showed transient controls belonging to an inactive panel. The reviewer traced the timing to Dockview visibility and the existing shadcn `transition-all` default 150ms transition. Source `b4ebd405e1eefa40b754c11cba2b0f8b661a7e3d` adds three `toBeHidden` assertions before the screenshot; it changes only the integrated pilot spec (five added lines including comments), with no runtime/CSS change. Typecheck passed. Published production assets and backend source remain unchanged.

Fresh Chromium attempt 2 passed all 18 browser cases, including the new hidden-control assertions, and its wrapper/independent SQL oracle passed. The coordinator viewed the settled `integrated-dirty-sales-preserved.png`: inactive-tab duplicate controls were gone and the dirty Sales note remained. This is actual Chromium screenshot review, separate from attended IME/Narrator acceptance. Backend source/artifact remained unchanged.

TRX: `TestResults/web3-06-integrated-chromium-attempt2/integrated-chromium-attempt2.trx`; sanitized browser output: `TestResults/web3-06-host-20261101-a/iis-c6d5b586e4ca4d86b3acdd22e9ab47d0/browser-output.log`; screenshots copied to that TRX directory's `browser-artifacts/`. Wrapper completion included awaited cleanup. Post-run inspection found no 44321/18081 listeners or IIS/testhost worker; manifest/public PEM were deleted.

```powershell
$env:MYDMSVN_WEB_PILOT_PROJECT = 'chromium'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-chromium-attempt2.trx' --results-directory TestResults/web3-06-integrated-chromium-attempt2
```

Fresh Edge attempt 2 passed all 18 browser cases, including the intermediate hidden-control assertions, and its wrapper/independent physical SQL oracle passed. The coordinator viewed the settled PNG: no inactive-panel controls remained visible and the dirty Sales note was unchanged. Both intermediate browser runs passed on Web pilot source `b4ebd405e1eefa40b754c11cba2b0f8b661a7e3d`, backend test/fixture source `82f235248b1b570d41ae42f5d8cffd53f8b9f46e`, and unchanged production assets. This is bounded automated/visual inspection, not attended input or general release approval.

TRX: `TestResults/web3-06-integrated-edge-attempt2/integrated-edge-attempt2.trx`; sanitized output: `TestResults/web3-06-host-20261101-a/iis-f37759f3f2b143e185f06652866cf0d7/browser-output.log`; reviewed screenshots copied to the TRX directory's `browser-artifacts/`. Awaited guarded cleanup passed; post-run inspection found no 44321/18081 listeners or IIS/testhost workers, with manifest/public PEM removed. No unresolved allocation/login/process cleanup was reported in any of the four live invocations.

```powershell
$env:MYDMSVN_WEB_PILOT_PROJECT = 'edge'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-edge-attempt2.trx' --results-directory TestResults/web3-06-integrated-edge-attempt2
```

The intermediate candidate has **36 executed browser invocations (18 per browser), 2 wrapper passes, 0 failures/skips**, with separate fresh owned fixtures. Including the two retained initial runs gives 72 browser invocations and 4 wrapper passes across the two test candidates; these are repeated executions of 18 selected cases, not 72 distinct business scenarios. Initial runs did not fail functional assertions; the screenshot review prompted a test synchronization improvement followed by the fresh intermediate runs.

### Final review correction — require retained hidden controls

Final review found the Sales hidden-control assertion could pass vacuously: its parent `getByRole` region locator omitted `includeHidden`, so the hidden region could disappear from locator matching before the child assertion inspected its control. The intermediate runs' functional/SQL results and actual PNG observations remain recorded, but that assertion alone did not prove a retained Sales control was hidden.

Web test-only source `d2e416f0a99bafe3851b61bbc90771fb10af9907` adds `includeHidden` to the parent and requires all three inactive controls to have count 1 before `toBeHidden`. Typecheck passed. No frontend runtime/CSS, published bytes or backend fixture source changed. Fresh Chromium attempt 3 passed all 18 browser cases, including count 1 and hidden assertions for all three retained inactive controls, plus its wrapper/independent physical oracle. The coordinator viewed the PNG: inactive controls were absent and the dirty Sales note remained unchanged. Post-run inspection found no 44321/18081 listeners or IIS/testhost workers, with manifest/public PEM removed; wrapper cleanup passed. TRX: `TestResults/web3-06-integrated-chromium-attempt3/integrated-chromium-attempt3.trx`; sanitized output: `TestResults/web3-06-host-20261101-a/iis-ef33832e9cb047468b2c1f13a873e39b/browser-output.log`; screenshots were copied to its result directory's `browser-artifacts/`.

Fresh Edge attempt 3 passed all 18 browser cases, including count 1 and hidden assertions for all three retained inactive controls, plus its wrapper/independent physical oracle. The coordinator viewed the PNG: no inactive-panel buttons were visible and the dirty Sales note remained unchanged. Post-run inspection found no 44321/18081 listeners or IIS/testhost workers, with manifest/public PEM removed; awaited guarded cleanup passed. TRX: `TestResults/web3-06-integrated-edge-attempt3/integrated-edge-attempt3.trx`; sanitized output: `TestResults/web3-06-host-20261101-a/iis-f1f73a19c66149cabcb708c11d30dbc8/browser-output.log`; screenshots copied to its result directory's `browser-artifacts/`.

Final scope review reported no gap in the selected bounded scope. Re-review of the assertion correction at `d2e416f` reported no further actionable source issue. The final source pair is Web pilot **`d2e416f0a99bafe3851b61bbc90771fb10af9907`** and backend test/fixture **`82f235248b1b570d41ae42f5d8cffd53f8b9f46e`**, with unchanged published production assets.

```powershell
$env:MYDMSVN_WEB_PILOT_PROJECT = 'chromium'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-chromium-attempt3.trx' --results-directory TestResults/web3-06-integrated-chromium-attempt3

$env:MYDMSVN_WEB_PILOT_PROJECT = 'edge'
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-edge-attempt3.trx' --results-directory TestResults/web3-06-integrated-edge-attempt3
```

**Final candidate:** 18 Chromium + 18 Edge browser cases passed, plus 1 wrapper per browser, with 0 failed/skipped cases and fresh independent owned fixtures. Both final screenshots were reviewed. Across all three retained candidates there were 108 browser invocations and 6 wrapper passes, repeating the same 18 selected cases; only the final 36 browser invocations establish the strengthened latest-candidate assertions. All six invocations completed guarded cleanup without a reported unresolved allocation/login/process failure.

All six live invocations passed the independent physical assertions: receipt 10 @ 10 then 10 @ 20 gives Q20/V300; Sale 4 @ 25 freezes unit cost 15 / COGS 60 / profit 40 and leaves Q16/V240; a later-posted backdated receipt 2 @ 50 leaves Q18/V340 without recosting that Sale. Same product in the other warehouse receives 3 @ 7, Q3/V21. Original regression keys remain independently reconciled and the complete global posted-source/ledger/value/key set is checked.

## 5. Pre-acceptance limits and disposition

At this implementation checkpoint, B04/B05 remained **IN_PROGRESS — implementation and bounded verification complete, pending Owner acceptance**. The subsequent Owner decision and merge are recorded in section 7 below. Final Chromium/Edge browser assertions, inspected screenshots, independent physical oracle and cleanup are verified; prior attempts remain recorded with their source-specific limits. At that checkpoint, Owner closeout/merge was a separate pending decision and B04/B05 were not marked DONE. Backend CI was not added or claimed PASS; frontend secret-free CI belongs to F06 and its own evidence. That checkpoint alone implied no merge/archive/production approval.

Full IIS, production deployment, audit destination/retention, distributed limiter, durable drafts/idempotency, current-candidate attended Telex/VNI/Narrator and JWT-localStorage XSS residual remain deferred/not approved. Historical WEB2 and WEB3-04 evidence remains unchanged and candidate-specific. CEX/WinForms remains independent.

## 6. PR publication and pre-merge review — 2026-11-01

Owner requested pushing both branches, creating PRs and checking CI/review before merge. Backend [PR #78](https://github.com/chung6a8m/MyDmsVn/pull/78) targets `main` from `codex/web3-06-integrated`; reviewed head `38e54e5745d02ca85eee89f84bf2f8b512b27432`, base `e86335add61cc2d1e936cadad019b96053e4b9ca`. Frontend companion is [PR #17](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/17). Both branches were pushed without rewriting history; main remained unchanged.

Fresh independent read-only source review found no actionable P0–P3 issue. It covered strict result union, independent valuation expectations, global source/ledger/value/key accounting including SQL NULL handling against schema constraints and surrounding assertions, legacy modes, ownership/cleanup and the focused HTTP/concurrency additions. Review was recorded as a [GitHub COMMENT](https://github.com/chung6a8m/MyDmsVn/pull/78#pullrequestreview-5727848116), not an approval or Owner acceptance. No runtime/test source changed after the recorded SQL/browser candidate; this publication checkpoint adds documentation only.

GitHub was queried at the reviewed SHA: **0 configured Actions workflows, 0 check runs, 0 commit statuses**. Backend hosted CI is **NOT CONFIGURED / NOT RUN**, not PASS. The actual local net48/SQL/browser verification in sections 2–4 remains the execution evidence. The PR was mergeable, with no unresolved review threads at inspection; this does not close B04/B05 or waive the recorded limits. No new SQL/browser suite was run solely for PR publication because tested source is unchanged.


## 7. Owner acceptance and merge — 2026-11-01

The Owner explicitly accepted the recorded verified scope and limitations, authorized merging frontend PR #17 and backend PR #78, and requested completion of WEB3-06/B04/B05. **B04/B05 are DONE (bounded); the backend WEB3 companion is COMPLETED (bounded), with B01–B05 DONE within their recorded scopes.** This is acceptance of the existing evidence, not a new runtime execution or production release. Frontend task status remains owned by its companion plan.

| Repository | Merged PR | Reviewed head accepted for merge | Actual merge commit |
|---|---|---|---|
| Backend | [PR #78](https://github.com/chung6a8m/MyDmsVn/pull/78) | `bc8217878f1f66888378a8d2af5cf6e09d06a190` | `07d3f09d94d117e83961b7c0cc0a890dd1f54ef8` |
| Frontend companion | [PR #17](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/17) | `89387a3fe55cdac22fc7fe20a516b6b68eaa3a3c` | `b67ce456c55c85b2b0303ef04798b8d039f6829d` |

Read-only Git checks confirmed each merge tree exactly matches its reviewed head: backend tree `31eb6b397445879351223a9ef83136a7d6a5b87c`, frontend tree `c826a6fbef9e3f176a30090c9d15d43882135ad4`. The coordinator fast-forwarded both original clean `main` checkouts to these merge commits. This closeout edits documentation only; no source/test/SQL/browser/build was rerun and no new runtime PASS is claimed. The actual tested sources remain backend `82f235248b1b570d41ae42f5d8cffd53f8b9f46e` and final Web pilot `d2e416f0a99bafe3851b61bbc90771fb10af9907`, with the artifact hashes and earlier candidates preserved above.

Accepted verification remains 54 Inventory SQL rows, 6 Security HTTP/SQL/provisioning rows and 26 pure oracle rows, all PASS with no failures/skips, plus the final fresh Chromium and Edge runs each with 18 browser cases and 1 wrapper PASS, independent physical oracle, guarded cleanup and reviewed screenshots. Prior attempts and review corrections are not removed or recast as different results.

Backend hosted CI has **0 configured workflows and remains NOT CONFIGURED / NOT RUN**, explicitly accepted as a recorded limitation, **not CI PASS**. The frontend companion's final reviewed-head CI passed in both the [push run 41302677410](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/actions/runs/41302677410) and [PR run 41302682776](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/actions/runs/41302682776): each executed 763 unit/component, 134 fake-browser and 1 production-artifact case. Those secret-free frontend jobs are separately owned frontend evidence; they do not establish backend net48/SQL or production acceptance.

Owner acceptance preserves every recorded limit: full IIS/production NOT RUN / NOT APPROVED; operational audit retention and distributed limiting deferred; durable drafts/idempotency not supplied; current-candidate attended Telex/VNI/Narrator NOT RUN; JWT-localStorage XSS residual retained. No physical archive/move is performed by this closeout. Historical WEB2 and prior WEB3 evidence remain untouched; the independent CEX workstream remains ACTIVE and unchanged. Further backend/runtime or release work requires a new explicit scope selection.

Closeout documentation checks: all 286 local file links across the eight changed Markdown files resolved; UTF-8 without BOM and CRLF were verified; `git diff --check` passed. These are documentation checks only.
