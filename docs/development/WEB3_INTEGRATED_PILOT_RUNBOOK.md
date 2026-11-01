# WEB3-06 — integrated HTTPS/browser and independent SQL pilot

The Owner authorized the bounded B04/B05 test/fixture/oracle and nonproduction acceptance extension on 2026-11-01. The [backend plan](../plans/20261101-002-web3-goods-receipt-inventory-http.md) owns B04/B05 status; the [frontend plan](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261101-001-web3-goods-receipt-inventory-web.md) owns F06. This runbook defines execution, not a PASS or closeout claim. No business/runtime, SQL schema/grant, TLS, process-supervisor or production change is authorized by this fixture.

## Selection and existing lifecycle

Use the existing Security owner and `WebHostIisExpressTests` lifecycle. The new selector is:

`Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation`

It reuses positively owned disposable schema11 SQL, SID-checked restricted runtime login, unchanged principal provisioner, existing trusted localhost certificate, isolated IIS Express configuration, five-minute supervised browser process tree and awaited guarded cleanup. Run exactly one SQL owner/browser invocation at a time. No TLS bypass, automatic write retry, rate-limit bypass, ad hoc SQL runner or production database is permitted.

The two original selectors keep their separate modes and data contracts. Integrated mode adds `mode: "web3-integrated"` to the existing private manifest. The frontend pilot config must select the existing eight Sales cases, seven Receipt cases and the new integrated spec. Every file must execute; source counts are not evidence of execution.

## Manifest and actors

The existing `catalog`, `receiptCatalog`, `actors`, `origin` and `resultPath` remain. Integrated mode adds:

```json
{
  "mode": "web3-integrated",
  "integratedCatalog": {
    "warehouseId": 2,
    "otherWarehouseId": 3,
    "employeeId": 1,
    "customerId": 1,
    "productId": 3
  }
}
```

IDs are examples; use manifest allocations. The two new warehouses have codes `WEB3-IW` and `WEB3-IW2`; the product is `WEB3-IP`. Both integrated pools start with no balance or movement. Original Sales keeps its seeded Q1000/V15000; original Receipt retains its own product. No initial integrated stock is seeded directly.

`integratedEditor`, `balanceReader` and `cardReader` are ordinary users with the four Catalog Read permissions. The editor has Sales Read/Write/Post, Receipt Read/Write/Post, Balances.Read and StockCard.Read. Each reader has only its own Inventory feature permission; neither has source-document Read/Write/Post. Existing actors and SQL runtime grants are unchanged. Passwords and actor credentials remain private temporary fixture input outside served assets.

## Result and physical oracle

Original result rows retain their formats from the [Receipt pilot runbook](WEB3_RECEIPT_PILOT_RUNBOOK.md). The integrated spec appends five rows, with canonical string document IDs and decimal strings:

```json
{
  "scenario": "integrated",
  "actor": "integratedEditor",
  "documentType": "GoodsReceipt",
  "receiptId": "123",
  "quantity": "10.0000",
  "value": "100.0000"
}
```

The Sale row uses `documentType: "SalesReceipt"`, `saleId`, `quantity: "4.0000"` and `cost: "60.0000"`. Preserve actual successful posting order: receipt10@10, receipt10@20, sale4@25, backdated receipt2@50, then receipt3@7 in the other warehouse. Rejected attempts never produce Post records.

The backend validates the **complete** result union against all physical Posted Receipt/Sale IDs before partitioning. Unknown scenario/type, invalid integrated actor, noncanonical or duplicate IDs and omitted/invented physical Posts fail. The existing Receipt oracle checks Receipt actors separately; legacy Sales result rows retain their actor-free contract. IDs are module-scoped; equal numeric Receipt and Sale IDs are valid. Legacy oracles remain scoped to their original keys, with global reconciliation preventing hidden extra documents/movements.

The integrated oracle reads physical headers, source lines, ledger, valuation companions and balances independently of production projection/valuation helpers. It checks:

- Four Receipt Posts and one Sale Post exactly, original actors, source keys/dates, header and movement timestamps, and ledger posting order.
- First pool: +10/+100, +10/+200, -4/-60, +2/+100; final **Q18/V340**. Historical Sale remains unit cost15, COGS60, revenue100, profit40 after the backdated receipt.
- Other warehouse: **Q3/V21**, using the same product without changing the first pool.
- Complete source-to-ledger-to-valuation correspondence, no Draft movement, no orphan/extra economic movement, exact allowed balance-key set and per-key Q/V reconciliation. Original Sales baseline is explicit; integrated opening Q/V is zero.

These are expectations until an invocation passes. Browser projections and the private result file do not substitute for the SQL oracle. The browser separately verifies displayed Inventory values/invalidation, source navigation, concurrent dirty Sales state and actual server permission denials. Focused existing Inventory/Security SQL tests own deterministic lock/rollback/revocation/rounding/depletion cases; the sequential browser scenario alone does not establish concurrency coverage.

## Prepare and run

Review the fixture/test diff first. Build the selected Web candidate with locked dependencies, run its focused checks, then build production `dist`. Follow the [sole test policy](../TEST_POLICY.md) and [Receipt fixture preparation](WEB3_RECEIPT_PILOT_RUNBOOK.md#prepare-and-execute) to restore/build Security Release/net48, publish the host to a new empty directory and copy the selected `dist` to `wwwroot`.

Set only process-local inputs, without printing secrets:

- `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`: existing explicitly designated nonproduction master connection.
- `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY`: newly published combined host/artifact directory.
- `MYDMSVN_WEB_TEST_TLS_THUMBPRINT`: already trusted localhost certificate/binding.
- `MYDMSVN_WEB_TEST_BROWSER_DIRECTORY`: selected frontend checkout containing all three specs and dependencies.
- `MYDMSVN_WEB_PILOT_PROJECT`: `chromium`, then `edge` in a separate fresh invocation.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_integrated_browser_preserves_same_key_QV_COGS_and_warehouse_isolation --logger 'trx;LogFileName=integrated-chromium.trx' --results-directory TestResults/web3-06-integrated-chromium
```

Use a new results path per attempt/browser. Pure result validation selectors are `FullyQualifiedName~WebIntegratedPilotResultTests|FullyQualifiedName~WebReceiptPilotResultTests`. They require no SQL and prove only malformed-result defenses.

Record exact frontend/backend source identities, artifact/runtime/browser versions, observed browser cases and wrapper results separately, SQL oracle, original failures, skips and cleanup in new WEB3 evidence. Retain sanitized local artifacts; never commit credentials, raw tokens or traces/HAR/video. Missing configuration and skipped required checks are NOT RUN. Preserve the existing failure/drain quarantine rules; never drop resources by prefix or adopt another invocation's resources.

Full IIS/production, audit retention, distributed limiting, durable drafts/idempotency, current-candidate attended IME/Narrator and JWT-localStorage XSS residual remain separate unaccepted limits. Closeout requires the owning plan/evidence and Owner review.
