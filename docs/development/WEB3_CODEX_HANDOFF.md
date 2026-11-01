# WEB3 backend Codex handoff — execution guide

**Selected 2026-11-01; B01 merged; B02/B03 implemented and verified in bounded net48 HTTP/TestServer + SQL scope.** Candidate d2e7e3f3709a7e97cf91c7d4558c560303fa498d is local and not merged; [new evidence](../evidence/web3/20261101-b02-b03-http-inventory.md) records actual results. B04/B05 and the WEB3 IIS Express/browser pilot remain PLANNED/NOT RUN. Read in order: [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → [active index](../plans/README.md) → [WEB3 backend plan](../plans/20261101-002-web3-goods-receipt-inventory-http.md) → [canonical corpus](../contracts/web3-v1/README.md) → [source/SQL gap checkpoint](../design/WEB3_B01_SOURCE_CHECKPOINT.md) → [WEB3 wire](../design/WEB3_INVENTORY_HTTP_WIRE.md) → [SQL oracle](../design/WEB3_STOCK_LEDGER_ACCEPTANCE.md) → sole [TEST_POLICY](../TEST_POLICY.md) and [tests README](../../tests/README.md). Web companion owns its own F tasks and [handoff](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/development/WEB3_CODEX_HANDOFF.md). WEB2 is archived; WinForms CEX-A01–A05 remains independently active.

## 1. Verify source before each PR

1. Start at current `main`, not an archived WEB2 branch; record SHA, projects/target frameworks and lockfile. Inspect schema11 Web opt-in, existing IIS Express fixture and deployment runbook. Do not change WinFormsKit gitlink or native CEX code.
2. Inspect `src/MyDmsVn.Server.HttpHost/{Startup,Web2Pipeline,AuthController,SalesControllers,SalesWireInput,SalesWireOutput,StrictWireJson}.cs` and `src/MyDmsVn.Server.HttpHost/README.md`. `SalesControllers.cs` owns `WireApiController` and current MediatR dispatch. Use that host and security pipeline; no second HTTP middleware service.
3. Inspect `src/MyDmsVn.Contracts/InventoryContracts.cs`, `src/MyDmsVn.Server.Application/Inventory/{GoodsReceiptFeatures,GoodsReceiptQueries,InventoryQueries,InventoryPermissionDefinitions}.cs`, `src/MyDmsVn.Server.Infrastructure/Inventory/*`, [Data model](../DATA_MODEL.md), [Direct Sales costing](../DIRECT_SALES_COSTING.md) and [Contracts](../CONTRACTS.md).
4. Inspect existing `tests/MyDmsVn.Server.HttpHost.Tests`, Inventory/Security SQL owners, and packaged `WebHostIisExpressTests`. Choose actual source-verified, nonempty test selectors; scenario IDs in plans are not runnable test names.
5. Recheck the B01 source checkpoint if main changes. It resolves count-as-string, full list lines, explicit nullable response fields, Save label defaults, positive replace-line IDs, required paging, eight-place average and UTC formatting. Preserve these corpus decisions; document new discrepancies before runtime implementation.

## 2. PR-by-PR implementation order

B01–B03 below describe the delivered contract and implementation boundaries; do not restart them as new work. Review the candidate and its evidence, then use the separately owned frontend roadmap for F03 adoption. For an existing Web database, the [host runbook](../../src/MyDmsVn.Server.HttpHost/README.md) records the exact B02/B03 principal provisioning delta. Subsequent B04/B05 execution requires its own selected scope and evidence.

### PR B01 — contract freeze (no HTTP runtime change)

- Canonical `docs/contracts/web3-v1/` now contains field tables, manifest, executable golden JSON and hostile/numeric/expansion controls. Backend owns fixtures; frontend consumes exact pinned bytes/provenance. Run `node docs/contracts/web3-v1/validate-corpus.mjs` and `node --test docs/contracts/web3-v1/validate-corpus.test.mjs`; these are SQL-free contract checks, not server endpoint tests.
- Map each field to source `InventoryContracts.cs`/handlers and select exact wire output/null/paging projection. Update WEB3 design if source disagrees, with explicit reason. Never change Local DTO serialization just to simplify Web.
- Verify contract, links and source review; record source hash and nonempty fixture-check execution evidence. Stop if new schema/permission policy is needed unexpectedly.

### PR B02 — thin routes and capabilities

- Add proposed `GoodsReceiptsController` and `InventoryController` in existing net48 HttpHost, using `WireApiController.Execute<T>`, `IMediator.Send`, current actor/SQL session and ApiResponse mapping. Dispatch exactly `ListGoodsReceiptsQuery`, `GetGoodsReceiptByIdQuery`, `CreateGoodsReceiptDraftCommand`, `UpdateGoodsReceiptDraftCommand`, `PostGoodsReceiptCommand`, `GetStockBalancesQuery` and `GetStockCardQuery`.
- Extend `AuthController.Capabilities` with fixed feature whitelist `goods-receipts`, `inventory-balances`, `inventory-stock-card`. Preserve `feature=sales` response contract. Receipt Read/Write/Post and Inventory Balances/StockCard Read are independent server permissions; UI hints do not authorize requests.
- Add SQL-free HTTP tests for verb/route, 400/401/403/404/409, malformed body/query, Origin/CSRF/CSP and unauthorized caller without bypass. Maintain Sales/Auth/Catalog tests.

### PR B03 — precise inventory wire and SQL-backed endpoints

- Add feature-specific strict input/output mappers: int64 JSON **strings**, invariant decimal **strings**, checked int32 IDs, calendar `yyyy-MM-dd`, UTC `Z` timestamps, opaque rowversion; reject extra/duplicate JSON/query keys and numeric decimal tokens before business dispatch.
- Receipt Save contains only header and lines `{productId,quantity,unitCost}`; Update includes expectedVersion and whole-line replacement (no receiptLineId request). Existing Create/Update materializes authoritative Draft DTO before COMMIT; WEB3 emits explicit null labels/time/note as applicable, locally overriding null omission without changing Sales/Local. Receipt Post ack has ID/status/UTC **without version**. No post-commit Get or fake version.
- Preserve receipt list pageNumber/pageSize/warehouse/status/search and source ordering; balance/card query requirements and read-only behavior. No new stock adjustment or SQL writes through HTTP.
- Test authorized restricted SQL principal, denied no-effect, pre-commit response, 1/200 lines, duplicate Product, rowversion, UTC/decimal and StockCard source joins. Review the exact runtime SELECT/EXEC privileges; do not grant db_owner or blanket DML.

### PR B04 — independent physical SQL oracle

- Add [SQL scenarios](../design/WEB3_STOCK_LEDGER_ACCEPTANCE.md) to *existing* Inventory/Security owners using owned nonproduction disposable DB, not a new test runner.
- Independently read `StockLedger`, `StockValuationEntries`, `StockBalances`, receipt/sale headers and sale line costs. Receipt Post → Sales Post → later Receipt Post must preserve frozen COGS; test concurrency, duplicate Post, rollback, sorted stock locking and distinct warehouse keys.
- Verify browser/service projections only *after* physical Q/V oracle; no expected values computed through the same query method under test. Record actual test count/skips and cleanup.

### PR B05 — HTTPS pilot and handoff

- Extend the existing `Publish-Web2Host.ps1`/`WebHostIisExpressTests` owned fixture; publish into a new **empty** directory and copy Web production `dist` to `wwwroot`. Use pretrusted localhost certificate, restricted SQL principal/owned schema11 DB and existing process/job/cleanup safeguards.
- Run Chromium and Edge **sequentially** against fresh fixtures; exercise login/refresh, Goods Receipt via real AG Grid, balances/card, Sales and independent SQL oracle. Ensure production build lacks fake Lab/token dumps, Origin/TLS remains strict and no hidden client write retries.
- Record actual browser cases separately from .NET wrapper rows; missing IIS Express/SQL/certificate/Edge is BLOCKED/NOT RUN, never PASS or reused WEB2 evidence. WEB3 is bounded pilot, not full IIS/production approval.

## 3. Non-negotiable implementation boundaries

- No SQL/UoW use inside HTTP controllers; handlers retain authorization, validation, transaction and actor audit ownership.
- Preserve `GoodsReceipt` unique Product and 1–200 line rules; Update full replacement; Sales duplicate Product/gift still legal. Don't rely on server receipt line IDs being stable after update.
- No direct mutation endpoint for StockBalances/StockLedger/Valuation and no client-supplied number/status/actor/ledger or lineAmount.
- No fake receipt version in Post ack, automatic Create/Update/Post replay or backend idempotency journal.
- No global auth rewrite, runtime retarget, alternate ORM, broad SQL grants, general service framework, full IIS or production release. STOP for explicit Owner scope decision if these become necessary.
- Preserve CEX plan and all archived/historical evidence independently; new WEB3 updates belong only to WEB3 files.

## 4. Commands and evidence

All product/test targets are `net48`. Use Windows .NET Framework 4.8 + SDK from `global.json`, locked restore, nonempty test discovery and scope-specific test runs. Example (unfiltered HttpHost.Tests, actual result to be recorded):

```powershell
$project = 'tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj'
dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
dotnet test $project -c Release -f net48 --no-restore --logger 'trx;LogFileName=web3-host.trx' --results-directory ('TestResults/web3-host-' + [guid]::NewGuid().ToString('N').Substring(0,8))
if ($LASTEXITCODE -ne 0) { throw 'Host tests failed' }
```

For SQL set `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` explicitly to non-production `master` and run relevant owners **sequentially** as [tests README](../../tests/README.md) prescribes. Missing config/SQL skipped is NOT RUN. Packaged pilot needs explicit `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY`, `MYDMSVN_WEB_TEST_TLS_THUMBPRINT`, `MYDMSVN_WEB_TEST_BROWSER_DIRECTORY` and `MYDMSVN_WEB_PILOT_PROJECT` as current runbook documents. Never include passwords/JWT/SQL strings in logs or commits.

Record backend and Web SHAs, task IDs, exact commands and discovered/executed rows, pass/fail/skip, SQL ownership/drain/principal, browser versions/TLS, equations/physical rows, failures and remediation. File new evidence in `docs/evidence/web3/` only. B01 has corpus execution plus diff/link/encoding checks; it creates no HTTP/SQL/browser runtime evidence.
