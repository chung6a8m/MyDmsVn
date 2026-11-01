# WEB3-04 — bounded Goods Receipt HTTPS/browser fixture

Owner authorized the fixture extension and real acceptance on 2026-11-01: "Cho phép mở rộng fixture và chạy nghiệm thu thật". This is the bounded backend fixture dependency for Web F03/F04, not completion of B04, all of B05, F05/F06 or production acceptance. The [backend plan](../plans/20261101-002-web3-goods-receipt-inventory-http.md) owns backend status; [fixture evidence](../evidence/web3/20261101-web3-04-receipt-pilot-fixture.md) records actual execution separately.

## Existing infrastructure and explicit selection

Use the existing Security test owner, disposable `SqlTestDatabase`, SID-checked restricted login, reviewed Web principal provisioner, net48 published host, isolated IIS Express configuration, trusted localhost certificate and owned browser process tree. There is no second SQL runner or production test endpoint. The existing Sales-only selector and its manifest/oracle remain Sales-only:

`Published_React_browser_uses_real_HTTPS_SQL_and_atomic_stock`

The separate bounded Receipt selector reuses the same fixture lifecycle:

`Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock`

Run one selector and one browser at a time. Never run concurrent SQL owners, reuse an existing application database, disable TLS/limiting or provision a machine binding as part of acceptance. The existing binding/certificate must already be trusted. Missing configuration or skipped tests are NOT RUN, not acceptance.

## Manifest and browser results

The temporary manifest retains `origin`, `actors.alice/bob/poster`, `catalog` and `resultPath`. Receipt mode adds:

```json
{
  "mode": "web3-receipt",
  "actors": {
    "receiptEditor": { "username": "receiptEditor", "password": "<temporary test credential>" },
    "receiptPoster": { "username": "receiptPoster", "password": "<temporary test credential>" },
    "receiptReader": { "username": "receiptReader", "password": "<temporary test credential>" }
  },
  "receiptCatalog": { "warehouseId": 1, "employeeId": 1, "productId": 2 }
}
```

IDs above are illustrative; always use allocated values from the manifest. All Receipt actors receive the four Catalog lookup Read permissions. `receiptEditor` receives Sales Read/Write/Post and Receipt Read/Write/Post. `receiptPoster` receives Receipt Write/Post, with no Receipt Read. `receiptReader` receives Receipt Read, with no Write/Post. None is a superuser; the ordinary permission service remains authoritative.

Receipt headers use the existing PILOT Warehouse/Employee. Receipt lines use the separate Product `WEB3-RP` (`Web3 Receipt Product`), initially with no balance. Existing Sales tests retain the `PILOT-P` key, Q=1000 and V=15000. This isolates existing Sales COGS expectations; this fixture does not establish B04's Receipt→Sale→later Receipt valuation scenario.

Frontend `playwright.pilot.config.ts` must select only `web2-live.spec.ts` in default mode, and both that file and `web3-receipt-live.spec.ts` when `mode` is `web3-receipt`. Both files must actually execute. Keep retries zero, one worker, TLS validation enabled and trace/video/HAR disabled. The backend requires the Receipt spec and nonempty Sales and Receipt Post results. Both modes retain the existing five-minute `GuiOwnedProcess.RunAsync` execution budget and its unchanged process-tree cleanup safeguards.

Keep the existing result array format for Sales rows: `{ "saleId": "...", "quantity": "...", "cost": "..." }`. Append one row per successfully committed Receipt Post, including a real Post whose response is deliberately lost:

```json
{
  "documentType": "GoodsReceipt",
  "receiptId": "123",
  "quantity": "2.0000",
  "value": "6.0000",
  "actor": "receiptEditor"
}
```

All values are strings. `receiptId` must be canonical positive int64 text: no leading zero, sign, whitespace or numeric JSON token. IDs are parsed once, checked for numeric uniqueness and compared as an exact set with physical SQL Posted receipt IDs before per-row verification. `actor` is `receiptEditor` or `receiptPoster`; quantity/value are the expected total source quantity and sum of independently rounded line values. Do not record Draft-only or rejected operations as Post. Duplicate or omitted Posted receipt rows fail SQL reconciliation. The SQL oracle reads source lines, independently calculates positive half-away-from-zero four-place values, verifies exactly one matching ledger/valuation companion per line including key, timestamp and actor, verifies the complete Posted receipt set and compares aggregate receipt stock. Existing Sales source/COGS assertions remain, with stock/ledger aggregates scoped to the original key in combined mode. Only the two expected balance keys may exist.

## Prepare and execute

Build a stable selected frontend candidate with locked dependencies and its focused tests, then `npm.cmd run build`. Parent/coordinator must first review the fixture diff and coordinate exclusive SQL execution. From this backend checkout:

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore
./src/MyDmsVn.Server.HttpHost/Publish-Web2Host.ps1 -OutputPath '<new empty absolute publish directory>' -SkipBuild
```

Copy the selected Web `dist` contents to the newly published `wwwroot` as described in the [existing host runbook](../../src/MyDmsVn.Server.HttpHost/README.md). Keep credentials, manifest and JWT keys outside served assets. Configure process-local inputs without printing secrets:

- `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING`: designated nonproduction master connection, using the existing [test policy](../TEST_POLICY.md) ownership rules.
- `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY`: combined fresh published root.
- `MYDMSVN_WEB_TEST_TLS_THUMBPRINT`: existing trusted localhost:44321 certificate.
- `MYDMSVN_WEB_TEST_BROWSER_DIRECTORY`: absolute selected frontend checkout, with dependencies and both pilot specs.
- `MYDMSVN_WEB_PILOT_PROJECT`: `chromium`, then `edge` in a separate fresh invocation.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_Receipt_browser_uses_real_HTTPS_SQL_and_independent_receipt_stock --logger 'trx;LogFileName=receipt-chromium.trx' --results-directory TestResults/web3-04-receipt-chromium
```

Use separate TRX/results paths for Edge. The fixture passes its private manifest and exported public certificate to the supervised Node child; no SQL connection or signing key reaches the browser. Cleanup waits for the exact owned workers, then removes the owned login/database under existing safeguards. On uncertainty, preserve resources for operator review; never bulk-drop by name prefix.

## Required bounded scenarios and evidence

The selected frontend spec owns actual Create→Update→Post, separate server403, stale409, acknowledged Post followed by actual denied Get, and committed-response-loss unknown lock with one dispatch. For loss, `route.fetch({ maxRetries: 0 })` must reach the real host and succeed before `route.abort()` drops the browser response. Do not substitute a successful fixture body. An acknowledged Post remains Posted/read-only even if Get403 fails; an unacknowledged write stays unknown and is not replayed.

Record exact frontend/backend SHAs and publish artifact, Windows/.NET/SQL/Node/browser versions, commands, .NET wrapper rows separately from actual browser test counts, failures, skipped cases and owned cleanup. Retain sanitized local artifacts only; do not commit credentials/tokens/raw traces. Build and discovery do not establish runtime acceptance. B04 cross-module stock/cost oracle, Inventory UI, all B05/F06 acceptance, current attended IME/accessibility, full IIS and production remain separate gates.
