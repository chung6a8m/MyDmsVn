# WEB3 server companion — Goods Receipt & Inventory over net48 HTTP

**Owner selection:** 2026-11-01, subsequently extended explicitly to backend B01 for WEB3-01. **Status:** COMPLETED (bounded) — Owner accepted WEB3-06/B04/B05 and PR #78 merged on 2026-11-01; all B01–B05 DONE within their recorded scopes. B01 DONE (bounded, merged in PR #75); B02/B03 DONE (bounded implementation and automated verification) under explicit Owner selection of WEB3-03 on 2026-11-01. B02/B03 and the bounded Receipt prerequisite are merged through PR #76/#77; their original [B02/B03 evidence](../evidence/web3/20261101-b02-b03-http-inventory.md) and [Receipt evidence](../evidence/web3/20261101-web3-04-receipt-pilot-fixture.md) retain exact checks and limits. **B04/B05 DONE (bounded) — Owner accepted and [PR #78](https://github.com/chung6a8m/MyDmsVn/pull/78) merged at `07d3f09d94d117e83961b7c0cc0a890dd1f54ef8`**; [integrated evidence](../evidence/web3/20261101-b04-b05-integrated-pilot.md) records actual completed checks, final-candidate verification and retained limits. **Tasks owned here:** `WEB3-B01–B05`. The Web repo owns F01–F06 in its [Roadmap 03](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/plans/20261101-001-web3-goods-receipt-inventory-web.md). **Milestones:** WEB3-01 through WEB3-06 cross-repo map. Never copy or independently mutate F task status here.

[HTTP/JSON authoritative contract](../design/WEB3_INVENTORY_HTTP_WIRE.md) · [SQL/cross-module acceptance](../design/WEB3_STOCK_LEDGER_ACCEPTANCE.md) · [Codex handoff](../development/WEB3_CODEX_HANDOFF.md) · [evidence index](../evidence/web3/README.md) · [WEB2 archived contract](../design/WEB2_HTTP_AUTH_AND_WIRE_CONTRACT.md) · [sole test policy](../TEST_POLICY.md).

## 0. Problem, scope, immutable boundaries

WEB2 delivered a `net48` IIS/System.Web thin HTTP host, `api/v1` Sales/Catalog, short Bearer JWT localStorage access + SQL-backed rotated refresh GUID in HttpOnly/Secure/Strict cookie, isolated restricted SQL principal and a real IIS Express HTTPS/SQL/Chromium/Edge pilot. The Owner accepted **only** full IIS NOT RUN as the WEB2 closeout exception; it is **not** production sign-off. WEB3 extends that host rather than introduces a second backend or changes desktop Local.

Backend already has `CreateGoodsReceiptDraftCommand`, `UpdateGoodsReceiptDraftCommand`, `PostGoodsReceiptCommand`, `GetGoodsReceiptByIdQuery`, `ListGoodsReceiptsQuery`, `GetStockBalancesQuery` and `GetStockCardQuery` under `Server.Application/Inventory`. They are independent of HTTP; their current handlers/validators and typed repositories remain authoritative.

**Selected:** HTTP Inventory routes, strict versioned wire mapping, independent Read/Write/Post/Balance/StockCard capabilities, least-privilege Web principal, SQL+HTTP cross-module tests, owned pilot extension, companion documentation. **Excluded:** SQL business rewrite, new schema unless source-verified deficiency mandates separately reviewed migration, stock adjustment/transfers/returns, generic ledger writes, retrospective costing, new permission seeding, new runtime/ORM, full IIS/prod deployment, durable write journal/idempotency.

**Preserve:** single explicit UoW per mutation; Shared receipt/sale posting + Exclusive reconciliation barrier; sorted stock key locks; append-only ledger, matching valuation entry, Q/V balances atomic, moving-average COGS frozen on Sale Post; actual POST order not receipt business date; SQL-owned DbUp migrations; deny-by-default auth. No controller SQL or trusted client actor grants.

