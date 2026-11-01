# ADR 0013 — Extend the net48 HTTP host with Goods Receipt and read-only Inventory

- **Date:** 2026-11-01
- **Decision owner:** MyDmsVn Owner (Roadmap 03 direction)
- **Status:** Accepted **for bounded Roadmap 03 scope and architecture direction**; implementation, actual wire field freeze, testing and production acceptance are **PENDING**.
- **Relates:** [ADR 0001](0001-local-and-remote-adapters.md), [0002](0002-explicit-unit-of-work.md), [0004](0004-p5-ledger-and-posting.md), [0011](0011-direct-sales-and-moving-average-costing.md), [0012](0012-web2-net48-host-preflight.md); [WEB3 server plan](../plans/20261101-002-web3-goods-receipt-inventory-http.md), [WEB3 wire design](../design/WEB3_INVENTORY_HTTP_WIRE.md).

## Context

WEB2 established a thin net48 System.Web HTTP host with JWT/SQL session and Sales/Catalog API, with scoped IIS Express browser/SQL evidence. It has not yet demonstrated reuse by a second editable module. Goods Receipt has implemented Application Create/Update/Post/Get/List contracts, and Inventory has authorized read-only Balance/StockCard queries. Both Goods Receipt and Sales write stock/valuation through one transaction/locking protocol, making cross-module integration a stronger architecture test than another unrelated CRUD screen.

## Decision

1. **Extend**, not replace, the WEB2 net48 HTTP host and current Application/Infrastructure. Do not build a second backend, move Server.Application logic to controllers, upgrade runtime or reintroduce a different ORM.
2. Add versioned **HTTP-specific** Goods Receipt and Inventory DTO/mappers (strict int64/decimal strings, dates, rowversions, independent statuses). Local/Desktop transport-neutral DTOs retain their existing serialization. B01 must freeze exact field tables/corpus before endpoint implementation.
3. Use the existing code-defined permission registry and server actor pipeline for Receipt Read/Write/Post and Inventory Balances/StockCard Read; frontend feature capabilities are hints. No client actor authority, role migration/auto-grant or broad SQL principal privileges.
4. Preserve Goods Receipt's **unique-Product** / replacement-line rules distinct from Sales' legal duplicates/gifts. Preserve Goods Receipt Post acknowledgement **without a rowversion**; no fabricated response or auto-retry, no new idempotency journal.
5. Keep Inventory Balance/StockCard strictly **read-only**. Preserve transaction-owned posting, sorted stock locks, immutable sale costs, append-only quantity/value ledger and moving-average valuation. Cross-module acceptance must independently verify physical Q/V/ledger/COGS on nonproduction SQL, not solely the HTTP projections.
6. ReactWebUI may extract **minimal multi-module workspace/session boundaries**, not a general form or business controller; frontend task state is owned by its separate plan. Desktop WinForms CEX remains an independent active workstream and is not superseded.
7. Full IIS, production rollout, operational audit retention/multiworker rate limiting, durable drafts/idempotency and attended current-candidate input testing remain outside WEB3 unless separately authorized; WEB2 full IIS exception is neither inherited as PASS nor converted into an implementation obligation.

## Consequences / tradeoffs

**Advantages:** validates Clean Architecture reuse, shared authentication, multi-feature permissions and stock/valuation correctness through a second HTTP use case. Lower risk than changing SQL/domain or rebuilding frontend infrastructure.

**Costs:** requires strict per-feature wire/contracts and additional multi-module workspace work; new frontend regression boundaries; extra restricted-SQL queries/grant review; real hosted browser acceptance. An HTTP Post without ack remains ambiguous in the absence of a durable operation journal. Existing WEB2 localStorage access JWT retains XSS exfiltration residual risk.

**Rejected:** copying Sales store/DTO as Receipt, exposing generic stock CRUD, automatically retrying failed writes, forcing Goods Receipt keyset pagination, deploying full IIS as an implicit WEB3 prerequisite, replacing the UI stack or moving valuation rules to React.

## Proof and implementation status

This ADR is a **planning decision only**, not evidence that routes/controller, feature registry or SQL tests exist. B01 source/fixture reconciliation gates B02/B03; B04/B05 and Web F06 require real HTTP/SQL/browser, independent physical oracle, focused regression and candidate-specific evidence under the single test policy. Preserve original WEB2 and CEX historical evidence/status; do not rewrite them.
