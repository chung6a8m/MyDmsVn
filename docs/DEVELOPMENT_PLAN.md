# Development status — v1a-pro-ui-next

**Updated:** 2026-10-24. The [UI-NEXT roadmap](ui-next/20261023-active-roadmap.md) remains the sole execution queue, originally activated from merged PR #43 at `7ef3aa0887528e19d7e2b45bb3b61cb5f4d53639`. This page distinguishes implemented source, historical acceptance and proposed work; it defines no separate task order.

## Implemented baseline

| Area | Current state | Scope qualification |
|---|---|---|
| Foundation, Local Login/session and security | Implemented | Preserve source-defined authorization, actor/session boundaries and historical exceptions |
| Catalogs and Inventory | Implemented | Four catalogs, Goods Receipt Draft/Post, quantity/value ledger, balances/card and reconciliation |
| Direct Sales and valuation | Source implemented and merged | Draft/Post, seven-column workspace, keyset queries and schema10; archived acceptance remains incomplete |
| RESP | Complete in approved scope | [Accepted fixtures and lifecycle](RESPAWN_VERIFICATION.md#pr-36-review-follow-up) |
| L2DB | Complete in approved scope | [Accepted net48 persistence/package smoke](L2DB_VERIFICATION.md#l2db-t07--frozen-revision-acceptance-and-package-smoke) |
| GUI-P5 | Complete in Owner-approved scope | [Final GUI evidence](GUI_P5_VERIFICATION.md#final-t06-acceptance--2026-10-20); preserve the signed T05 Down exception |
| BCI / receipt inline editing | Complete in accepted UI scope | App-owned native inline adapter, parsing and accessibility; [signed scope](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) |
| BCF lookup and visual refinement | COMPLETE / OWNER ACCEPTED in bounded scope | [Exact candidates and explicit exceptions](evidence/bcf/20261021-t06-integrated-acceptance.md#final-t06-owner-acceptance--2026-10-21) |
| RELEASE-MANIFEST-01 | Closed narrowly on its historical package | Schema9/scripts001–009 metadata proof does not establish full installed-release acceptance |

Product and all ten active test projects target `net48`. Package forms remain Windows x86 Desktop.App and separate admin DbMigrator; AppCore/net8 is parked. Development source requires schema10 and the common receipt/sale/reconciliation valuation protocol. Persistence, explicit UoW, security and test-resource ownership remain binding.

## Active work — UI-NEXT

The [design and control policy](ui-next/20261023-design-and-control-policy.md) and [implementation plan](ui-next/20261023-implementation-plan.md) cover workspace-owned feedback, field/cell validation, typed input, tab close and busy presentation, and semantic theme surfaces. UIN-T00–T13 are VERIFIED in their recorded bounded scopes and exact revisions; preserve the [T13 ledger](ui-next/evidence/20261024-uin-t13-integration.md), original failures and later PR review correction evidence.

**Current work: DateInput correction UIN-D01–UIN-D03**, continuing application PR #45 on `codex/uin-t13`. The Owner approved [01 — Unified Compact](ui-next/20261024-dateinput-unified-compact-design.md) on 2026-10-24. Follow the [active correction plan](ui-next/20261024-dateinput-unified-compact-correction.md) for segment-preserving caret behavior, unified border/icon-only trigger, exact dependency pins and affected-host verification. Correction implementation is **NOT STARTED**, runtime checks are **NOT RUN**, and Owner visual acceptance of the implemented candidate remains pending. The design and documentation handoff establish no runtime PASS.

Shared UI owns reusable control rendering/theme/lifecycle. MyDmsVn retains workspace/session/document identity, dirty/close and operation policy, validation mapping, security denial and uncertain-Post recovery. No new business workflow, runtime, provider or production-release initiative is activated.

## Archived work boundary

The [archive boundary](archive/20261023-pre-ui-next/README.md) retires the previous plan queue and excludes its unfinished work. SAL R3/R4 source fixes were reviewed/approved at `f5039cb`; [T08 integration evidence](evidence/sal/20261021-t08-integrated-acceptance.md) records product/package `f5039cb` and separate GUI test `4697744`, with candidate-specific core/SQL/package/native100 runnable automation PASS. Native125, attended observations and dated Owner acceptance remain OPEN. Archiving does not complete, waive or silently reschedule those obligations.

The [former index](plans/README.md) and [SAL plan](archive/20261023-pre-ui-next/plans/20261021-002-direct-sales-and-moving-average-costing.md) retain historical task provenance. Existing Sales rules remain in their [business design](DIRECT_SALES.md), [costing](DIRECT_SALES_COSTING.md), [query/UI](DIRECT_SALES_QUERY_AND_UI.md) and [acceptance ledger](DIRECT_SALES_ACCEPTANCE.md). Existing BCF fields, English copy, permissions, Save/Post/recovery, row identity and native input behavior remain preserved source contracts.

## Production and evidence boundaries

Operational installation/recovery, support decisions, legacy credential support and production sign-off remain [UNACCEPTED / UNSCHEDULED release prerequisites](DEFERRED_WORK.md#release-prerequisites). The deleted REL/P6 and modern-runtime procedures are not restored. The migrator still admits Empty/exact10 and narrowly validated exact9 under its documented maintenance boundary; old prefixes001–008 and partial/unknown schemas remain refused.

[TEST_POLICY](TEST_POLICY.md) owns execution scope; [TEST_STRATEGY](TEST_STRATEGY.md) owns coverage; [tests/README](../tests/README.md) owns commands. UI-NEXT follows its [scoped validation and handoff](ui-next/20261023-validation-and-handoff.md), without inherited SAL-T08 or full six-owner fresh/reuse repeats. Documentation-only work requires diff/link/status/encoding checks. Historical totals and blocked/skipped scenarios cannot establish a new runtime result.
