# WEB4-05 — recovery HTTPS/browser fixture

Owner selected frontend WEB4-05/F04 on 2026-11-02 and explicitly authorized this bounded backend test fixture plus fresh nonproduction SQL/IIS Express pilot. The [backend plan](../plans/20261101-003-web4-reliability-server.md#6-b05--focused-sql--integrated-https-browserphysical-oracle-in-progress) owns B05; [evidence](../evidence/web4/20261102-b05-recovery-pilot.md) owns executed results. No product runtime, schema, principal grants, machine TLS configuration or production deployment is changed.

## Selector and ownership

`WebHostIisExpressTests.Published_React_WEB4_recovery_preserves_journal_and_physical_stock`

This opt-in Security.Tests selector reuses the existing owned fixture lifecycle, with current `DatabaseMigrationRunner.MigrateWeb` creating exact12 and host mode `web4`. It creates a fresh ownership-tracked database, a random SID-tracked restricted SQL login, hidden IIS Express process and isolated configuration. The existing provisioner supplies its existing schema12 minimum journal grants. Existing WEB2/WEB3 selectors retain frozen11 and `legacy-web11`.

Use the already designated nonproduction master connection and existing trusted localhost:44321 TLS binding; no TLS bypass or trust/binding creation. Ports44321/18081 must be free. Run Chromium and Edge separately with one SQL owner at a time. The existing browser supervisor has a five-minute maximum, one worker and zero retries. Browser output is sanitized; trace/HAR/video must remain disabled. The exact owned process tree drains before login SID/session verification and database ownership cleanup. Uncertainty fails and retains resources; never bulk-drop by prefix.

## Private manifest and controls

The manifest keeps `origin`, `actors`, `catalog`, `receiptCatalog`, `integratedCatalog`, and `resultPath` from the previous fixture, and adds:

```json
{
  "mode": "web4-recovery",
  "recovery": {
    "version": 1,
    "deploymentEpoch": "<initial server GUID>",
    "controlPipe": "<full owned Windows pipe path>",
    "controlToken": "<ephemeral test token>"
  }
}
```

Only the new WEB4 specs execute in this mode; it does not execute historical unkeyed specs with the keyed client. The manifest remains outside served assets and is deleted after the run. Browser code receives no SQL connection, JWT signing key or fixture control token. Playwright's Node driver alone uses the controls.

Each control opens one new pipe connection and exchanges one frame: four-byte unsigned little-endian UTF-8 byte length, followed by a JSON object. Reuse the strict framing already implemented by `WebFrontendPortBridge`; it rejects oversized/truncated frames, duplicate properties and trailing JSON. The pipe ACL admits only the current Windows user, requires its random token, has at most32 requests and a finite cancellation budget. There is no arbitrary SQL, actor or database parameter.

- `{id: 1, token, action: "rotateEpoch"}` rotates this owned database once and returns `{id: 1, ok: true, deploymentEpoch}`. The guarded SQL UPDATE waits behind existing journal epoch locks; previous committed journal identities retain their original epoch.
- `{id: 2, token, action: "setWritePermission", granted: false}` sets only ordinary fixture actor `integratedEditor`'s existing `Sales.Receipts.Write` grant. Use `true` to restore it. The reply is `{id: 2, ok: true}`.

The fixture awaits and drains the control task before SQL cleanup. These are administrative fault injections in the test owner, not production API endpoints.

## Result and independent oracle

Write one object `{version: 1, mode: "web4-recovery", posted: [], operations: [], checks: []}` to `resultPath`.

`posted` retains the strict historical formats: Sales `{saleId, quantity, cost}`; Receipt `{documentType: "GoodsReceipt", receiptId, quantity, value, actor}`; integrated rows add `scenario: "integrated", actor: "integratedEditor"`, and use the appropriate explicit document type. IDs and four-place decimals are strings. Record every physical Post exactly once, including a Post with suppressed acknowledgement, and no rejected/Draft operation.

Every committed operation, including Draft-only Create/Update, has a row:

```json
{
  "actor": "integratedEditor",
  "operationId": "<UUIDv4>",
  "operationKind": "Sales.Create",
  "requestTargetId": null,
  "resultDocumentId": "1",
  "requestFingerprint": "<lowercase SHA-256>",
  "deploymentEpoch": "<epoch at dispatch>"
}
```

Create retains null original target; Update/Post target equals result identity. The backend compares every row and the complete actor/key set against physical `WebOperationJournal`; it requires all six command kinds but does not assume a fixed count. It independently compares all physical source IDs against Create keys, every Posted ID against exactly one Post key, current source actor/status, full Posted union and reported quantity/value/cost.

The raw SQL oracle checks every source line has exactly one matching ledger/value companion, matching warehouse/product, quantity, audit actor and posting stamp, no Draft movements, extra or orphan rows, and only fixture stock keys. It reconstructs moving average in actual ledger posting order with independent decimal arithmetic, including cumulative duplicate/gift line allocation, full depletion, frozen eight-place unit cost, four-place COGS/revenue and final Q/V. It does not call product valuation/projection helpers. The arithmetic is scoped to the small pilot quantities, not a new general extreme-decimal proof.

`checks` is an exact unique set: `six-command-recovery-sales`, `six-command-recovery-receipt`, `valuation-and-inventory`, `epoch-headers`, `absent-and-denied`, `corrupt-and-mismatch`, `storage-failure`, `actor-isolation`, `security`, `epoch-switch`, `same-key-concurrency`, `live-cross-tab`. These completion markers do not replace browser assertions or observed executed counts.

## Preparation and execution

Build a stable exact frontend candidate and review both fixture/spec diffs before execution. From the backend checkout:

```powershell
dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet build tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore
./src/MyDmsVn.Server.HttpHost/Publish-Web2Host.ps1 -OutputPath '<new empty absolute directory>' -SkipBuild
```

Copy exact frontend production `dist` into that newly published `wwwroot`. Set process-local `MYDMSVN_WEB_TEST_PUBLISH_DIRECTORY`, `MYDMSVN_WEB_TEST_BROWSER_DIRECTORY`, existing `MYDMSVN_WEB_TEST_TLS_THUMBPRINT`, designated `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` and `MYDMSVN_WEB_PILOT_PROJECT=chromium`. Never print credentials or use application databases.

```powershell
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --no-build --filter FullyQualifiedName~Published_React_WEB4_recovery_preserves_journal_and_physical_stock --logger 'trx;LogFileName=web4-chromium.trx' --results-directory TestResults/web4-chromium
```

Repeat with `edge` and a new result path; each invocation owns a fresh DB. Missing inputs skip the opt-in row and are NOT RUN, never PASS. SQL-free validators use `FullyQualifiedName~WebRecoveryPilotResultTests`; existing result/framing regressions remain separate selectors. Record actual browser counts separately from the single .NET wrapper row, source/artifact hashes, browser/runtime versions, failed attempts and cleanup. Full IIS, attended IME/Narrator, real process death at Commit and product same-key resend remain outside this fixture's evidence.

The fixture writes `sql-environment.json` beside its sanitized browser output using the already owned SQL connection. It contains only ProductVersion, ProductLevel and Edition, with no server/database name or connection material. Hosted `backend-web.yml` runs only the four SQL-free validator classes with required TRX/class guards; it does not run this live selector.
