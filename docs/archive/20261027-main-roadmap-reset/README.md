# Main roadmap reset — 2026-10-27

**Source:** [MyDmsVn main at `6eee394263a28c33360877fcf3feb6e0dcdab8ad`](https://github.com/chung6a8m/MyDmsVn/commit/6eee394263a28c33360877fcf3feb6e0dcdab8ad), the merge of [PR #52](https://github.com/chung6a8m/MyDmsVn/pull/52). PR #52 carries completed Sidebar [PR #50](https://github.com/chung6a8m/MyDmsVn/pull/50) and Security [PR #51](https://github.com/chung6a8m/MyDmsVn/pull/51) from the previous work branch into `main`.

**Purpose:** Retire the latest bounded workstream execution plans without inventing a successor roadmap. The current task selector is [`docs/plans/README.md`](../../plans/README.md): **no active implementation plan selected**. This is a documentation archive only; no source, tests, vendors, dependencies, SQL/database, package or permission contracts are changed.

## Retired plans

| Historical workstream | Frozen plan | Source and verification boundary |
|---|---|---|
| Sidebar Navigation — horizontal overflow | [SBN-T00–T02](plans/20261025-001-sidebar-navigation-horizontal-scrollbar.md) | Completed within the Owner-chosen SQL-free hosted-shell and physical100/125 Light/Dark scope. [Sidebar evidence](../../evidence/sidebar/20261025-sidebar-horizontal-scrollbar.md). No real Login, production package, SQL or release acceptance. |
| Security Administration — UI Lab first | [SEC-T00–T08](plans/20261025-002-security-administration-ui-lab-first.md) | Explicit SEC-T05 Owner approval of the reviewed SQL-free candidate (2026-10-26); bounded SEC-T06 Local cutover, SEC-T07 real-SQL checks and SEC-T08 handoff recorded. [UI Lab evidence](../../evidence/security/20261025-security-ui-lab.md) and [Local/SQL evidence](../../evidence/security/20261025-security-local-sql.md). Remaining SEC-T04 review/keyboard, incomplete attended SEC-L01–SEC-L12 matrix and later-revision limits are **OPEN**, not PASS. |

These two full plans preserve task IDs, dated results, failures, commands, hashes and historical status prose. Their old paths under `docs/plans/` are compatibility/heading-anchor pages. Any former "PR Draft", "before gate" or former-branch statements in the frozen bodies describe their original date and are superseded by the merged PR facts above. Relative local links have been rebased; archived task checkboxes are not executable authorization.

## What remains authoritative

- [Architecture and ADRs](../../adr/README.md), [source contracts](../../CONTRACTS.md), [Security](../../SECURITY.md), [Security workspace specification](../../design/SECURITY_ADMINISTRATION_UI.md), [UI Lab runbook](../../development/UI_LAB.md), [test policy](../../TEST_POLICY.md), [test strategy](../../TEST_STRATEGY.md) and [operational runbooks](../../README.md) remain current reference documents, subject to source verification before implementation.
- [Previous archive (2026-10-25)](../20261025-pre-next-roadmap/README.md), [older archive (2026-10-23)](../20261023-pre-ui-next/README.md), [UI-NEXT evidence](../../ui-next/README.md), and existing `docs/evidence/` retain candidate-specific provenance and original limitations; they are **not** future queues.
- Security's approval is a bounded SQL-free design gate; the subsequently merged Local/SQL work has its own limited evidence. A new task needs its own explicit scope, fresh source inspection and selected verification. Neither archival nor merge establishes complete attended, actual Login, physical-display, packaged or production release acceptance.
- [Sales SAL-T08](../../evidence/sal/20261021-t08-integrated-acceptance.md) retains native125, attended and dated Owner acceptance **OPEN**; earlier BCF/BCI/RESP/L2DB/GUI-P5 exceptions remain candidate-specific. [Production release prerequisites](../../DEFERRED_WORK.md#release-prerequisites) remain **UNACCEPTED / UNSCHEDULED**.

## Decision and preservation policy

Do not reactivate old plans, copy unresolved checkboxes into a future backlog or mark old failures PASS. New work requires an explicit Owner selection, a new dated plan and a current-source review. Remove former branch names from **live Codex context and current documentation**, but do not rewrite historical evidence to erase provenance. [Repository instructions](../../../AGENTS.md) and [context](../../../AI_CONTEXT.md) are the active entry points.

Documentation-only verification: inspect the diff, local link targets/anchors, archive/status routing and Markdown encoding; SQL/GUI execution is neither required nor claimed.

## Documentation verification (2026-10-27)

- Comparing the archive branch with inspected `main`: **24 changed Markdown paths only**; no application source, tests, scripts, runtime configuration, Git submodules or dependency pins changed.
- Both archived plan bodies match the original plan text after removing the archive notice and normalizing rebased relative links. **11 Sidebar and 21 Security original section headings** remain on their compatibility pages so existing anchors retain a target.
- **704 relative Markdown link occurrences** across the 24 changed paths resolved to tracked files/directories in the archive branch tree; **zero missing targets**. This verifies local targets, not GitHub rendering or every external URL.
- The prior branch name is absent from refreshed active entry/context pages, plan index, technical guidance and compatibility pages. Historic `docs/HISTORY.md` and archived plan/evidence provenance are preserved deliberately and are not active work instructions.
- This change is documentation-only. **SQL, GUI, product/package and release tests: NOT RUN / NOT CLAIMED** for the archival commit.