## 1. Current verified source anchors / acceptance caveat

Inspected against `main` when selected; Codex **must re-inspect working main before implementation**:

| Role | Current source |
|---|---|
| Host startup and pipeline | `src/MyDmsVn.Server.HttpHost/Startup.cs`, `Web2Pipeline.cs`, `WebHostOptions.cs` |
| Existing HTTP pattern | `SalesControllers.cs` (`WireApiController` + MediatR), `SalesWireInput.cs`, `SalesWireOutput.cs`, `StrictWireJson.cs` |
| Feature capabilities | `AuthController.cs` current `capabilities?feature=sales`, permission authority `IPermissionAuthorizationService` |
| Source DTOs | `src/MyDmsVn.Contracts/InventoryContracts.cs`; do **not** alter Local transport serializer |
| Commands/validators | `src/MyDmsVn.Server.Application/Inventory/GoodsReceiptFeatures.cs` |
| Queries/validators | `GoodsReceiptQueries.cs`, `InventoryQueries.cs` |
| Security | `InventoryPermissionDefinitions.cs`; existing `Inventory.GoodsReceipts.{Read,Write,Post}`, `Inventory.Balances.Read`, `Inventory.StockCard.Read` |
| SQL/provider | `Server.Infrastructure/Inventory/*`, SQL web principal grants/provisioning source, `StockLedger`/`StockValuationEntries`/`StockBalances` |
| Test owners | `tests/MyDmsVn.Server.HttpHost.Tests`; `tests/MyDmsVn.Server.Infrastructure.Inventory.Tests`; `tests/MyDmsVn.Server.Infrastructure.Security.Tests`; `tests/MyDmsVn.Server.Infrastructure.Migrations.Tests` if migration touched |
| Baseline pilot | `src/MyDmsVn.Server.HttpHost/README.md`, `docs/evidence/web2/20261031-http-jwt-sales.md` |

**Known business differences from Sales:** Receipt Save takes 1–200 lines with `ProductId/Quantity/UnitCost` and unique Product, **no existing line ID in request**; Update replaces its lines; command responds with Draft detail prepared **before transaction commit**. Receipt Post response has only `ReceiptId/Status/PostedAtUtc`, **no version**. Receipt list uses pageNumber/pageSize and total, not Sales cursor. Inventory balances/card are authorized read-only queries with pageNumber/pageSize; StockCard has no running balance in current contract.

## 2. WEB3-B01 — authoritative source/wire/permission freeze (milestone 01)

**State: DONE (bounded) — contract checkpoint merged in PR #75 at `feb3b6cbc3bec3fe505c2b575fd4a8050973524d`; Owner selected subsequent B02/B03 implementation.** Owner explicitly authorized B01 on 2026-11-01. Source baseline `8b599c782816d9cd64f04a45d809a2b8eb1235c8`; [canonical field tables/corpus](../contracts/web3-v1/README.md), [manifest](../contracts/web3-v1/manifest.json), [source/SQL privilege gap matrix](../design/WEB3_B01_SOURCE_CHECKPOINT.md), [execution evidence](../evidence/web3/20261101-b01-contract-checkpoint.md). B01 supplied no runtime endpoint, SQL grant, .NET business source or production approval. At this B01 checkpoint, B02/B03 were separately selected and B04/B05 remained PLANNED; later execution and acceptance are recorded below.

