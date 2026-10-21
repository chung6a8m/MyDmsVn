# Development status — v1a-pro

**Updated:** 2026-10-22. [The execution index](plans/README.md) is the sole roadmap. This page distinguishes implemented behavior, accepted evidence and planned refinement; it does not define another task order.

## Implemented baseline

| Area | Current state | Scope qualification |
|---|---|---|
| Foundation, Local Login/session and security | Implemented | Preserve original accepted exceptions and source-defined permissions |
| Catalogs and Inventory | Implemented | Products, Warehouses, Employees, Customers, Goods Receipt Draft/Post, ledger/balances/card |
| RESP | Complete in approved scope | [Accepted fixtures and lifecycle](RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| L2DB | Complete in approved scope | [Accepted net48 persistence/package smoke](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| GUI-P5 | Complete in Owner-approved scope | [Final GUI evidence](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); preserve the signed T05 Down exception |
| BCI / receipt inline editing | Complete in accepted UI scope; PR #40 merged at `6040d7b` | Native Product/Quantity/Unit-cost cell editors, app-owned adapter, strict numeric parsing and writable accessibility exist; [final BCI scope](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) |
| BCF lookup and visual refinement | COMPLETE / OWNER ACCEPTED in bounded UI scope | [Final candidate, comparisons and explicit exceptions](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) |
| RELEASE-MANIFEST-01 | Closed narrowly | Metadata reflects schema 9/scripts 001–009; no inference of full installed-release acceptance |

Product and all ten active test projects target `net48`. Two shipped package directories are Windows x86 Desktop.App and the separate admin DbMigrator. AppCore/net8 is parked. Persistence, explicit UoW, SQL fixture ownership and removed-runner boundaries remain unchanged.

## Approved next implementation — SAL

[Direct Sales and moving weighted average](DIRECT_SALES.md) is ACTIVE from PR #41 baseline `35ba2e8a3f37fb07095701eb94f4b56f7941c491`. [SAL-T00–T08](plans/20261021-002-direct-sales-and-moving-average-costing.md) cover arithmetic/contracts, schema10/bootstrap, valued receipt/reconciliation, sales Draft/Post, LINQ/keyset, Local/VM, seven-column UI and frozen acceptance. T00 foundations and [T01 schema/bootstrap checkpoint](evidence/sal/20261022-t01-schema-bootstrap.md) are complete at `b5a2f3f` / `81e8aab`; [T02 valued inventory](evidence/sal/20261022-t02-valued-inventory.md) is complete at `47165ded`; [T03 Draft/detail](evidence/sal/20261022-t03-sales-draft.md) is complete at `1164199`; [T04 atomic Post](evidence/sal/20261022-t04-atomic-sales-post.md) is complete at `7eeafa4`; [T05 keyset queries](evidence/sal/20261022-t05-sales-keyset-queries.md) are complete at `f9149a6`; [T06 Local/session/ViewModels](evidence/sal/20261022-t06-local-session-viewmodels.md) is complete at `5fbd6f5`; T07 is IN PROGRESS per the execution index. Full SAL remains incomplete.

Approved additions include manual price, repeated Product rows, gifts, Product filtering and cost/profit snapshots. Payments, debt and returns stay excluded. The planning/shipped baseline remains schema9/quantity-only stock; reviewed T01 development source implements schema10 and narrow validated exact9 SQL-login/restricted-maintenance admission. T02 receipt posting/reconciliation/stock reads use the shared valuation protocol; T04 Sales Post completes that source protocol; Native UI, frozen integration/package/Owner and deployment acceptance remain incomplete. No intermediate task is a standalone deployment candidate. A10–A12 have bounded T01 PASS; multi-task [SAL acceptance](DIRECT_SALES_ACCEPTANCE.md), T08 revalidation and Owner acceptance remain open.

## Completed BCF handoff

BCF-T00–T06 are COMPLETE / OWNER ACCEPTED in the [final bounded scope](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21). All three reference comparisons, exact source/test/package evidence, visual differences, six wide125 NOT TESTED, deferred T04 Minor and native100 composite limitation have explicit disposition. SAL is now the active successor; BCF is not restarted. Existing English copy, data fields, permissions, Save/Post/recovery, row identity and native inline editing are preserved.

## Retired scope and production boundary

The owner removed the unimplemented REL/P6 plan and deferred modern-runtime procedure on 2026-10-20. The former future-work checklists remain retired. See [History](HISTORY.md#2026-10-20--business-compact-roadmap-reset).

Operational support decisions, installation/recovery evidence, legacy credential support and production sign-off remain [unaccepted release prerequisites](DEFERRED_WORK.md#release-prerequisites). Their removal from the roadmap is a scheduling decision, not a PASS or waiver. The T01 migrator accepts Empty/exact10 and narrowly validated exact9 under its documented maintenance boundary; automatic upgrade from an old 001–008 prefix remains unsupported.

## Evidence discipline

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) owns behavior coverage; [tests/README](../tests/README.md) owns direct commands. Documentation-only work requires diff/link/status/encoding checks. SAL implementation uses focused tests and T08 frozen integrated evidence; BCF gates remain historical. Historical totals and blocked/skipped scenarios cannot establish a new runtime result.
