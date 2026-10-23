# UI Next — SME desktop assessment

**Date:** 2026-10-22 (Asia/Bangkok)  
**Requested branch:** `v1-ui-next`  
**Status:** ANALYSIS / PROPOSALS — not an active implementation roadmap

## Purpose

This folder preserves the Owner's assessment request and the source-backed findings needed to continue the discussion across sessions. The goal is a compact, keyboard-friendly WinForms UI for an SME application: consistent Light/Dark appearance, contextual operation feedback, visible field/cell validation, efficient document entry, and less repeated application infrastructure.

The Owner considers the current visual direction appropriate. This is an assessment of the reusable UI foundation and input behavior, not a request to redesign the application or implement every proposal. The only repository changes in this assessment are these documents.

## Read in this order

| Document | Purpose |
|---|---|
| [Source assessment](20261022-ui-foundation-assessment.md) | What exists, why the reported gaps occur, ownership boundaries, and recommended direction |
| [Input and inline-editing contracts](20261022-input-and-inline-editing-contracts.md) | Proposed numeric/date editing, validation, keyboard, grid, and commit/cancel behavior |
| [Candidate backlog and validation](20261022-candidate-backlog-and-validation.md) | Priorities, dependencies, focused evidence, and decisions to make before implementation |

Read the repository [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), [active execution index](../plans/README.md), and [TEST_POLICY](../TEST_POLICY.md) first. These assessment documents do not supersede them.

## Exact inspected baseline

| Repository / component | Inspected revision | Relationship |
|---|---|---|
| `chung6a8m/MyDmsVn` | `9d8a199a27bc505f58b5997fd611ce249c26c00d` | Head of `v1-ui-next` when assessment started |
| `chung6a8m/MyDmsVn.BootstrapSourceGrid` | `2bd7874f7e80b22ef181763749c2a2938ab7f16e` | Application's pinned submodule |
| `chung6a8m/MyDmsVn.Bootstrap5WinFormUI` | `2be9a4d778fda7016207b75470020be6126f2f3f` | Nested UI submodule consumed through BootstrapSourceGrid |
| `chung6a8m/sourcegrid` | `f4e457b43582bf01892f50bdc74aa480531e5944` | Nested SourceGrid pin |
| UI upstream `main` | `33f1476d87f3c0bfd11f5cf4c6688e7edc9423b0` | One commit ahead, with no changed files compared with the consumed UI revision |

The [UI comparison](https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/compare/2be9a4d778fda7016207b75470020be6126f2f3f...33f1476d87f3c0bfd11f5cf4c6688e7edc9423b0) prevents attributing these gaps to an unconsumed newer UI implementation. Findings are source analysis, not newly executed Windows interaction evidence.

The active product target remains `net48`, Windows x86. SAL-T07 source exists at `879ab7b`, but the current execution index still records T07 IN PROGRESS / OPEN, native125 RESOURCE BLOCKED, R4 deferred Minor, and T08 NOT STARTED. Do not turn the phrase "almost finished" or this assessment into acceptance. SAL remains the sole ACTIVE roadmap.

## Findings to retain across sessions

| ID | Finding |
|---|---|
| F01 | Sales uses `BootstrapDatePicker` for its document date and plain `DateTimePicker` for optional From/To filters. The former itself wraps the native picker. |
| F02 | `BootstrapCalendarPicker` already supplies a themed, nullable, date-only selection surface. It does not supply free-text date entry or partial-date completion. |
| F03 | `BootstrapToastContainer`, `BootstrapAlert`, and `ValidationState` already exist. The main gaps are adoption, scope/routing, and field/cell presentation. |
| F04 | Shared notifications contain only Kind/Message; Shell displays the latest message. Catalog and receipt screens also have local labels, so not every message is Shell-only. |
| F05 | Catalog duplicate-code conflicts omit field metadata. The mapper therefore supplies no field path and `DesktopViewModelBase` cannot attach that conflict to the Code editor. |
| F06 | Receipt numeric editing deliberately uses `FormatMode.None`. Stock Numeral formatting can remove meaningful input; simply enabling it would violate current precision and rejection rules. |
| F07 | BootstrapSourceGrid already has text/formatted/lookup editor adapters; BootstrapDataGridView already has `BootstrapLookupColumn`. A new universal grid is not required. |
| F08 | Receipt adapters duplicate generic editing mechanisms. Business-specific save/post/security/identity policies still belong to the application. |
| F09 | Closable-tab infrastructure, generic themed surfaces/text, and a reusable tab busy overlay are useful missing seams; some constituent primitives already exist. |

## Scope and decision status

The Owner explicitly requested stronger inline editing, tab-scoped feedback, invalid states, faster numeric/date entry, and an SME-oriented evaluation. The following are recommendations awaiting selection, not previously approved APIs:

- Evolve the current libraries plus a thin application workspace layer.
- Prioritize contextual feedback and complete validation before broad cosmetic replacement.
- Preserve the current strict numeric behavior until an information-preserving formatter/parser path is proven.
- Add a typed date-entry composition that reuses the custom calendar; retain native date-time behavior for explicit exceptions.
- Extract shared edit mechanisms from receipt and sales incrementally; retain both grid implementations with a common behavioral contract.
- Add generic close-request, busy-overlay, and theme primitives; keep dirty-document and transaction-outcome decisions in MyDmsVn.

Suggested names such as `WorkspaceFeedbackHost`, `FieldValidationBinder`, `BootstrapDateInput`, `BootstrapDecimalInput`, and `BootstrapBusyOverlay` are illustrative. None is asserted to exist in the inspected source.

Preserve the seven sales columns, independent duplicate-Product rows, manually entered prices, gift quantity/cost behavior, strict decimal precision, and authoritative uncertain-Post recovery. Payments, receivables, returns, new pricing/tax workflows, grid replacement, and runtime migration are outside this assessment's implementation authority.

## How to resume

1. Resolve the current branch head and nested submodule pins again. Compare only the code relevant to findings F01–F09; do not silently carry these findings forward if the source changed.
2. Read these documents and the current active roadmap. Keep analysis proposals separate from approved work and from historical acceptance.
3. Record the Owner's next choices in this folder with date, selected behavior, affected repository, and remaining alternatives.
4. Once a bounded work package is selected, create its concrete design/implementation/evidence requirements under the repository's then-current process. Do not activate this entire candidate backlog automatically.
5. Keep upstream changes traceable and explicitly pinned. Record exact source/test/package revisions for subsequent native Windows evidence.

## Verification boundary for this assessment

Inspected source, existing tests, documentation, Git trees, and upstream comparison. No application code, submodule pin, test policy, or active-roadmap state is changed. No Windows GUI, SQL integration, or package acceptance run is claimed. Documentation validation covers changed paths, links, encoding, source references, and proposal-versus-existing API distinctions.
