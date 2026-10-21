# Development status — v1a-pro

**Updated:** 2026-10-21. [The execution index](plans/README.md) is the sole roadmap. This page distinguishes implemented behavior, accepted evidence and planned refinement; it does not define another task order.

## Implemented baseline

| Area | Current state | Scope qualification |
|---|---|---|
| Foundation, Local Login/session and security | Implemented | Preserve original accepted exceptions and source-defined permissions |
| Catalogs and Inventory | Implemented | Products, Warehouses, Employees, Customers, Goods Receipt Draft/Post, ledger/balances/card |
| RESP | Complete in approved scope | [Accepted fixtures and lifecycle](RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| L2DB | Complete in approved scope | [Accepted net48 persistence/package smoke](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| GUI-P5 | Complete in Owner-approved scope | [Final GUI evidence](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); preserve the signed T05 Down exception |
| BCI / receipt inline editing | Complete in accepted UI scope; PR #40 merged at `6040d7b` | Native Product/Quantity/Unit-cost cell editors, app-owned adapter, strict numeric parsing and writable accessibility exist; [final BCI scope](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) |
| List/header lookup binding | Existing defects identified | Filter query may clear; unchanged-key header reselect/restore may reset caret; BCF repairs all three named hosts |
| RELEASE-MANIFEST-01 | Closed narrowly | Metadata reflects schema 9/scripts 001–009; no inference of full installed-release acceptance |

Product and all ten active test projects target `net48`. Two shipped package directories are Windows x86 Desktop.App and the separate admin DbMigrator. AppCore/net8 is parked. Persistence, explicit UoW, SQL fixture ownership and removed-runner boundaries remain unchanged.

## Current selected change

[BCF — lookup correctness and visual parity](plans/20261021-001-business-compact-lookup-and-visual-parity.md) is **ACTIVE / PLANNED** on the merged PR #40 baseline. All BCF implementation tasks are NOT STARTED and new runtime obligations are NOT RUN. Its [design](BUSINESS_COMPACT_REFINEMENT.md), [acceptance ledger](BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) and [reference register](design-references/business-compact/README.md) define one two-phase queue.

Phase 1 reproduces manual per-key typing and repairs `WarehouseFilterLookup`, `WarehouseLookup` and `EmployeeLookup`, including raw text, caret/range, committed identity and async boundaries. Native100/125 Light/Dark and attended lookup evidence must pass at T02 before visual work.

Phase 2 completes the shared shell/theme, Products toolbar/results/inspector and guarded detail-focused receipt presentation. This deliberately expands the former BCI catalog exclusion and always-visible receipt split. It preserves existing English UI, data fields, permissions, Save/Post/recovery, row identity and native inline editing. T06 closes only on new frozen evidence and Owner review against all three retained designs.

## Retired scope and production boundary

The owner removed the unimplemented REL/P6 plan and deferred modern-runtime procedure on 2026-10-20. The former future-work checklists remain retired. See [History](HISTORY.md#2026-10-20--business-compact-roadmap-reset).

Operational support decisions, installation/recovery evidence, legacy credential support and production sign-off remain [unaccepted release prerequisites](DEFERRED_WORK.md#release-prerequisites). Their removal from the roadmap is a scheduling decision, not a PASS or waiver. The current migrator accepts Empty/Compatible schema states; automatic upgrade from an old 001–008 prefix remains unsupported.

## Evidence discipline

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) owns behavior coverage; [tests/README](../tests/README.md) owns direct commands. Documentation-only work requires diff/link/status/encoding checks. BCF implementation uses focused tests, its T02 input gate and T06 frozen Windows/package milestone. Historical totals and blocked/skipped scenarios cannot establish a new runtime result.
