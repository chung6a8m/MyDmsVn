# Execution-plan index

**Updated:** 2026-10-28 · **Inspected archive source:** [`main@57a45491d97b2efb15e79b1140b52e876641167d`](https://github.com/chung6a8m/MyDmsVn/commit/57a45491d97b2efb15e79b1140b52e876641167d) · **Status:** BUSINESS COMPACT EXC-T05 TESTS/PHYSICAL-100 PASS; PHYSICAL-125 AND OWNER APPROVAL PENDING

## Current plan

**Selected on 2026-10-28:** the [three selected Business Compact exceptions](20261028-001-business-compact-exceptions.md) plan owns the narrow disabled-action, Security modal and read-only multiline corrections. The [selected design](../design/BUSINESS_COMPACT_EXCEPTION_CLOSURE.md) records the implementation boundary; [candidate evidence](../evidence/ui-labs/20261028-business-compact-exceptions.md) separates implemented source, compiler checks, Windows/native execution and Owner approval. The task starts from app `5386a6d8efe23099c436eb7ef62141df8d3896b8` and toolkit pin `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; the Windows code/test candidates are app `f492a7f87ac3cff5d6eac91bf9430899dc48e925` and toolkit `a71d64bd82eb40123eafef4c5a6832e53d7ef9ca`, with final toolkit evidence pin `01672482250ed1f5a1ed99f10250efa697d78638`. Focused native tests and physical-100 Light/Dark normal/minimum review passed; physical 125% and Owner approval remain pending. The exceptions remain OPEN until the selected evidence supports closure.

The [Business Compact and independent UI Labs plan](../archive/20261028-business-compact-and-independent-ui-labs/plans/20261027-001-business-compact-and-independent-ui-labs.md) is archived with UIL-T00–UIL-T08 complete within their recorded bounded scopes. Its dated UIL-T04/T06 approvals, UIL-T07 product-host parity and UIL-T08 handoff remain tied to their exact candidates and exceptions; the historical runtime/test handoff is `8ec1ff71ef0a6a7cf4befa88bbd7bed2616827a0`.

**Retained decision — focused Owner confirmation is PENDING.** The separately selected [2026-10-28 source focus correction](../evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md#2026-10-28-review-findings-follow-up) at `3b5f95e2d7d1414f0ffe97f47a3bdef58f33d786`, retained in the inspected HEAD, is implemented and focused-verification complete. Archival preserves this outstanding decision; it does not approve the correction or reopen the full UIL native matrix.

The later test-only corrections for [Issue #59](../evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md#issue-59--native-close-observer-cleanup) at `bc2a2f1b5f3361782b6fd6b77d07a323008e01bf` and [Issue #60](../evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md#issue-60--lab-close-journal-ownership-assertion) at `57a45491d97b2efb15e79b1140b52e876641167d` are complete within their recorded implementation and focused-verification scopes. Issue #59 was closed as `completed` on explicit Owner direction; that tracking action and the test-only results do not grant native approval or resolve the pending source-correction confirmation.

The [archive boundary](../archive/20261028-business-compact-and-independent-ui-labs/README.md) preserves these distinctions. The durable [Business Compact policy](../design/BUSINESS_COMPACT.md) and [Lab specification](../design/INDEPENDENT_UI_LABS.md) remain current contracts. Archived tasks, open acceptance and completed handoffs do not select additional implementation, SQL, actual Login, package, release or deployment work.

Use [AGENTS](../../AGENTS.md) → [AI_CONTEXT](../../AI_CONTEXT.md) → this index → the explicitly authorized scope and selected dated plan when present → [TEST_POLICY](../TEST_POLICY.md). Current [Architecture](../ARCHITECTURE.md), [ADRs](../adr/README.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md) remain valid references.

## New-roadmap activation policy

1. Select the new scope explicitly with the Owner. Inspect the then-current `main` source, relevant gitlinks and actual tests; do not use an old branch name as the base of new context.
2. Create a **new dated plan** under `docs/plans/` that owns its status, concrete tasks, dependencies, UI Labs/design approval when applicable, source/runtime boundaries and focused acceptance evidence. Add the plan link here **only when selected**.
3. Use historical failures/limits as risk references, **not** inherited work authorizations or automatic PASS. Preserve the sole test policy, separate SQL/GUI gates, explicit evidence and release safeguards.
4. At the next archive, freeze completed bounded execution owners, leave compatibility anchors, refresh AGENTS/AI_CONTEXT and keep original evidence immutable.

## Historical plans and preserved limits

| Boundary | Records and limitations |
|---|---|
| [2026-10-28 Business Compact and independent UI Labs](../archive/20261028-business-compact-and-independent-ui-labs/README.md) | Completed bounded UIL-T00–UIL-T08; separate source focus correction confirmation **PENDING**; Issue #59/#60 test-only corrections complete within their recorded scopes. |
| [2026-10-27 main reset](../archive/20261027-main-roadmap-reset/README.md) | [Sidebar plan](../archive/20261027-main-roadmap-reset/plans/20261025-001-sidebar-navigation-horizontal-scrollbar.md) (hosted-shell only) and [Security plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md) (Owner-approved SEC-T05 plus bounded Local/SQL; incomplete SEC-T04/attended acceptance). |
| [2026-10-25 archive](../archive/20261025-pre-next-roadmap/README.md) | UI-NEXT and A0/A1, retained A3 consumer and candidate-specific limitations |
| [UI-NEXT historical index](../ui-next/README.md) | DateInput and T13 candidate failures, corrections and bounded results |
| [2026-10-23 archive](../archive/20261023-pre-ui-next/README.md) | Earlier plans including uncompleted SAL native125/attended/Owner acceptance |
| [Release prerequisites](../DEFERRED_WORK.md#release-prerequisites) | UNACCEPTED / UNSCHEDULED — never inferred from a source merge |

Evidence must remain tied to its source/test/package hashes, environment and selected scope. Documentation-only archival or activation has no product acceptance or SQL/GUI pass of its own.
