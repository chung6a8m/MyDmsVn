# WEB3 Goods Receipt & Inventory HTTP server companion — archive (2026-11-01)

**ARCHIVED / COMPLETED (bounded).** After accepting the recorded WEB3-06/B04/B05 scope and limitations and merging PR #78, the Owner explicitly requested archival of completed Roadmap 03. All backend B01–B05 are DONE within their recorded scopes. This archive changes documentation routing only; it adds no runtime/test/SQL result or production approval.

Read the [frozen completed backend plan](plans/20261101-002-web3-goods-receipt-inventory-http.md), [unchanged Owner acceptance evidence](../../evidence/web3/20261101-b04-b05-integrated-pilot.md#7-owner-acceptance-and-merge--2026-11-01), [current execution index](../../plans/README.md), [historical handoff/reference](../../development/WEB3_CODEX_HANDOFF.md) and [frontend archive](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/blob/main/docs/archive/20261101-web3-goods-receipt-inventory/README.md). Frontend F task status is independently owned; this backend archive does not duplicate it.

## Accepted source and bounded scope

- Archive baseline: backend `main@1e80634a286074d5fde2764896d3ae31e1b48a9b`, containing the completed Owner closeout. The frozen plan preserves that completed text and all dated checkpoint history, with a historical banner and rebased relative links only.
- Backend [PR #78](https://github.com/chung6a8m/MyDmsVn/pull/78) merged reviewed head `bc8217878f1f66888378a8d2af5cf6e09d06a190` at `07d3f09d94d117e83961b7c0cc0a890dd1f54ef8`. Frontend [PR #17](https://github.com/chung6a8m/MyDmsVn.ReactWebUI/pull/17) merged reviewed head `89387a3fe55cdac22fc7fe20a516b6b68eaa3a3c` at `b67ce456c55c85b2b0303ef04798b8d039f6829d`. The existing acceptance evidence records identical merge/reviewed trees.
- Actual final integrated tested sources remain backend `82f235248b1b570d41ae42f5d8cffd53f8b9f46e` and Web pilot `d2e416f0a99bafe3851b61bbc90771fb10af9907`. Recorded checks remain 54 Inventory SQL, 6 Security HTTP/SQL/provisioning and 26 pure oracle rows PASS, plus final Chromium and Edge each 18 browser cases and 1 wrapper PASS, independent physical Q/V/COGS oracle, guarded cleanup and inspected screenshots. These are historical candidate-specific results, not newly executed tests.

The completed slice retains net48 System.Web, strict JWT/refresh and string ID/decimal wire, authorized Receipt Draft/Post, read-only Inventory projections and independent moving-average/ledger verification. B01 corpus, B02/B03 runtime and B04/B05 verification retain their original ownership; archive status is not a new implementation authorization.

## Accepted limits remain limits

- Backend CI has **0 configured workflows; NOT CONFIGURED / NOT RUN**, accepted as recorded, never CI PASS. Frontend secret-free CI is separately recorded in its own evidence and does not prove backend SQL/net48 acceptance.
- **Full IIS and production: NOT RUN / NOT APPROVED.** IIS Express HTTPS does not establish full IIS or release readiness.
- Current-candidate attended Telex/VNI/Narrator remains **NOT RUN**. Automated typing and inspected PNGs do not substitute for attended input/accessibility.
- Audit retention, distributed limiter, durable drafts/idempotency and operational release requirements remain deferred; JWT in localStorage retains its accepted XSS residual. No automatic write replay, runtime/schema/valuation/grant change or new production authority is introduced.
- **CEX-A01–A05 remains ACTIVE and unchanged** in the current execution index. WEB2 and all earlier evidence retain their original bounded decisions.

## Routing and preservation

The [former plan path](../../plans/20261101-002-web3-goods-receipt-inventory-http.md) retains every original heading anchor and links to its frozen section. Earlier PLANNED/IN_PROGRESS wording inside the frozen plan describes historical checkpoints superseded by its recorded Owner acceptance; it is not a new task queue. New work requires a separately selected scope.

The frozen acceptance paragraph's statement that no physical archive/move was part of that closeout describes the earlier acceptance-only action. The Owner's subsequent archive request authorizes the routing change recorded here; it does not alter the earlier evidence or acceptance limits.

All `docs/evidence/` files remain at their original paths and unchanged byte-for-byte. The [canonical corpus](../../contracts/web3-v1/README.md), [wire design](../../design/WEB3_INVENTORY_HTTP_WIRE.md), [SQL oracle design](../../design/WEB3_STOCK_LEDGER_ACCEPTANCE.md), [integrated runbook](../../development/WEB3_INTEGRATED_PILOT_RUNBOOK.md) and [host runbook](../../../src/MyDmsVn.Server.HttpHost/README.md) remain technical references, not execution authorization. No evidence is copied or rewritten into the archive. Documentation verification covers link/anchor routing, preserved evidence hashes, frozen-content equivalence, encoding and diff only; no runtime suites are run for archival.