1. Read current `InventoryContracts.cs`, actual DTO constructor/nullable behavior, receipt/stock query services and exact validation. Inventory List: 1..200 pageSize, search <=128 and status Draft/Posted; Balance search <=256 and 1..200 pageSize; StockCard requires positive warehouse/product, optional UTC-range and <=200 pageSize. Confirm these are still true on implementation commit. Establish exact server response names without leaking Local numerical JSON format.
2. Author canonical `WEB3` wire field/route/status corpus in `docs/contracts/web3-v1/` (B01 implementation deliverable; not created by docs activation) with test-owned fixtures/manifest. All int64s/decimals strings; int32s JSON numbers; UTC vs calendar dates; nullable avg vs zero; 1/200 lines, duplication forbidden, version tokens, max boundary. Verify both backend and frontend parse identical authoritative fixtures. Never duplicate authoritative payloads in Web repo.
3. Freeze proposed routes and authorization in `WEB3_INVENTORY_HTTP_WIRE.md`; inspect current `AuthController.Capabilities` to extend whitelist without weakening the Sales shape. Feature-scoped responses: `goods-receipts` {read,write,post}; `inventory-balances` {read}; `inventory-stock-card` {read}. Feature unknown -> deterministic 400; no privilege escalation from UI hints.
4. Reconcile receipt line mapping: Save never accepts line IDs, Update replaces lines, server result in request order/lineNo; frontend owns `clientLineId`; define what mismatch means and reject ambiguous responses. No Product-ID dedup as an implicit map.
5. Inventory API and SQL principal grant gap matrix: list exact tables/views/queries read by existing services and the current restricted principal; no DDL/db_owner or unconditional grant growth.
6. Record field/table code links, reference boundaries, exact source SHAs, selected test cases and any blocking inconsistency. **No API implementation until this gate is clean.**

**Deliverable:** canonical contract + review notes, no fake PASS. Suggested tests: Node corpus syntax/decimal/ID checks, pure .NET mapper fixtures (if created), nonempty discovery. Documentation verification uses link/diff/encoding only.

## 3. WEB3-B02 — net48 HTTP routes and fine-grained capabilities (milestone 03)

**State: DONE (bounded) — thin routes, independent capabilities and restricted SQL grants implemented; 415 HTTP host cases and relevant SQL checks passed.** See [candidate evidence](../evidence/web3/20261101-b02-b03-http-inventory.md). Owner selected WEB3-03 on 2026-11-01. **Depends on:** B01 + approved field tables.

1. Add thin `GoodsReceiptsController` and `InventoryController` in the existing HttpHost assembly (proposed names; inspect source before adding). Use current `WireApiController.Execute<T>`, `IMediator.Send`, request-scoped authoritative actor, HttpJson/Asp.NET envelope and the same Web2Pipeline Bearer/session validation. Do not introduce a second `HttpConfiguration` or independent auth middleware.
2. Expose method set in wire design: GET receipt list/detail, POST Create, PUT Draft, POST Post, GET balances and stock-card. No GET side effects; no POST retry in any host/client layer; anti-CSRF Origin protection for unsafe methods retained.
3. Extend `GET /api/v1/auth/capabilities?feature=...` whitelist and make response projections feature-specific without altering `feature=sales`. Enforce actual permissions in Application per request; test denied Read with allowed Write/Post, revoked grants and superuser unknown/deprecated keys fail closed.
4. Validate URI IDs, body/query members, content type, no duplicate fields, exact Unicode/UTF8, body caps, dates/offsets and ranges **before dispatch**. For GET detail/query reject unexpected body and query keys. Reuse extracted strict parser primitives only if Sales parity tests prove no behavior change; prefer narrow new input/output mapping classes.
5. Assess Web SQL runtime principal least privileges for read-only queries and receipt writes; grant minimum required through existing provisioning/validation source if needed. Keep Desktop/maintenance principals untouched; include negative restricted-principal tests.
6. Preserve existing health, SPA/static routing, CSP nonce, TLS/Origin constraints, rate limit, JWT/cookie rotation and no secrets diagnostics. Do not re-open WEB2 full IIS exception.

**Exit tests:** HTTP route/protocol behavior with no SQL where possible; each relevant existing SQL owner verifies authorization and restricted SQL; existing Sales HTTP/auth negative tests still PASS if common boundary changed.

## 4. WEB3-B03 — strict inventory/receipt wire, SQL parity and query semantics (milestones 03–05)

