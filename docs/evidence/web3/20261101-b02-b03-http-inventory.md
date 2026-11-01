# WEB3-B02/B03 — Goods Receipt and Inventory HTTP implementation

**Owner selection:** WEB3-03 backend B02/B03, 2026-11-01. **Result:** DONE within the implemented and automated-verified B02/B03 scope; local candidate is not merged or deployed. The [backend plan](../../plans/20261101-002-web3-goods-receipt-inventory-http.md) owns status; B04/B05 and frontend F tasks are separate.

## Candidate and scope

- Base: main at feb3b6cbc3bec3fe505c2b575fd4a8050973524d (B01 merged PR #75). Branch: codex/web3-03-backend. Implementation/test commit: d2e7e3f3709a7e97cf91c7d4558c560303fa498d. Subsequent documentation-only commit does not change this tested runtime.
- Canonical web3-v1 corpus remains byte-for-byte unchanged from B01; its manifest sourceBaseline is 8b599c782816d9cd64f04a45d809a2b8eb1235c8. Manifest/checkpoint runtime wording describes that historical contract-only revision, not this implementation.
- Frontend coordination reference: WEB3-02 main merge c2a7e4fdd3ba029db7c064ce81915f394fbffd65. No frontend source or frontend runtime/browser test was changed/run for B02/B03.
- Windows 11, .NET SDK 10.0.303, net48 Release tests; Node 22.14.0. HTTP uses the existing OWIN TestServer/production pipeline; SQL uses the explicitly configured nonproduction master connection via MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING. No credentials, database identifiers or raw TRX are committed.

## Implemented boundaries

Seven thin routes dispatch the existing authorized Application queries/commands through WireApiController and MediatR. Feature-scoped capabilities add goods-receipts, inventory-balances and inventory-stock-card while preserving Sales. Independent permissions remain authoritative per request; unknown/multiple feature inputs fail closed.

Feature-local input/output mapping implements int64/count/decimal strings, positive int32 numbers, calendar dates, UTC Z with up to seven fractional digits, canonical eight-byte Base64 rowversions, strict member/query/UTF-8/content rules and explicit nullable fields. Receipt lines preserve request order; Update replaces server line IDs. Duplicate Product remains Application validation with GoodsReceipt.DuplicateProduct and lines[1].productId, never silent deduplication. Create/Update emit the existing authoritative pre-commit DTO without a follow-up Get; Post has no version. Balances/Card preserve real quantity/value/nullable average and signed movements; no synthesized running balance.

SQL provisioner and runtime overgrant validator add only GoodsReceipts SELECT/INSERT/UPDATE, GoodsReceiptLines SELECT/INSERT/DELETE and GoodsReceiptNumberSequence UPDATE. Tests reject line UPDATE (including a column grant), header DELETE and the existing overgrant cases. No schema, valuation algorithm, Local serializer, Sales handler, WinForms or dependency pin changes.

## Executed checks

| Scope | Actual result | Local artifact |
|---|---|---|
| Canonical corpus tests | 8 PASS, 0 FAIL/SKIP | node:test console |
| Corpus validator | 32 fixtures, 29 positive, 68 negative, 85 scalar controls, 4 rounding controls, 601 expanded lines, 15 lifecycle assertions; hashes valid | validator console |
| HTTP RED before implementation | 150 executed: 140 FAIL, 10 PASS, 0 SKIP; absent routes/capabilities and mapper stubs | TestResults/web3-inventory-red/inventory-red.trx |
| Full HTTP host GREEN | 415 PASS, 0 FAIL/SKIP | TestResults/web3-host-green/host-green.trx |
| Restricted principal provisioning | 8 PASS, 0 FAIL/SKIP | TestResults/web3-principal/web3-principal.trx |
| Existing Inventory command/query SQL parity | 32 PASS, 0 FAIL/SKIP | TestResults/web3-03-inventory-parity/inventory-parity.trx |
| HTTP plus real SQL, initial run | 19 executed: 18 PASS, 1 FAIL, 0 SKIP; test-only oracle identifier | TestResults/web3-http-sql/web3-http-sql.trx |
| Corrected HTTP/SQL happy-path oracle | 1 PASS, 0 FAIL/SKIP; completes all 19 selected HTTP/SQL cases | TestResults/web3-http-sql-corrected-oracle/web3-http-sql-corrected-oracle.trx |

Full HttpHost GREEN covers 88 Inventory endpoint and 63 wire cases plus 264 existing Sales/Catalog/auth/transport/SPA cases. The wire suite gained one explicit duplicate-Product-through-Application case after RED, accounting for 151 new GREEN versus 150 initial RED cases. Test duration reported by runner: HTTP 4 seconds, principal 37 seconds, Inventory 1 minute 55 seconds. Inventory SQL selector executed 27 command, 3 Goods Receipt query and 2 Inventory query cases.

HTTP/SQL scope selects 5 new Goods Receipt cases, 7 existing Sales cases and 7 existing auth cases. The new receipt happy-path oracle initially failed at an unquoted T-SQL LineNo identifier after Create/Update succeeded. It was corrected to [LineNo] and rerun in isolation: 1/1 PASS, 0 SKIP. The initial batch took 2 minutes 2 seconds; the focused rerun took 15 seconds. No runtime change was needed. Initial failure evidence is retained, not reported as a green first run.

The completed SQL happy path independently observes replacement IDs/order, no stock effects before Post, stale-version 409 with unchanged state, two matching ledger/valuation rows, aggregate Q=5/V=39, server actor audit and card movement Q=3/V=33. Get/List/Local detail and card agree. Other cases verify denied/revoked operations leave rows unchanged, write/Post succeeds without Read, 200 unique lines and receipt/line IDs above 2^53, maximum unit cost, duplicate Product and precision rejection, missing grants, and valuation INSERT failure rolling back header/lines/balances/ledger/value. Existing Sales tests retain duplicate/gift lines, costing, Catalog, Local parity and rollback; auth tests retain refresh/session/revocation/concurrent actor semantics.

Effective selected coverage is **474 distinct .NET cases PASS** (415 host + 8 principal + 32 Inventory + 19 HTTP/SQL) plus 8 Node tests, with no skipped required cases. Principal and HTTP/SQL runs made 28 fresh owned schema11 allocations including the failed first attempt; all completed migration and awaited guarded disposal, with no cleanup failure. The Inventory owner additionally completed its existing fresh/reuse fixture lifecycle, including reported APP reset/cleanup/drop success. Required SQL ran against the designated nonproduction configuration; no orphan cleanup or unsafe prefix-only drop was used.

## Commands

Run from the backend worktree. Each locked restore succeeded; builds and SQL owners ran sequentially. Commands below use the actual selected projects and filters; selectors are test coverage, not a new blanket suite gate.

~~~powershell
node --test docs/contracts/web3-v1/validate-corpus.test.mjs
node docs/contracts/web3-v1/validate-corpus.mjs

dotnet restore tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~InventoryEndpointTests|FullyQualifiedName~InventoryWireTests' --logger 'trx;LogFileName=inventory-red.trx' --results-directory TestResults/web3-inventory-red
dotnet test tests/MyDmsVn.Server.HttpHost.Tests/MyDmsVn.Server.HttpHost.Tests.csproj -c Release -f net48 --no-restore --logger 'trx;LogFileName=host-green.trx' --results-directory TestResults/web3-host-green

dotnet restore tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebPrincipalProvisioningTests' --logger 'trx;LogFileName=web3-principal.trx' --results-directory TestResults/web3-principal

dotnet restore tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Server.Infrastructure.Inventory.Tests/MyDmsVn.Server.Infrastructure.Inventory.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~GoodsReceiptCommandIntegrationTests|FullyQualifiedName~GoodsReceiptQueryIntegrationTests|FullyQualifiedName~InventoryQueryIntegrationTests' --logger 'trx;LogFileName=inventory-parity.trx' --results-directory TestResults/web3-03-inventory-parity

dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebGoodsReceiptHttpSqlTests|FullyQualifiedName~WebSalesHttpSqlTests|FullyQualifiedName~WebAuthHttpSqlTests' --logger 'trx;LogFileName=web3-http-sql.trx' --results-directory TestResults/web3-http-sql
dotnet test tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj -c Release -f net48 --no-restore --filter 'FullyQualifiedName~WebGoodsReceiptHttpSqlTests.Restricted_HTTP_create_replace_post_and_queries_match_physical_SQL_and_Local' --logger 'trx;LogFileName=web3-http-sql-corrected-oracle.trx' --results-directory TestResults/web3-http-sql-corrected-oracle
~~~

Documentation verification: git diff --check passed; all 230 local links across nine changed Markdown files resolved; UTF-8 without BOM and working-tree CRLF passed. Canonical corpus, Application/Contracts business source and vendor pins have no diff against the base. Raw TRX remains local and ignored.

## Limits and handoff

An independent source reviewer found no actionable implementation issue in routes, wire, permissions or SQL grants. Verification is bounded to automated net48 HTTP/TestServer and owned SQL cases. B04 cross-module Receipt → Sale → later Receipt acceptance, B05 IIS Express/Chromium/Edge integrated pilot, attended UI/IME, full IIS and production are NOT RUN for this candidate. Existing Sales regression coverage does not substitute for B04/B05. No browser acceptance or frontend Receipt implementation is implied.

The [host runbook](../../../src/MyDmsVn.Server.HttpHost/README.md) records the exact additional grants and operator provisioning step for an existing Web database. B01 corpus is ready for pinned frontend consumption; the [handoff](../../development/WEB3_CODEX_HANDOFF.md) directs subsequent owners to the implementation and evidence. Full IIS, durable operation recovery/idempotency and operational release gates remain outside this selection.
