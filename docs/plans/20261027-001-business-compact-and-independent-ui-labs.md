# Business Compact conformance and independent UI Labs — active roadmap

**Selected:** 2026-10-27 · **Status:** ACTIVE / implementation NOT STARTED · **Task prefix:** `UIL`
**Inspected source:** [`main@8de4451573e8a9a033d7e5a0b07e5f22ef3018b9`](https://github.com/chung6a8m/MyDmsVn/commit/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9), after [PR #53](https://github.com/chung6a8m/MyDmsVn/pull/53).
**Goal:** Make Business Compact and the pinned WinFormsKit the repeatable basis for UI work, and let Sales and Security be launched and reviewed independently in an environment that reproduces the product's presentation.
**Architecture:** Two thin net48 WinExe launchers; the existing UI Lab project becomes a shared library with neutral host infrastructure and separately owned module forms/tools. Keep the current production WinForms assembly, one scenario assembly and one UI test assembly initially. Both Labs use the product's theme/bootstrap and real production controls with fake ports.
**Tech stack:** .NET Framework 4.8, WinForms, existing Bootstrap5WinFormUI/BootstrapSourceGrid identities from pinned WinFormsKit, existing xUnit/STA helpers and direct `dotnet test`.
**Specifications:** [Business Compact policy](../design/BUSINESS_COMPACT.md), [independent UI Labs](../design/INDEPENDENT_UI_LABS.md), [Security UI and behavior](../design/SECURITY_ADMINISTRATION_UI.md).

This is the sole task/status owner for the selected workstream. The Owner approved the design direction and requested this documentation PR and active roadmap. That approval selects the work; it is **not** native approval of a new candidate, implementation evidence, permission to merge this PR, or production-release acceptance. This PR changes documentation only. Implementation starts with `UIL-T00` after the roadmap is adopted; subsequent work follows the dependencies below.

## 1. Current source and reason for the work

The current [UI Lab program](../../samples/MyDmsVn.Desktop.UiLab/Program.cs) starts one [UiLabForm](../../samples/MyDmsVn.Desktop.UiLab/UiLabForm.cs). Its Sales/Security selector defaults to Sales; Security review therefore traverses a Sales startup. The same form owns both modules' fixture/control fields and developer tools. Some [Security review tests](../../tests/MyDmsVn.Desktop.Ui.Tests/SecurityReviewScenarioTests.cs) reproduce that sequence. Splitting only the dropdown's initial selection would leave the unwanted runtime coupling.

The product [FoundationShellForm](../../src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs) installs and switches [BusinessCompactAppearance.CreateTheme](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs). The Lab's theme switch instead uses `BootstrapTheme.CreateDefault`. Calling `BusinessCompactAppearance.Apply` on a workspace styles from the current theme; it does not install the product theme. A Lab screenshot can consequently differ from product presentation even when both hosts use the same workspace control.

Security already uses production `SecurityWorkspaceControl`/`SecurityWorkspaceViewModel`, toolkit tabs/buttons/radios, custom lists, native search/filter controls and native dialogs. It already has tests, reference SVGs and dated evidence. The selected work makes the remaining component choices, compact geometry, command hierarchy and native visual review explicit. It is not a second implementation of Security business behavior.

| Baseline item | Inspected value / consequence |
|---|---|
| WinFormsKit gitlink | `c232a4c09dc0386a5bd8bb484a5c991acae827d5` at `vendor/MyDmsVn.WinFormsKit`; retained Bootstrap5WinFormUI/BootstrapSourceGrid assembly and namespace identities |
| Nested SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944`; no blanket replacement or upgrade |
| UI Lab solution | [MyDmsVn.UiLab.sln](../../MyDmsVn.UiLab.sln) currently lists the mixed launcher, Scenarios and Ui.Tests projects; new launchers below are proposed |
| Scenario source | [ScenarioInfrastructure](../../samples/MyDmsVn.Desktop.UiLab.Scenarios/ScenarioInfrastructure.cs) mixes reusable infrastructure with Sales-specific types; source ownership can be separated within the existing assembly |
| Test ownership | [Ui.Tests project](../../tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj) links existing small native helpers; [assembly policy](../../tests/MyDmsVn.Desktop.Ui.Tests/AssemblyInfo.cs) disables parallelization |
| Existing integration | Production already composes the reviewed Security control through Local ports; there is no legacy Security factory cutover left to repeat |

## 2. Scope and constraints

Deliver two startup projects, `MyDmsVn.Desktop.UiLab.Sales` and `MyDmsVn.Desktop.UiLab.Security`, in the same repository and UI Lab solution. Each starts directly in its module, owns only that module's runtime graph, lists only its scenarios/tools and remains within that module through reset/session replacement/close. Keep a shared neutral host for theme, presentation bounds, journal plumbing and lifecycle mechanics. Retain the old `MyDmsVn.Desktop.UiLab` assembly identity as a class library to minimize friend-access churn; keep module forms/tools in explicit Sales/Security source folders.

Share the production theme construction and bootstrap contract, including startup and Light/Dark switching, typography, DPI behavior and measured workspace client area. Lab toolbar/journal space must not silently reduce the reviewed product viewport. Use actual shell normal/minimum geometry as the reference, not a screenshot canvas size.

Adopt the durable Business Compact policy, a source-verified control map and one focused repository UI skill. Review a native Role permissions view and one account create/edit dialog as the pilot before expanding the visual changes across Security. Complete the current candidate's native approval before final product-host parity and handoff.

The following constraints apply to every task:

- Preserve Security ownership, permission semantics, canonical keys, draft/save/recovery behavior, revision chaining, keyboard/caret behavior, close admission and session lifetime in the existing [Security specification](../design/SECURITY_ADMINISTRATION_UI.md). No permission CRUD, new bulk contract, SQL migration, automatic grants or authentication change.
- Keep SQL-free Labs composed from production presentation/application contracts plus fakes. No Local, Server, DbMigrator, SQL fixture or real Login configuration in either Lab's runtime graph; no sample/fake dependency in production composition.
- Keep one scenario assembly and one Ui.Tests assembly initially. Source organization and runtime independence are required; full compilation isolation, a production assembly split and mass migration of existing tests are not selected.
- Preserve cancel → release/cancel owned gates → finite drain with a live message pump → UI-thread disposal → replacement. A timeout poisons that host instance and prevents a new graph. Reset remains a developer fixture lifetime boundary; it does not constitute evidence for ordinary Close. Preserve dirty/busy/recovery admission on Close independently of Reset.
- Prefer pinned WinFormsKit controls. A native/custom exception records the needed capability, alternatives checked, ownership and focused verification. No unexplained blanket replacements, invented toolkit APIs, theme manager or new generic UI framework.
- Keep app-specific permission/session/validation/recovery policy in MyDmsVn. A proven generic toolkit gap requires narrow upstream work, independent evidence and an explicit consumer pin; it is not silently added to this plan.
- Follow [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md). Use focused affected verification. Native GUI runs stay serialized on interactive Windows/net48. Do not revive retired test runners, manifests or approval-file machinery.
- This plan selects no SQL verification, actual Login, package or release task. A proven changed server/SQL contract requires an explicit scope update and the existing policy; it does not inherit the previous Security SQL round.
- Archived Security/Sidebar/UI-NEXT/SAL plans and evidence remain immutable. Their old passes, waivers and open acceptance are neither new passes nor an inherited backlog. No dependency on a separate `MyDmsVn.Docs` repository is introduced.

## 3. Task ledger and dependencies

The implementation/evidence cells are initially `NOT STARTED`; the native decision cells are `NOT APPROVED`. Update this table and the linked candidate evidence as work happens. Do not create another status ledger in a skill, runbook or design document.

| Task | Deliverable | Depends on | Implementation / evidence | Owner decision |
|---|---|---|---|---|
| UIL-T00 | Current control inventory, exceptions and repo UI workflow | Roadmap adoption | NOT STARTED — first eligible task | Not a native gate |
| UIL-T01 | Shared product/Lab Business Compact theme and presentation bootstrap | T00 | NOT STARTED | Covered by later candidate review |
| UIL-T02 | Independent Sales/Security launchers, lifecycle and direct test setup | T01 | NOT STARTED | Covered by later candidate review |
| UIL-T03 | Role permissions + account-dialog pilot, native evidence | T02 | NOT STARTED | Await T04 |
| UIL-T04 | Owner review of the pilot baseline | T03 | NOT STARTED | **NOT APPROVED** |
| UIL-T05 | Apply approved baseline to remaining Security views/dialogs | T04 approval | NOT STARTED | Await T06 |
| UIL-T06 | Final Security native review and explicit candidate approval | T05 | NOT STARTED | **NOT APPROVED** |
| UIL-T07 | SQL-free product-host parity and affected Sales/common regression | T06 approval | NOT STARTED | No release approval implied |
| UIL-T08 | Reconcile docs, commands, evidence and handoff | T07; resolve affected review findings | NOT STARTED | Close only the accepted bounded scope |

Use a focused implementation commit for each task or coherent slice; retain evidence/correction commits where useful. No task is complete merely because its code compiles or its document exists. A material visual change after T04/T06 reopens the affected review; do not discard original findings.

## 4. Implementation tasks

### UIL-T00 — Control inventory and repository UI workflow

**Files:** create the proposed `.agents/skills/mydmsvn-winforms-ui/SKILL.md`; update `AGENTS.md` and `AI_CONTEXT.md` when it exists. Inspect `src/MyDmsVn.Desktop.WinForms/Features/Security/`, `BusinessCompactAppearance.cs`, current Lab sources and the pinned vendor documentation/source. Create the candidate evidence record at the proposed `docs/evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md` with pending fields rather than invented results.

1. Revalidate main/source/toolkit pins and the current component inventory. Map each affected search/filter, command, list/grid, tab, status and dialog to an existing toolkit capability or a justified exception. Include the role grant list and account dialogs; existing toolkit usage is part of the inventory.
2. Record actual typography/metrics/theme APIs at the pinned revision, app overrides, native behavior to preserve and any gap needing a separate decision. Do not treat gallery/demo-only font profiles or a proposed factory as existing public APIs.
3. Create one repository skill that routes UI work through policy → source/component map → independent Lab → focused tests/native evidence → applicable Owner gate → product-host verification. It links this plan for task state and the durable policy for rules; it must not embed copied acceptance status or require an external docs repository. Follow the skill-authoring workflow applicable in the implementation environment.
4. Exercise the workflow against a concrete proposed SEC edit and verify that the correct controls, Lab, scope and evidence are selected. Review the actual instructions and links; no runtime or SQL test is required for a skill/document-only change.

**Done when:** the map covers the pilot and remaining SEC surfaces, exceptions are explicit, the skill is tracked and usable in the repository, and the first runtime task has a concrete source/test scope. A planned skill path in this roadmap does not mean the skill already exists.

### UIL-T01 — One Business Compact presentation contract

**Files:** `src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs`, `FoundationShellForm.cs`, `Properties/AssemblyInfo.cs` only if needed; `samples/MyDmsVn.Desktop.UiLab/Program.cs` and `UiLabForm.cs`; affected focused tests in the existing Desktop/Ui.Tests owners. A small new bootstrap facade is permitted only where required; its type name is not prescribed as an existing API.

1. Factor/reuse the existing product theme creation and initialization order. Both startup and toggle use `BusinessCompactAppearance.CreateTheme` or a narrowly extracted equivalent shared by product and Labs. Preserve ReducedMotion and native composite ownership; do not recolor private toolkit children with an app recursion.
2. Establish typography and density from the actual product/policy; apply theme to module dialogs as well as the workspace. Remove the Lab's fallback to a visually different default theme. Avoid changing the global product minimum size or shrinking text to fit.
3. Define measured normal/minimum workspace-client profiles from the actual product shell. Make Lab diagnostics distinguish outer size, content client bounds, physical display scale and reported `DeviceDpi`. Place developer chrome outside the target viewport or provide an explicit review mode that preserves those bounds.
4. Verify theme selection/propagation and switching with the real controls, including an open dialog where supported; inspect Dark/Light, readable selected/disabled/focus states, and finite subscription disposal. Use focused behavior tests for a real changed boundary, not tests that merely duplicate constants.

**Done when:** product and Lab use one presentation contract and measurements show what the candidate is being reviewed within. Record any visual differences before the pilot; no visual acceptance is claimed yet.

### UIL-T02 — Independent module launchers and lifecycle

**Files:** convert `samples/MyDmsVn.Desktop.UiLab/MyDmsVn.Desktop.UiLab.csproj` to a reusable library; refactor its `Program.cs`/`UiLabForm.cs` into neutral host infrastructure and module-owned forms/tools; create proposed `samples/MyDmsVn.Desktop.UiLab.Sales/` and `samples/MyDmsVn.Desktop.UiLab.Security/` WinExe projects with thin STA entry points. Update `MyDmsVn.UiLab.sln`, affected project/lock files, the scenario source layout, Ui.Tests references/setup, and [UI_LAB.md](../development/UI_LAB.md).

1. Build the project boundaries from the [independent Lab specification](../design/INDEPENDENT_UI_LABS.md). Retain the shared library's assembly identity where compatible. Add only the precise facade/friend access required; do not make production internals public for convenience.
2. Move genuinely common journal/gate/session/task tracking separately from module-specific scenario enums/calculators. Keep one scenario assembly. Canonical Sales permission keys inside SEC test data remain valid data, not a reason to create a Sales fixture.
3. Make each launcher create its module form directly, with only its scenario picker, tools and runtime graph. Remove the module picker from the normal review path; no hidden SAL construction, reset or persisted module selection may precede SEC. Enforce the symmetric rule for SAL.
4. Share lifecycle mechanics without moving business admission into the common host. Preserve reset cancellation/drain/timeout poison, late-result rejection, close admission, session replacement, modal ownership and disposal. Tests should observe actual constructed graphs/fixture calls and disposal, not merely assert a window title or selected enum.
5. Change Security host/review/reset test setup to launch Security directly. Retain existing direct-control tests and existing Desktop/Session owners. Group tests clearly by Sales/Security/Common using deliberate source organization or traits; confirm actual discovery. Keep assembly serialization and native guard/helper reuse.
6. Locked restore/build the affected projects; inspect evaluated Lab and production dependency graphs. Run the focused startup/lifetime cases for both modules and common host. Update current executable/project commands only after the projects exist and the commands have been verified.

**Done when:** SEC and SAL each open directly and remain independent through reset/close/session replacement, relevant lifecycle tests pass, production excludes samples, and the documented commands launch the expected module. Shared assembly compilation may still include both modules.

### UIL-T03 — Native SEC pilot

**Files:** affected sections of `SecurityWorkspaceEditorBindings.cs`, `SecurityRoleDetailView.cs`, `SecurityPresentation.cs`, their existing collaborators and corresponding Ui.Tests; use the existing Security reference SVGs and specification. Update the candidate component map and evidence.

1. Implement **Quyền của vai trò** as the first composition pilot: clear grant state, friendly name plus canonical-key access, grouping/filtering, visible draft count including hidden changes, and explicit save/cancel actions. Preserve serial single-key mutation/recovery semantics. Select a pinned toolkit grid/list composition based on capabilities; do not merely repaint the current concatenated checklist and declare the reference satisfied.
2. Implement one representative account create/edit dialog with the shared theme/typography, toolkit-backed inputs/buttons where suitable, validation, keyboard order, focus and contextual command hierarchy. A modal is its own presentation surface and must not rely on a parent workspace's theme traversal.
3. Use existing meaningful draft/filter/serial-save/dialog tests and add coverage only for changed behavior. Preserve raw typing/caret, checkbox Space behavior and Enter-in-search safety with native input where required.
4. Capture and review both pilot surfaces in Dark/Light at physical 100% and 125%, using normal and constrained product-content bounds. Include long Vietnamese labels/canonical keys, selected/disabled/validation states and the changed dialog. Record actual geometry and native observations separately from screenshots.

**Done when:** the pilot is runnable from the direct SEC launcher, its component choices and native evidence are reviewable, and findings are recorded. Proceed to T04; do not expand to all SEC surfaces yet.

### UIL-T04 — Owner pilot baseline gate

**Files:** this task ledger and the candidate evidence record; corrections in the narrowly affected pilot files if the review requires them.

Present the real running pilot, exact source/test/vendor revisions, screenshots, geometry and unresolved findings. The Owner explicitly approves or rejects the Role permissions composition and account-dialog baseline, including the visual hierarchy, density and toolkit exceptions. Record date/time, decision, scope and any exception. Documentation approval, mockup approval, successful automated tests and historical SEC-T05 approval do not satisfy this gate.

**Done when:** the pilot candidate has dated explicit approval. If rejected or required evidence is missing, T05 stays blocked while the pilot is corrected. Approval applies only to the reviewed baseline; material later changes require focused renewed review.

### UIL-T05 — Remaining Security conformance

**Files:** `src/MyDmsVn.Desktop.WinForms/Features/Security/` affected presentation files; existing relevant UI tests and candidate evidence. Change Desktop.Application only for a demonstrated presentation-binding need while preserving its transport-neutral contracts.

1. Apply the approved policy and controls to Users, user information/memberships/effective permissions/direct decisions, Roles/member list, read-only catalog, and the remaining create/edit/activation/password/import dialogs and feedback surfaces. Record an explicit coverage inventory so no secondary modal is missed.
2. Preserve Owner header corrections and ownership: memberships in user **Vai trò (n)**, role **Thành viên (n)** read-only, no duplicated role-member header count, no user-header role chips, no catalog mutation. Effective permissions remain an explanation of loaded data.
3. Use command hierarchy rather than one uniform button style. Keep allowed/denied/unavailable/read-only/dirty/busy states legible with text/focus semantics. Validate intentional two-line permission rows and richer master rows against compact composition; do not force every row into a single-line height.
4. Run affected tests for edit buffers, filtering/selection, draft retention, session/revision identity, partial/conflict/uncertain recovery and close admission. Preserve existing source-owned behavior tests instead of reimplementing their owners. Review all changed modal/theme/focus paths in the SEC Lab.

**Done when:** the remaining SEC surface inventory follows the approved baseline, exceptions and regression evidence are recorded, and the candidate is ready for T06. No new authorization or SQL behavior is claimed.

### UIL-T06 — Final native candidate review

**Files:** this ledger, candidate evidence, [Security review guide](../development/SECURITY_UI_LAB_REVIEW.md) only for actual reusable command/exercise changes.

1. Review the full changed SEC surface inventory in both themes and physical 100%/125%, at normal/minimum product-content bounds. Cover truncation/overflow, long labels, selected/disabled/focus states and the edited modal surfaces. Reuse the pilot matrix where the exact candidate and unchanged behavior justify it; record affected reruns after changes.
2. Run the affected repeatable scenarios from the review guide: read/denied/error, user ownership, role/direct draft, filtering hidden changes, partial/conflict/uncertain save, reload/diff, close/session/reset and contextual import where presentation/bindings changed. Verify nonempty discovery and actual executed native cases. This is current-candidate coverage, not a claim that the entire historical attended matrix was completed.
3. Present the candidate and findings to the Owner. Record a dated explicit approve/reject decision for the final SEC UI, its exact revisions and bounded exceptions. Missing observations remain `NOT RUN`/`OPEN`; a historical waiver is not silently reused.

**Done when:** required selected observations have evidence and the Owner approves the final candidate within a stated scope. T07 stays blocked otherwise. Changes that materially alter the approved UI reopen the affected review.

### UIL-T07 — Product-host parity and focused shared regression

**Files:** existing production shell/composition and SQL-free hosted test helpers only if a demonstrated parity issue needs correction; affected Desktop/Session/Ui.Tests and evidence. There is no legacy factory migration to schedule.

1. Host the same approved production control in the real product shell using existing SQL-free injected test/session ports or the smallest compatible test composition. Compare startup/toggle theme, typography, measured client bounds, navigation/close integration and modal ownership with the direct SEC Lab. Do not add fake switches or sample references to shipping Desktop.App.
2. Select Sales/Common regressions for the shared host/theme/lifecycle paths actually changed. Include existing SAL reset/toolbar behavior when affected; a `~Sales` filter alone misses them. Preserve one native GUI session at a time and the current test owners.
3. Resolve parity defects; rerun focused evidence and reopen T06 for material changed UI. Record evaluated production references and the absence of sample/fake composition.

**Done when:** the approved candidate retains the same presentation in the product host and affected shared/SAL behavior is verified. SQL-free injected composition does not prove actual Login, Server authorization, SQL transactions, a packaged executable or release acceptance.

### UIL-T08 — Documentation and bounded handoff

**Files:** this plan, `AGENTS.md`, `AI_CONTEXT.md`, `README.md`, `docs/README.md`, `docs/development/UI_LAB.md`, `docs/development/SECURITY_UI_LAB_REVIEW.md`, the durable policies/skill and candidate evidence; the execution index remains the sole plan selector.

Replace implementation-target wording with verified current facts only where completed. Reconcile launcher paths, startup instructions, module filters, control exceptions, theme/geometry profiles and the repository skill. Record actual source/test/vendor revisions, selected/discovered/executed results, native/Owner decisions, fixes and remaining limitations. Check links, status and encoding. Leave historical plans/evidence unchanged.

**Done when:** another contributor can open either Lab directly, find the policy/skill, repeat the recorded focused checks and distinguish implementation, native approval and remaining release limits. Close only this plan's accepted scope; do not mark archived open work complete.

## 5. Verification and evidence contract

For this **documentation PR**, verify diff, local links/anchors, current-vs-proposed statements, plan routing, task dependencies, baseline/pins and UTF-8/line-ending rules. Do not run SQL or GUI for this change. No native result or candidate Owner decision is being recorded by this activation.

For implementation, each task records the smallest affected project/filter and runs it in the supported environment. Use actual `--list-tests` discovery before relying on selectors. At the inspected baseline, useful class groups are:

| Group | Source-verified selection at this baseline |
|---|---|
| Security | `FullyQualifiedName~Security` in Ui.Tests; inspect exact discovered classes before each candidate run |
| Sales | `FullyQualifiedName~SalesScenarioBehaviorTests` OR `FullyQualifiedName~UiLabResetTests` OR `FullyQualifiedName~UiLabToolbarReviewTests` |
| Common | `FullyQualifiedName~NativeDriverIndependenceTests`, plus new shared-host cases only once implemented/discovered |

These are selection starting points, not required full-suite repeats or proof that test bodies start the correct module. T02 must remove the old Sales-first SEC setup. An implemented module trait scheme may supersede the class filters only after mapping and verifying actual case discovery. Keep GUI commands sequential, finite waits, `WinFormsTestGuard`, native exception/DataError capture and verified disposal. No empty selection, missing mandatory observation or unexamined skip counts as PASS.

Create the candidate evidence record during T00 and use the [review guide template](../development/SECURITY_UI_LAB_REVIEW.md#gate-evidence-template). Record source/test/gitlink hashes, environment, physical scale and reported DPI, outer/client/content geometry, module entry point, fixture/actions, component mapping/exceptions, screenshots/native observations, sanitized command/TRX summaries, findings/corrections and explicit Owner decisions. The evidence describes observations; this plan owns task status. Passwords, raw import JSON and private SQL data do not belong in committed evidence.

## 6. Review focus and completion boundary

Reviewers should challenge five specific failure modes: a visually different Lab theme, hidden opposite-module construction, duplicated host lifecycle with weaker teardown, unreasoned native/custom controls or uniform action styling, and a screenshot/test pass being promoted to Owner approval. Also check that the new skill routes to durable current policy and cannot revive old plans or external approval dependencies.

Completion requires both direct Labs, the shared presentation contract, the repository UI workflow, SEC conformity to the approved pilot/final candidate, meaningful affected verification and a coherent handoff. Full compile isolation, new business features, toolkit upgrades, SQL integration, actual Login, package acceptance and production release remain outside this selected roadmap.