**State: DONE (bounded) — canonical mapping, query semantics and owned HTTP/SQL parity verified; no schema or business algorithm change.** See [candidate evidence](../evidence/web3/20261101-b02-b03-http-inventory.md) for the corrected test-only SQL oracle and actual passing coverage. At this B03 checkpoint, B04/B05 integrated acceptance remained PLANNED; later acceptance is recorded below. **Depends on:** B01, B02.

1. Implement receipt wire DTO mapper with canonical invariant **string** ID/decimal output, string ReceiptDate `yyyy-MM-dd`, UTC PostedAtUtc, opaque Base64 expectedVersion. Create/Update only include actual Save fields; Update expectedVersion required, ID only from route; Post requires exactly `expectedVersion` body and returns {receiptId,status,postedAtUtc} **without version**. Detail uses source result including display names/active flags; no post-commit read added by controller.
2. Receipt List preserves existing source ordering, 1-based pageNumber/pageSize, count, Warehouse/Status/Search filters. Reject unsupported date/Product filters rather than introducing them incidentally. No mandatory keyset rewrite or business data ordering change.
3. Balances wire includes warehouse/product identity, quantity, inventoryValue and `averageUnitCost:null` when appropriate; StockCard includes ledgerId/documentType/documentId/number/quantityChange/valueChange and genuine UTC timestamp; **do not fabricate historical names, opening or running balances**.
4. Sales/Receipt StockCard document number join remains server authoritative. No direct mutation endpoint for StockBalances, StockLedger, valuation or reconciliation. Trigger/ledger semantics stay as deployed.
5. Test precision >2^53, negative stock movement values, nullable averages, UTC parsing, rowversion 409, duplicate product errors, values at SQL decimal bounds, <=200 lines, no leaked SQL details, response built pre-commit and actor audit.
6. Test local/Application contract parity versus HTTP with owned SQL tests, including header/line order and immutable stock valuation. Validation field paths must match `lines[index].field` for Web mapping.

**Exit:** strict HTTP/SQL contract pass, Catalog/Sales regression on affected shared paths, no SQL migration unless separately required/approved.

## 5. WEB3-B04 — independent cross-module SQL oracle (milestone 06)

**Owner selection — 2026-11-01:** Owner explicitly authorized B04/B05 for WEB3-06: tests/fixture/oracle/documentation and designated non-production SQL + existing IIS Express HTTPS, separate fresh Chromium/Edge fixtures. No business runtime, schema, SQL-grant or production change. Backend baseline `e86335add61cc2d1e936cadad019b96053e4b9ca`, branch `codex/web3-06-integrated`; frontend prepared candidate `73f560e20ffeefc0ee32bf1d327ebce7c8bc918c`. Earlier PLANNED statements within completed work packages describe their prior checkpoints, not the current selection.

Execution sequence:

- [x] Extend the existing owned Security browser fixture with explicit integrated mode and an independent per-key SQL oracle; retain legacy Sales/Receipt regression selectors and complete posted-document set accounting.
- [x] Run focused Inventory valuation/post/concurrency/rollback cases, then Security HTTP/SQL parity; pure oracle controls separately. Actual results: 54 Inventory SQL + 6 Security SQL + 26 pure rows PASS, no failed/skipped rows; no new SQL runner or grants.
- [x] Finish latest review-corrected pilot verification. Published net48 host + production Web build and final Web pilot `d2e416f0a99bafe3851b61bbc90771fb10af9907` passed each browser with 18 cases and 1 wrapper, physical oracle/cleanup and PNG review, including retained-control count 1 and hidden assertions. Prior candidates and the review correction remain recorded separately; runtime artifacts are unchanged.
- [x] Complete latest-candidate live reconciliation and final evidence handoff. Final scope review has no gap; re-review of the test correction has no further actionable source issue; [new B04/B05 evidence](../evidence/web3/20261101-b04-b05-integrated-pilot.md) records candidate `82f235248b1b570d41ae42f5d8cffd53f8b9f46e`, completed focused checks, final Chromium/Edge PASS, inspected screenshots and independent physical oracle/cleanup, with historical attempts preserved. Frontend F06 state stays exclusively in its own plan. The subsequent Owner acceptance is recorded below; production remains separately NOT APPROVED.


