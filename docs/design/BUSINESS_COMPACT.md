# Business Compact — design and control policy

**Status:** current design policy selected by the Owner on 2026-10-27; runtime conformance must be established for each candidate. The bounded [Business Compact and independent UI Labs](../plans/20261027-001-business-compact-and-independent-ui-labs.md) implementation and handoff are complete, with exact candidate evidence linked from that roadmap. This policy remains current when an execution roadmap is completed or archived.

Read with [Security UI and behavior](SECURITY_ADMINISTRATION_UI.md), [Independent UI Labs](INDEPENDENT_UI_LABS.md), [UI Lab runbook](../development/UI_LAB.md) and [Security review guide](../development/SECURITY_UI_LAB_REVIEW.md). This policy governs presentation and control selection; the Security specification retains business rules, authorization, state and recovery. Historical tasks and approvals retain their original boundaries.

## 1. Basis and observed gaps

Inspected source: [`MyDmsVn@8de4451573e8a9a033d7e5a0b07e5f22ef3018b9`](https://github.com/chung6a8m/MyDmsVn/commit/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9). These observations describe the baseline before roadmap implementation, not runtime changes made by this documentation PR.

| Baseline component | Observation and resulting requirement |
|---|---|
| [BusinessCompactAppearance](../../src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs) | `CreateTheme` supplies the product palette; `Apply` styles against the current theme. Calling `Apply` alone does not install the product theme. |
| [FoundationShellForm](../../src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs) | Startup and theme switching use `BusinessCompactAppearance.CreateTheme`. Both Labs must share this configuration path. |
| [UiLabForm](https://github.com/chung6a8m/MyDmsVn/blob/8de4451573e8a9a033d7e5a0b07e5f22ef3018b9/samples/MyDmsVn.Desktop.UiLab/UiLabForm.cs) | One mixed host defaults to Sales; its theme toggle uses `BootstrapTheme.CreateDefault`. Its current outer size of `1250 × 850` does not establish product content-area parity. |
| [SecurityWorkspaceControl](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs) and [editor bindings](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceEditorBindings.cs) | Some toolkit controls already exist, including tabs/buttons/radio buttons. Search/filter fields, the grants checklist and some dialogs still use native controls or custom presentation; inventory and assess each relevant location. |
| [SecurityUserDetailView](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityUserDetailView.cs) | Effective-permission rows contain a friendly name and key on two lines, with height 45. Assess content and scaling before selecting density; a row above 32 is not automatically wrong. |

The goal is consistent native presentation: clear hierarchy, useful administration density, preserved native editing and reuse of the pinned toolkit. Recoloring native controls or using a few `BootstrapButton` instances does not establish whole-view conformance.

The [original Business Compact images](../design-references/business-compact/README.md) retain their registered source and historical BCF interpretation. Use them with this policy for visual context; they do not establish acceptance of a later native candidate.

## 2. Theme, typography and ownership

### BC-01 — One product configuration

The shell, Sales Lab and Security Lab must install the same Business Compact configuration before creating workspace/dialog controls. Light/Dark switching must use that same path. Reuse `BusinessCompactAppearance.CreateTheme` and the existing `BootstrapThemeManager`; any new bootstrap/helper name is an implementation decision, not an existing API.

Do not copy palettes into the Labs, create another theme manager or substitute toolkit defaults for product configuration. `CreateTheme` currently retains base metrics/typography and ReducedMotion; account for all of them during extraction. An app-owned palette configured through the public theme API is an appropriate boundary.

Dark is Security's primary visual reference; Light uses the same structure, hierarchy and interactions. Selected/hover/focus/disabled/invalid/read-only states must remain legible in both. Exercise theme switching while a dialog is open, as well as workspace startup. A modal disables its owner toolbar: use a supported theme action or a focused test action through the theme API on the UI thread; this requirement does not add a theme button to every dialog.

### BC-02 — Shared typography roles

The baseline app uses Segoe UI with 10 pt body, 14 pt heading, 9 pt secondary and separate brand/strong roles; the toolkit has its own theme typography. These source values require consistent application, not independent hard-coded copies in each view. Configure composites through appropriate public APIs and compare them in both Lab and shell.

Preserve hierarchy between object names, metadata and section labels. Do not shrink fonts to fit constrained geometry. Vietnamese labels, long canonical keys and recovery copy must remain readable at physical DPI 100% and 125%; changing `Font` or scaling a screenshot is not physical-DPI evidence.

### BC-03 — Respect composites and lifetime

The toolkit owns composite internals, native editors, rendering and their subscriptions. The app configures public APIs and explicitly exposed app content: for example tab pages, `BootstrapPanel.Controls` and `BootstrapBusyHost.Content`. Preserve this boundary in `BusinessCompactAppearance`; do not traverse private editors to force colors/fonts/heights.

The app owns workspace/session/document identity, dirty drafts, validation mapping, authorization admission and recovery. Theme switching must not replace the graph, recreate an editor and lose its caret, modify a draft or retarget a mutation. Developer Reset cancels, drains with finite bounds and disposes before creating the replacement graph; a failed drain must not be hidden by presenting a fresh workspace. Ordinary Close first follows dirty/busy/recovery admission, then tears down only when admitted. Reset is a fixture boundary and does not prove Close behavior.

## 3. Geometry and density

### BC-04 — Match the actual workspace area

The UI Lab must reproduce product theme, typography, content-area size and theme-switching behavior. The baseline shell has normal outer size `1200 × 760` and minimum `900 × 600`; neither is a workspace client size. Measure the area remaining after chrome/navigation/header at both sizes and relevant DPI, then configure the Lab preview to match it.

Lab toolbars, scenario tools and journals are review tools. Keep them outside the measured preview or account for them so the workspace receives the correct area. The outer Lab host may accommodate those tools, but do not enlarge the target viewport or hide difficult product content merely to make a screenshot fit. Record outer bounds, preview client bounds, DeviceDpi and theme.

### BC-05 — Targets and justified exceptions

| Element | Target and review rule |
|---|---|
| Master/detail | Master approximately 240–280 logical px at normal size, reducible toward 180 when constrained. Keep detail identity visible; avoid a third permanent information column. |
| Single-line inputs/buttons | Target approximately 32–36 logical px, checked against pinned control metrics and actual content. Do not clip native edit/click/focus bounds by forcing height. |
| Single-line tables | Target approximately 28–32 logical px when font/padding/focus cues fit. Do not apply this unchanged to multiline rows. |
| Spacing | Use an 8/12 logical-pixel rhythm for related groups/areas and existing shared metrics where suitable. Avoid independent magic-number sets per form. |
| Multiline/secondary content | Greater height is allowed for a clear purpose with correct scaling. Record row type, content, measured height and reason; assess whether secondary content belongs in details instead. |
| Long keys/names | Ellipsis is allowed with full accessible text, tooltip and detail/copy access where needed. Do not truncate the only information that identifies a mutation target. |

These are design targets inherited from the Security specification and subject to native review. They do not describe an existing toolkit preset, require identical dimensions for every component or replace DPI measurements.

Reflow toolbars/labels and use local vertical scrolling where appropriate. Preserve the existing shell minimum and avoid workspace-wide horizontal scrolling. Necessary scrolling inside a specific control needs a reason and must not hide essential commands.

## 4. WinFormsKit-first control selection

### BC-06 — Exact pin and real contracts

The baseline is `vendor/MyDmsVn.WinFormsKit@c232a4c09dc0386a5bd8bb484a5c991acae827d5`, with SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. [Component contracts at the pin](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/c232a4c09dc0386a5bd8bb484a5c991acae827d5/docs/components/controls/COMPONENTS.md) define available capabilities/APIs. Compatible `MyDmsVn.Bootstrap5WinFormUI` namespaces and older assembly identities remain; the repository name does not require namespace migration.

Before creating a component or selecting a native control, inspect the controls and compositions available at the pin. Do not infer an API from a gallery label, demo image or latest upstream. [ControlGallery at the pin](https://github.com/chung6a8m/MyDmsVn.WinFormsKit/blob/c232a4c09dc0386a5bd8bb484a5c991acae827d5/samples/ControlGallery/MainForm.cs) is a reference; demo font profiles do not automatically become a public Business Compact preset.

### BC-07 — Default Security mapping

| Need | Preferred choice at the pin | Preserve or verify |
|---|---|---|
| Create/save/cancel/open/copy commands | `BootstrapButton`; existing toolbar composition when appropriate | Meaningful variant/outline/size, accessible name, focus, enabled state and validation admission. |
| Search, names, email, passwords | `BootstrapTextBox` | Native editing, caret/range, IME, read-only/validation and public password masking. Do not assume it derives from native `TextBox`. |
| Simple filters/selectors needing native binding | `BootstrapComboBox` | One selection model and native `Items`/binding/events. Native popup/arrow OS chrome is part of the contract; record it during review. |
| Selectors needing search/grouping/managed presentation | `BootstrapSelect` when the capability is needed | A separate control, not a drop-in ComboBox. Verify identity, keyboard, popup/lifetime; do not invent a workflow merely to use it. |
| Draft toggles and boolean filters | `BootstrapCheckBox` | Space changes the correct owner's draft; checking does not automatically dispatch a write. |
| Three direct-decision choices | `BootstrapRadioButton` | **Theo vai trò / Cho phép / Từ chối**, preserving nullable-decision semantics. |
| Tabs | `BootstrapTabControl` | The toolkit themes headers; the app configures page content using shared surface/typography rules. |
| Effective-permission/catalog/grant tables | `BootstrapDataGridView` with suitable columns/binding | Inherits `DataGridView`; the host owns columns/edit/read-only semantics. A grant checkbox column edits the draft only. Do not blanket-replace Sales SourceGrid. |
| Surfaces, sections and labels | `BootstrapPanel`, `BootstrapLabel`; `BootstrapCard` where composition warrants it | Semantic surfaces/typography and public content ownership; do not add card chrome around every group. |
| Status and feedback | Existing badges/alerts, `BootstrapToastContainer`, validation state, spinner/`BootstrapBusyHost` | Errors/recovery stay in the correct workspace and remain visible as needed; busy visuals do not determine authorization or transaction outcome. |
| Pure layout | `TableLayoutPanel`, `FlowLayoutPanel`, `Panel`, `SplitContainer` where appropriate | Native layout primitives are valid; surfaces/padding/fonts still follow the policy. Toolkit-control counts are not the goal. |

This is the default mapping, not a claim that every call site has migrated. Each task inspects current source/API, especially helpers accepting concrete `TextBox` or `ComboBox` types; use narrow binding/adapter changes instead of invalid casts or lost native semantics.

### BC-08 — Reviewable native/custom exceptions

Native layout, OS-owned window chrome and controls inside toolkit composites are not violations. Other native/custom interactive presentation requires a concrete capability reason, beyond convenience, existing code or “this is WinForms.” An exception records:

1. View/call site, purpose and chosen control.
2. Pinned alternatives inspected and the specific capability/compatibility gap.
3. Public API/composition assessed or tried and why it is insufficient.
4. Required theme/typography/DPI/keyboard/accessibility/lifetime behavior.
5. Bounded scope, native evidence and disposition: retained exception or upstream primitive.

Keep inventory/exception records in the candidate's task/evidence, referencing this policy; do not introduce another approval subsystem. Owner-drawn master lists require review of row structure, hit testing, focus and accessibility. Reading the palette does not establish correct density or layout.

A generic rendering/native/theme/lifecycle gap belongs in a narrow compatible upstream change with evidence and an explicit pin. Business composition/permission/session/draft behavior stays in MyDmsVn. This roadmap does not implicitly authorize a dependency upgrade or a new general UI framework.

## 5. Composition and hierarchy

### BC-09 — Task-based actions

Within one editing scope, the primary action must have appropriate emphasis; secondary/cancel actions do not have identical visual weight to Save. Destructive/contextual actions identify intent and target. Applying `Primary + Outline + Small` to every command does not establish hierarchy.

Separate search/filter/reload groups from data-changing commands through placement and clear labels. Provide one clear create action per object context. Retain explicit names from the specification: **Lưu thông tin**, **Lưu quyền vai trò**, **Lưu quyền này**, **Gán vai trò**, **Gỡ vai trò**, **Hủy thay đổi**. Enter in a search field must not dispatch a mutation.

### BC-10 — Role permissions pilot

The [role permissions reference](../design-references/security-administration/02-role-permissions.svg) defines pilot composition: identity/header, filters, a table with **Cấp | Quyền | Nhóm**, pending-change indicators and a summary/save/cancel area. Present canonical keys alongside friendly names using reviewed hierarchy; do not collapse the table into concatenated name/key/group checklist strings merely because the data remains present.

Checkboxes edit the selected role's draft; pending counts include changes hidden by filters. The footer reflects the real state, with no Save in a read-only context. Preserve ordered single-key saves, revision chaining, partial/uncertain recovery and navigation admission from the specification. An unchecked role grant does not mean Deny.

The pilot includes this view and one real account dialog to exercise input/label/button/validation hierarchy and theme on a top-level window. Obtain Owner approval of the native pilot before expanding across Security. A pleasing screenshot does not establish denied/partial-save states or unexercised dialogs.

### BC-11 — Presentation semantics

Effective permissions and the catalog remain read-only. Role Members only navigates to user membership when authorized/admitted; user headers contain no role chips or membership-management link, and role headers do not repeat member counts beside the active badge. Preserve the Owner's corrections in the [Security specification](SECURITY_ADMINISTRATION_UI.md) and [visual-reference interpretation](../design-references/security-administration/README.md).

Allow/Deny/Unavailable use text or icons with accessible meaning; color supplements them. Counts require known datasets; failed/unavailable data does not become zero or empty-success. Do not apply unreadable disabled styling to every read-only value. Focus, selected rows and direct Deny must remain distinguishable together in both themes.

## 6. Required UI workflow

### BC-12 — Inventory through native evidence

1. Read the selected task, this policy, module specification and actual source/pin. Identify presentation scope without reopening historical queues.
2. Map control groups and native/custom exceptions. Identify theme/bootstrap, typography, preview geometry and owner/lifetime effects.
3. Build the candidate using production components in the correct module Lab with fake ports. Keep it SQL-free and do not instantiate the other module's graph.
4. Exercise affected native views/states in the selected matrix; fix discrepancies before treating screenshots as a baseline.
5. Record source/test/toolkit revisions, environment/geometry, images/observations, focused test discovery/results, exception rationale and remaining limitations.
6. Record the dated Owner visual decision at the roadmap's named gate. Document approval, source merge or SVG references do not approve a native candidate.

Verification follows [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md). Docs-only changes need diff/link/status/encoding review. Runtime tasks use focused checks for actual changed boundaries; presentation work does not automatically select SQL or full historical suites. Native GUI runs require interactive Windows, a message pump, STA serialization, guards and existing finite cleanup.

## 7. One focused repository UI skill

The repository skill [mydmsvn-winforms-ui](../../.agents/skills/mydmsvn-winforms-ui/SKILL.md) routes UI work through this policy and the selected roadmap. The [candidate inventory](../evidence/ui-labs/20261027-business-compact-and-independent-ui-labs.md) records source observations and pending exceptions; this policy remains the durable authority.

The skill must contain:

- Triggers for MyDmsVn WinForms UI work, including views/dialogs, theme/density, control migration and UI Lab composition.
- A short reading route: active task → Business Compact → module specification → pin/API → relevant runbook/test policy.
- Before-code checks for module ownership, inventory/mapping, justified exceptions, actual client geometry and keyboard/lifetime effects.
- Candidate → focused checks → native evidence → named Owner gate workflow; only selected tasks define their gate and scope.
- A completion report with source/test/pin, evidence and real limitations; a successful build alone does not establish conformance.

The skill contains no SQL credentials, selects no roadmap automatically, and adds no external documentation-repository synchronization/approval dependency. It must not restore removed test runners or acceptance gates. UIL-T00 verifies instruction routing and a representative UI task; a personal skill outside the repository is unnecessary.

## 8. Acceptance evidence

| Criterion | Evidence for the selected candidate |
|---|---|
| Theme/typography parity | Shared bootstrap/theme toggle in shell/SAL/SEC; native views/dialogs in Light and Dark; consistent font roles. |
| Geometry parity | Normal/constrained workspace client measurements at real DPI 100%/125%; tools/journals do not distort preview area. |
| Control conformance | Inventory/mapping plus capability reasons, evidence and disposition for exceptions. |
| Composition | Pilot role table/footer and account dialog; expand remaining views/dialogs after approval. |
| Interaction | Native text/caret/focus, Space/Enter/Tab, read-only/disabled states, dirty navigation and relevant failure/recovery. |
| Ownership | Separate module graphs/tools; finite reset/close/session drain; late completions cannot repopulate presentation. |
| Explicit decision | Dated Owner approval for the exact candidate/gate, or `NOT APPROVED` with findings; no inherited approval from an earlier candidate. |

Detailed matrices, task states and gates live in the [active roadmap](../plans/20261027-001-business-compact-and-independent-ui-labs.md) and its evidence. When that roadmap is archived, this policy remains current for subsequent UI changes. Archival neither turns unresolved limits into PASS nor selects additional implementation work.
