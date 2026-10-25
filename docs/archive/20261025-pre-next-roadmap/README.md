# Archive boundary — 2026-10-25, before the next roadmap

**Requested scope:** archive documentation on `v1a-pro-ui-next` to prepare the next roadmap. **Inspected product HEAD:** [`6ee6951c58f6e4ad92f991521ff5c553d2d22526`](https://github.com/chung6a8m/MyDmsVn/commit/6ee6951c58f6e4ad92f991521ff5c553d2d22526), merge of [PR #48](https://github.com/chung6a8m/MyDmsVn/pull/48). **Current scheduling:** [no active execution plan](../../plans/README.md); the next roadmap has not been selected.

This boundary retires the old execution documents. It preserves implemented behavior, decisions, exact-candidate evidence and unresolved limitations. It does not define the next backlog or change runtime/test acceptance.

## Archived documents and compatibility

| Original location | Archived record |
|---|---|
| `docs/plans/README.md` | [Frozen former selector](plans/README.md) |
| `docs/plans/20261024-001-a0-a1-sales-ui-lab.md` | [A0/A1 implementation and verification](plans/20261024-001-a0-a1-sales-ui-lab.md) |
| `docs/ui-next/20261022-ui-foundation-assessment.md` | [Foundation assessment](ui-next/20261022-ui-foundation-assessment.md) |
| `docs/ui-next/20261022-input-and-inline-editing-contracts.md` | [Input/inline-editing assessment](ui-next/20261022-input-and-inline-editing-contracts.md) |
| `docs/ui-next/20261022-candidate-backlog-and-validation.md` | [Original candidates and validation](ui-next/20261022-candidate-backlog-and-validation.md) |
| `docs/ui-next/20261023-active-roadmap.md` | [UI-NEXT roadmap](ui-next/20261023-active-roadmap.md) |
| `docs/ui-next/20261023-design-and-control-policy.md` | [UI-NEXT design/control policy](ui-next/20261023-design-and-control-policy.md) |
| `docs/ui-next/20261023-implementation-plan.md` | [UI-NEXT implementation plan](ui-next/20261023-implementation-plan.md) |
| `docs/ui-next/20261023-validation-and-handoff.md` | [UI-NEXT validation/handoff](ui-next/20261023-validation-and-handoff.md) |
| `docs/ui-next/20261024-dateinput-unified-compact-design.md` | [DateInput design](ui-next/20261024-dateinput-unified-compact-design.md) |
| `docs/ui-next/20261024-dateinput-unified-compact-correction.md` | [DateInput correction](ui-next/20261024-dateinput-unified-compact-correction.md) |
| `docs/ui-next/20261024-dateinput-unified-compact-codex-prompt.md` | [Retired DateInput prompt](ui-next/20261024-dateinput-unified-compact-codex-prompt.md) |

The twelve records preserve the source snapshot's prose, tables, statuses and fenced commands; only an archive notice and relative-link rebasing are applied. Eleven original document paths now contain compatibility links with their original heading anchors. The live `docs/plans/README.md` remains the stable scheduling selector. Evidence backlinks and external bookmarks can still reach their recorded sections.

Historical phrases such as “current plan,” “review pending,” “NOT STARTED” and branch handoffs below describe their recorded dates. The merge facts in the next section clarify later events without rewriting candidate evidence. The [UI-NEXT landing page](../../ui-next/README.md), [current documentation index](../../README.md) and [archive index](../README.md) route readers to the appropriate records.

## Source and merge facts at this boundary

| Scope | Fact at archival | Evidence boundary |
|---|---|---|
| A0 documentation cleanup | [PR #46](https://github.com/chung6a8m/MyDmsVn/pull/46) merged at `d827bbbfdf2a55c1d55537cf7c4b3a4963234aa3`. | Documentation verification only; original source/runtime NOT RUN status remains. |
| A1 Sales UI Lab | [Product PR #47](https://github.com/chung6a8m/MyDmsVn/pull/47) merged at `6c385d7630b6b21e915e244113c214aa616327f6`. The historical plan's product “review pending / neither merged” wording is superseded by this merge fact. | Initial 27/27 and corrected 32/32 belong to their distinct candidates in the archived plan; this archive does not rerun or transfer those results to HEAD. |
| Separate Docs publication | [MyDmsVn.Docs PR #1](https://github.com/chung6a8m/MyDmsVn.Docs/pull/1) remains OPEN / UNMERGED when inspected on 2026-10-25, at head `2b9686c33a04eb416c2b9863ee83da53d1c8ccdc`. | Product PR #47 merging does not imply the separate Docs PR merged or authorize other phases. |
| A3 WinFormsKit consumer | [PR #48](https://github.com/chung6a8m/MyDmsVn/pull/48) merged at the inspected HEAD. The sole app gitlink is `vendor/MyDmsVn.WinFormsKit@c232a4c09dc0386a5bd8bb484a5c991acae827d5`; SourceGrid remains `f4e457b43582bf01892f50bdc74aa480531e5944`. | Source cutover is merged. The pinned [A3 plan](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/c232a4c09dc0386a5bd8bb484a5c991acae827d5/docs/plans/20261024-001-a3-winformskit.md) and [verification ledger](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/c232a4c09dc0386a5bd8bb484a5c991acae827d5/docs/verification/20261024-a3-bootstrap.md) own toolkit/consumer evidence and limits. |

The current product remains net48 with the Local production composition. Lab scenarios remain developer fakes. UI library assembly identities remain compatible; the old nested BootstrapSourceGrid/UI checkout paths are historical. [UI Lab usage](../../development/UI_LAB.md), [project structure](../../PROJECT_STRUCTURE.md), [technical stack](../../TECH_STACK.md), business contracts, ADRs and operational/test runbooks remain current technical references. Cross-repository organization decisions remain owned by [MyDmsVn.Docs](https://github.com/chung6a8m/MyDmsVn.Docs).

## Retained acceptance limits

- **A1:** corrected new Ui.Tests 32/32 belongs to `ede1116256d34a2e8d5902244698e9e183b62e29`. Earlier Desktop.Session 217/217, DateInput 2/2 and lookup 16/16 remain initial-candidate results. Lab smoke was 96 DPI; fakes do not prove Server permissions, SQL valuation, atomic Post, actual Login, native125, package, attended or Owner acceptance. No old tests were retired.
- **A3 consumer:** candidate `01457f6a64ed8a96647cc3f851d8f17791503c50` records locked restore/Release build, Architecture 13/13 and focused native Desktop 86/86 PASS. Full Desktop at earlier `20c6a6e` remains **FAIL: 787 pass, 26 fail, 21 skip / 834 discovered**. The earlier x86 ReceiptAcceptance probe's 4/4 failures and their reproduction on `6c385d7` remain failures; the separate 4/4 editor/lookup probe is distinct. Full Desktop, x86 and rollback were not rerun at `01457f6`; SQL, packaged/attended GUI, physical DPI and Designer remain NOT RUN there. Historical rollback restore/build and 2/2 smoke apply only to `20c6a6e`.
- **Toolkit evidence:** preserve each source/test candidate, failed trial and scoped rerun in the pinned ledger. Migration evidence from `8cfa253` / `0f4e273` is a separate earlier experiment, not the required `6c385d7` consumer result. Its obsolete runner/approval-file commands do not apply to this product branch.
- **UI-NEXT / DateInput:** retain [T13's attended-fixture FAIL after Owner Save and separate passing control](../../ui-next/evidence/20261024-uin-t13-integration.md), and [DateInput's first-candidate Owner failure and later bounded visual acceptance at 125%](../../ui-next/evidence/20261024-dateinput-unified-compact-correction.md). Keep physical-monitor/window-DPI distinctions and exact candidate revisions.
- **SAL:** [T08](../../evidence/sal/20261021-t08-integrated-acceptance.md) keeps product/package `f5039cb` distinct from GUI-test `4697744`; native125, attended observations and dated Owner acceptance remain **OPEN**.
- **Earlier baselines:** BCF retains its six strict wide125 NOT TESTED rows, T04 Minor and other signed limitations; GUI-P5 retains Warehouse 125% Dark Down NOT TESTED. BCI/BCF/RESP/L2DB/GUI-P5 completion remains limited to recorded scope.
- **Release:** [prerequisites](../../DEFERRED_WORK.md#release-prerequisites) remain **UNACCEPTED / UNSCHEDULED**. Product source cutover and documentation archival grant no release approval.

## Preservation and next-work boundary

Existing files under `docs/archive/20261023-pre-ui-next/`, `docs/evidence/` and `docs/ui-next/evidence/` stay at their original paths with identical Git blob content. Original failures, exceptions, commands, source/test/package revisions and design rationale are preserved. Current source, test code, project/configuration files, dependency locks and gitlinks are outside this documentation change.

There is no active successor task list. Unfinished acceptance and previously proposed phases are not automatically carried forward; the already merged A1/A3 source is not reset. Any future selected work needs its own scope based on the then-current source and original evidence. Historical prompts and deleted REL/P6/runtime directions do not become execution instructions.

## Documentation verification

Checks on the archival candidate against the inspected HEAD:

- **40 changed paths, all Markdown:** 26 existing documents updated and 14 archive/index documents added; no source/test/configuration/gitlink delta.
- **134 Markdown files / 2,676 local path-and-anchor occurrences:** no unresolved target, anchor or reference definition. Source/asset paths were checked against the complete repository tree at the inspected HEAD.
- **12 archived bodies and 11 compatibility pages:** original prose, task/status tables and commands match after accounting only for the archive notice and relative-link rebasing; all original heading anchors remain available.
- **52 protected archive/evidence files:** identical normalized Git blob content; original locations retained.
- **13 fenced code/command blocks in changed original documents:** preserved in their retained or archived destination. Changed Markdown is UTF-8 without BOM, CRLF in the working tree and normalized LF in Git blobs.
- **Copy-aware whitespace check:** `git diff --cached --find-copies-harder -C --check` PASS. Existing two-space Markdown hard breaks in copied historical assessments remain intact.

These checks validate documentation and preservation only. The commit and completion report identify the final checked delta.

Restore, build, product tests, SQL and GUI were **NOT RUN — documentation-only scope** under [TEST_POLICY](../../TEST_POLICY.md#2-choose-scope-from-the-changed-behavior). No new runtime outcome is asserted.