**State: DONE (bounded) — Owner accepted and PR #78 merged 2026-11-01. Dependencies verified:** B02/B03 + Web F04/F05 integrated candidate.

1. Seed a fresh positively owned non-production schema11 SQL database with Warehouse/Product/Employee and authorized users, optional independent Sales actor. Use existing SQL test helper or WEB2 owned pilot; no production SQL. Configure only `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` pointing to designated non-production `master` for SQL tests.
2. Post initial receipt, query balances/card; Post Sale on same stock key, query balances/card; post another Receipt after sale and assert previously posted Sales cost remains immutable. Verify quantity/value balance and ledger/valuation rows independently of Application projections. Verify distinct stock keys/warehouses, exact rounding and integer/fractional limits.
3. Verify concurrent same-receipt Post (one economic movement only), stale update/post version and unauthorized mutation leave SQL unchanged; distinct actor/session principal audit; ledger append-only; failed Post rollback of status, Q/V, ledger/valuation and audit as applicable. Exercise Shared/Exclusive valuation synchronization using existing deterministic SQL concurrency patterns; don't invent sleeps without lock handshakes.
4. Example acceptance math (new isolated scenario, **not** executed result): opening Q=10,V=100; Receipt +10 @20 ⇒ Q20,V300; Sale -4 ⇒ COGS60, Q16,V240, while original sale cost is frozen even if a later receipt changes average. Assert against rows, not screenshots.
5. Use current six-owner SQL policy: run affected Inventory/Security/etc sequentially, don't infer PASS from skipped SQL tests, keep test-owned cleanup and failed allocation evidence.

**Exit:** independent SQL oracle records exact candidate SHA(s), posting order, equations, query and physical-row checks; focused concurrency/rollback/authorization PASS, no false production claim.

## 6. WEB3-B05 — packaged HTTPS pilot, release boundary and handoff (milestone 06)

**State: DONE (bounded) — Owner accepted and PR #78 merged 2026-11-01. Dependencies verified:** B02–B04, Web F06 build.

**Bounded WEB3-04 prerequisite extension, Owner 2026-11-01 — verified:** Owner explicitly authorized "Cho phép mở rộng fixture và chạy nghiệm thu thật" for frontend F03/F04 Receipt acceptance. The separate Receipt selector reuses the existing owned pilot lifecycle, explicit ordinary Receipt actors and a separate Receipt stock key; the original Sales-only selector remains separate. [Runbook](../development/WEB3_RECEIPT_PILOT_RUNBOOK.md) owns manifest/results/execution. [New evidence](../evidence/web3/20261101-web3-04-receipt-pilot-fixture.md) records the two first Chromium failures, reviewed fixture/test corrections, then fresh Chromium and Edge each **15/15 browser PASS + 1/1 wrapper PASS**, independent physical SQL receipt Q4.0001/V80.0020 and preserved Sales/COGS, with guarded cleanup. Exact tested fixture `ed532fccb267cb5e543bd6b19bbf016265b255e1`, Web runtime `66764fb1687519ea502dcc9c4e3a2e4ecb99fc67`, pilot-only fix `3fbbe60bb0896d344f3e0a7d4ac0b8d0e3b1d516`. This narrow prerequisite does not complete B04 or all B05, authorize production/business/schema/grant changes, claim F05/F06 or attended acceptance, or constitute Owner closeout. Further live work still requires its selected scope and exclusive SQL coordination.

