# Development status — v1a-pro

**Updated:** 2026-10-20. [The execution index](plans/README.md) is the sole roadmap. This page distinguishes implemented behavior, accepted evidence and planned UI work; it does not define another task order.

## Implemented baseline

| Area | Current state | Scope qualification |
|---|---|---|
| Foundation, Local Login/session and security | Implemented | Preserve original accepted exceptions and source-defined permissions |
| Catalogs and Inventory | Implemented | Products, Warehouses, Employees, Customers, Goods Receipt Draft/Post, ledger/balances/card |
| RESP | Complete in approved scope | [Accepted fixtures and lifecycle](RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| L2DB | Complete in approved scope | [Accepted net48 persistence/package smoke](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| GUI-P5 | Complete in Owner-approved scope | [Final GUI evidence](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); preserve the signed T05 Down exception |
| Receipt editing | Read-only line grid with external editors | App-local accessibility wrappers, pending-query preservation and uncertain-Post reload lock exist; true inline cells do not yet exist |
| RELEASE-MANIFEST-01 | Closed narrowly | Metadata reflects schema 9/scripts 001–009; no inference of full installed-release acceptance |

Product and all ten active test projects target `net48`. Two shipped package directories are Windows x86 Desktop.App and the separate admin DbMigrator. AppCore/net8 is parked. The active persistence, explicit UoW, SQL fixture ownership and removed-runner boundaries remain unchanged.

## Current selected change

[BCI — Business Compact and inline Goods Receipt](plans/20261020-001-business-compact-inline-goods-receipt.md) is ACTIVE / PLANNED. Its [design](BUSINESS_COMPACT_UI.md) and [acceptance matrix](BUSINESS_COMPACT_ACCEPTANCE.md) define the shell/receipt layout, SourceGrid-hosted lookup/numeric editors, app-owned edit sessions, accessibility and new-candidate evidence.

All implementation tasks remain unexecuted. BCI changes desktop presentation and input coordination for existing receipt fields. It does not schedule a persistence migration, new stock fields, HTTP host or runtime reactivation.

## Retired scope and production boundary

The owner removed the unimplemented REL/P6 plan and deferred modern-runtime procedure on 2026-10-20. The former future-work checklists are retired. See [History](HISTORY.md#2026-10-20--business-compact-roadmap-reset).

Operational support decisions, installation/recovery evidence, legacy credential support and production sign-off remain [unaccepted release prerequisites](DEFERRED_WORK.md#release-prerequisites). Their removal from the roadmap is a scheduling decision, not a PASS or waiver. The current migrator accepts Empty/Compatible schema states; automatic upgrade from an old 001–008 prefix remains unsupported.

## Evidence discipline

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) owns behavior coverage; [tests/README](../tests/README.md) owns direct commands. A documentation-only refresh requires diff/link/status/encoding checks. BCI implementation uses focused tests and its named Windows/package milestone. Historical totals and blocked/skipped scenarios cannot establish a new runtime result.