1. Extend existing `WebHostIisExpressTests` / browser fixture as appropriate, not a second SQL test runner. Publish net48 host with `Publish-Web2Host.ps1` into an **empty** destination, copy separately built Web `dist` to `wwwroot`. Use existing HTTPS localhost certificate already trusted, explicit operator input, restricted random SID-tracked SQL login and owned disposable database.
2. Exercise real Chromium **and** Edge through production build, sequential fresh fixture per browser. Auth/login/refresh/cookie/SameSite/CSP/Origin and cross-actor fencing; receipt Create/Update/Post and Stock Balance/Card reads; Sales alongside receipt; JSON contract error/denies; browser outcome → independent SQL oracle. No fake ports, TLS disable, rate-limit bypass or golden fixture substitution.
3. Keep current worker/job tree drain/owned SQL cleanup. No secret/credential/token/HAR/trace captured into committed evidence. If environment missing, record BLOCKED/NOT RUN with exact prerequisite, not PASS.
4. Add minimal build/test CI only when runner supports net48; no new full SQL machine requirement. Distinguish documentation verification from runtime/source tests.
5. Record candidate-specific evidence in new `docs/evidence/web3/`, with links to Web evidence and failed trials. WEB2 counts and IIS Express proof are historical, not substitute for WEB3 acceptance.
6. Archive only this WEB3 plan after Owner explicitly accepts its bounded results; retain contract/design tests. **Full IIS, operational audit retention, distributed multi-worker limiter, durable draft/idempotency, current-candidate attended IME/accessibility, production release remain deferred/not approved**.

### WEB3-06 / B04–B05 Owner acceptance — 2026-11-01

Owner accepted the recorded verified scope and limits, authorized both PR merges and requested completion. **B04/B05 DONE (bounded); backend WEB3 companion COMPLETED (bounded), all B01–B05 DONE within their recorded scopes.** Backend [PR #78](https://github.com/chung6a8m/MyDmsVn/pull/78) merged reviewed head `bc8217878f1f66888378a8d2af5cf6e09d06a190` as `07d3f09d94d117e83961b7c0cc0a890dd1f54ef8`; its tree is unchanged. [Acceptance evidence](../evidence/web3/20261101-b04-b05-integrated-pilot.md#7-owner-acceptance-and-merge--2026-11-01) owns exact paired frontend merge identity, source/tree checks, existing test counts and the Owner decision.

This accepts the recorded 54 Inventory SQL + 6 Security SQL + 26 pure rows and final Chromium/Edge each 18 browser + 1 wrapper PASS. Backend CI has 0 workflows and is accepted as NOT CONFIGURED / NOT RUN, not PASS; frontend CI is independently recorded. No new runtime/test execution is implied by this documentation closeout. Full IIS/production, operational limits, attended input, durable drafts/idempotency and JWT-localStorage risk remain as recorded. No physical archive/move or CEX status change is part of this closeout.

## 7. Coordination with independent active CEX workstream

MyDmsVn `docs/plans/README.md` also selects **CEX-A01–A05 Editable Lines Composition and SAL Adoption**, owned by WinForms. WEB3 is an **additional parallel, independently authorized** server workstream. Do not archive CEX, change WinFormsKit gitlink, modify SourceGrid/WinForms visuals, or make CEX acceptance a WEB3 prerequisite. Avoid shared source collisions through PR sequencing; evaluate resulting main in each implementation PR.

## 8. Review cadence, STOP conditions, evidence

- Phase gate: B01 contract before implementation; B02/B03 host/DTO tests before Web F03 live adoption; B04+F06 integrated pilot after both modules implemented. Keep implementation PRs small and title with precise B IDs.
- Every change lists affected authoritative files, independent failing/regression case(s), exact commands, TRX/browser counts and relevant cross-repo commit SHA. Test selectors must discover >0 required cases; skipped required SQL is BLOCKED.
- STOP and request Owner scope decision for full IIS/production, schema change, ledger valuation algorithm change, new auth storage, new general UI/HTTP framework, DB grants beyond least privilege, operation replay/idempotency, other Inventory writes or security policy changes.
- Mark task `DONE (bounded)` only with required source and actual verification evidence, not when docs PR merges. Missing attended/prod gates remain explicitly unverified.
