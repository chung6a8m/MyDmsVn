# Security Administration — UI-Lab-First Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Follow repository instructions and the focused gates below. This file alone owns Security task and approval state.

**Goal:** Deliver the Owner-reviewed **Quản trị truy cập** workspace with distinct user, role and permission-catalog responsibilities, and approve its real UI/behavior in UI Labs before Local or SQL integration.

**Architecture:** Add application-owned `SecurityWorkspaceViewModel` and `SecurityWorkspaceControl` candidates using the existing administration/catalog DTOs and ports. First compose them only in the existing SQL-free UI Lab with deterministic fakes. Keep the production factory on the legacy control until the dated Owner gate. Then reuse the reviewed components through Local and focused SQL verification. Role grant Save uses ordered single-key commands with explicit partial-success recovery; it is not an atomic replacement transaction.

**Tech stack:** C# / WinForms / .NET Framework 4.8 (`net48`), existing WinFormsKit controls, xUnit and the serialized STA/native-input harness. No new runtime dependency, database schema or permission-registry mutation.

**Spec:** [UI and behavior specification](../design/SECURITY_ADMINISTRATION_UI.md), [visual references based on the reviewed layouts](../design-references/security-administration/README.md) and [Security UI Labs review guide](../development/SECURITY_UI_LAB_REVIEW.md). The specification and explicit Owner corrections take precedence over generated image text and illustrative counts.

**Status (2026-10-25):** SEC-T00–SEC-T03 have a SQL-free candidate in an isolated Security worktree. SEC-T04 now has physical native100/native125 evidence, keyboard Tab/Enter/Escape/Space corrections, independent-pane access/empty/reload coverage and bounded Reset poison-timeout evidence. The Owner waived High Contrast testing for SEC-T04 on 2026-10-25; the task remains open for the complete SEC-L01–SEC-L12 review rehearsal and remaining keyboard actions. SEC-T05 is NOT APPROVED; SEC-T06–SEC-T08 remain BLOCKED. The [Sidebar plan](20261025-001-sidebar-navigation-horizontal-scrollbar.md) retains its own scope and state. Exact candidate execution evidence appears in section 1A below; the original planning text and proposed names remain historical until verified there.

**Inspected baseline:** `chung6a8m/MyDmsVn@0216701835cc3326124a94760186989c1ac4f729` on `v1a-pro-ui-next`. WinFormsKit gitlink: `c232a4c09dc0386a5bd8bb484a5c991acae827d5`; nested SourceGrid: `f4e457b43582bf01892f50bdc74aa480531e5944`. Re-read current source, active plans and pins before implementation and record relevant drift.

## Global constraints

- Preserve `net48`, package boundaries, current transport DTOs, authorization/permission precedence, revision checks, auditing, import semantics and toolkit pins. AppCore/net8 remains outside the shipped path.
- SEC-T00–SEC-T05 are SQL-free. Do not change Local wiring, implement SQL integration tasks, change procedures/schema, run SQL verification or cut over the production factory before SEC-T05 is explicitly approved. Read-only source/contract inspection for planning is permitted.
- Use the actual candidate production components in the Lab. A separate HTML demo, rendered image or sample-only copy cannot establish acceptance of those components.
- The Lab/scenario/Ui.Tests transitive graph must exclude Local, Server, DbMigrator and SQL fixtures. Production/release graphs must exclude sample/fake dependencies. Existing architecture checks that scan only `src` do not establish sample/test isolation.
- Keep current Sidebar scope separate. Security must fit the existing shell sizes, navigation and workspace close/feedback contracts; it does not select a shell redesign or a new global framework.
- Follow [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md), [test commands](../../tests/README.md) and [UI_LAB](../development/UI_LAB.md). Use focused evidence, bounded waits and actual nonempty discovery. Do not reactivate archived SAL/UI-NEXT/BCF work, full acceptance matrices, removed runners or approval manifests.
- Keep Markdown UTF-8 without BOM, CRLF in working trees and LF in Git blobs. Preserve existing C# encoding conventions, original failures and historical evidence. No credentials, raw passwords, connection strings or unredacted request bodies in evidence.

## Review focus

1. The user header has no role chips/manage-role shortcut; **Vai trò (n)** owns that information. The role header has no duplicate member count; **Thành viên (n)** owns the detail count.
2. A role grant, a user's Allow/Deny/Inherit decision and a read-only explanation of loaded assignments have different controls, save boundaries and copy. Administrator remains an ordinary role.
3. Missing/denied/stale catalog or role data never becomes a fabricated empty dataset, zero members or a false default-deny result. `IsProtectedAccount` is not proof of an unconditional superuser Allow.
4. Serial Save chains actual returned revisions, reports committed items independently of the stop reason and does not replay or roll back earlier successful writes. A later successful reload cannot prove who committed an uncertain earlier operation.
5. Dirty navigation, ordinary Close, forced session replacement and late callbacks preserve one captured edit owner. The post-gate Local admission boundary must prevent a replacement actor from receiving an old editor's request.
6. Light/Dark, physical native100/native125, native input, minimum/normal geometry and reset/drain behavior must be observed in the running candidate before the Owner gate.

## 1. Selected scope and dependencies

| Task | Deliverable | Depends on | Initial state |
|---|---|---|---|
| SEC-T00 | Application projection, dataset state and draft foundations | Baseline review | PLANNED / NOT STARTED |
| SEC-T01 | Deterministic Security fake ports and scenarios | SEC-T00 contracts | PLANNED / NOT STARTED |
| SEC-T02 | Real candidate views and Lab composition | SEC-T00, SEC-T01 | PLANNED / NOT STARTED |
| SEC-T03 | Editors, serial Save, membership and close/lifetime behavior | SEC-T02 | PLANNED / NOT STARTED |
| SEC-T04 | Import, access/error states and native-review readiness | SEC-T03 | PLANNED / NOT STARTED |
| SEC-T05 | Attended UI Labs review and explicit Owner decision | SEC-T00–SEC-T04 | NOT APPROVED / NOT STARTED |
| SEC-T06 | Session-bound Local admission and factory cutover | Approved SEC-T05 | BLOCKED BY SEC-T05 |
| SEC-T07 | Focused Local/SQL integration and regression evidence | Approved SEC-T05, SEC-T06 | BLOCKED BY SEC-T05 |
| SEC-T08 | Reconcile bounded acceptance and handoff | SEC-T06, SEC-T07 | BLOCKED BY SEC-T05 |

SEC-T00 foundation work and SEC-T01 fixture preparation may overlap after their interfaces are agreed. Shared host/control edits require a single owner or isolated branches. Sidebar implementation is independent, but both workstreams serialize GUI runs and recheck shared shell geometry if either changes it. No completion in one workstream establishes acceptance in the other.

### 1A. SEC-T00–SEC-T04 candidate execution record — 2026-10-25

**Authorization and boundary:** The Owner explicitly selected SEC-T00–SEC-T04 and an isolated worktree. The candidate started from product `021856bcce6672659f026da88e4c56d8b3a6a5b9` on `v1a-pro-ui-next` (the plan's inspected documentation baseline was `0216701835cc3326124a94760186989c1ac4f729`). Candidate source/test commit: `7ef118aba279f731c1c19ed893c1334aecc1c73e`. Recursive gitlinks remained WinFormsKit `c232a4c09dc0386a5bd8bb484a5c991acae827d5` and SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. The production Security factory still creates the legacy control. No Local, SQL, schema, transport or permission-registry change was made.

| Task | Candidate result | Bounded verification / remaining gap |
|---|---|---|
| SEC-T00 | Application-only independent dataset/freshness state, permission explanation, immutable role draft and orthogonal save outcome implemented. | T00 RED boundary 19 PASS / 1 expected FAIL; GREEN 20/20 PASS. No live authorization assertion. |
| SEC-T01 | Deterministic Security fakes, bound workspace session, revisioned commands/import, gates, redacted journal and fixed fixture aliases implemented. | Final source-verified scenario tests 22/22 PASS after correcting held-commit state, schema-v1 parser and import error parity. Fake tests do not prove SQL commits or Login. |
| SEC-T02 | Candidate views and Sales/Security Lab composition implemented; production factory remains legacy. | Light/Dark at 900×600 and 1200×760, catalog/effective/read-only/header/member controls and Sales toolbar preservation exercised. A source-first UI skeleton did not establish an independent TDD RED; the final project run below is the runtime evidence. |
| SEC-T03 | Explicit role/direct drafts, one-key ordered Save, membership revisions, profile actions, close admission, session invalidation and recovery/rebase implemented. | Review-found owner, scrub, uncertain and recovery bypass defects were corrected and retested. Single-command fake responses remain separate from real Local admission and SQL audit proof. |
| SEC-T04 | Contextual JSON preview/apply, target/revision/digest invalidation, native text and keyboard tests, Reset/drain and independent pane-state tests implemented; candidate remains SQL-free. The 125% follow-ups corrected disabled role-grant contrast, local identity/password validation, master-list Tab order, Escape cancellation, grant Space behavior and a null-role render failure. | **OPEN:** physical 125% project evidence and bounded Reset failure evidence are recorded below. High Contrast is **NOT RUN / OWNER-WAIVED for SEC-T04 only**. Full SEC-L01–SEC-L12 review rehearsal and native keyboard activation of the remaining actions are not yet proven. Do not mark the T04 review-ready exit complete. |

**Current automated evidence:** Locked restore of `MyDmsVn.UiLab.sln` passed; Release/net48 full `MyDmsVn.Desktop.Ui.Tests` discovery was nonempty and the final TRX at `TestResults/security-ui-lab-candidate/security-ui-lab-candidate.trx` records **128 discovered/executed, 128 PASS, 0 failed/skipped/not-executed** for source/test commit `7ef118aba279f731c1c19ed893c1334aecc1c73e`. This includes 26 retained Sales/driver rows, 6 retained Sales Reset/toolbar rows, and 96 Security rows (projection/draft, scenario/fake, control, lifetime/save/import, pane and native/Reset). Focused post-change selectors passed 7/7 Security host/native and 26/26 lifetime/import; the full project was rerun after review corrections. Tooling: Windows interactive net48, SDK `8.0.426`, VSTest `17.11.1` x64. Evaluated Lab/Ui.Tests graph contained nine projects and excluded Local/Server/DbMigrator/SQL; evaluated Desktop.App + DbMigrator graph contained 13 projects and no sample/test/fake reference. Actual primary monitor effective DPI was 96 (100%), bounds 1920×1080, work area 1920×1032. Native tests observed per-character `WM_CHAR`, raw text/caret/selection/focus in that context; they do not establish physical 125%. Raw TRX remains local and uncommitted.

**Physical native125 follow-up — 2026-10-25:** Source/test correction `5903cecd0c9ee4419f76c48182f4a2fce9db12b9` and bounded Reset fixture/test `eb6382d74540f4d565b2e1002c3cae11e31fe345` retain the same recursive gitlinks and SQL-free composition. Locked restore and Debug/net48 Lab build passed. `GetScaleFactorForMonitor` reported **125** for the actual monitor in each of four Light/Dark × 900×600/1200×760 native-search test rows. The guard deliberately failed one row with expected 100 (`Expected: 100; Actual: 125`) and passed 4/4 with expected 125. The testhost reported monitor effective DPI 96×96 and `GetDpiForWindow` 96: this DPI-unaware process is scaled on a physical 125% desktop, and the two measurements are recorded separately. Client sizes were 900×600 and 1200×760; the measured list/detail bounds were respectively 234×412 / 646×464 and 315×565 / 865×624 in WinForms coordinates. Existing native `WM_CHAR` assertions checked every typed character's raw text, caret/selection, focus and selected owner. A separate live Lab session used native `SendInput` for `i`, then `n` in user search: the displayed filter narrowed to the inactive user while retaining the previously selected detail owner; Light and Dark user/role screens were inspected. In the Light catalog, native Down moved focus/selection and details to `Sales.Receipts.Write`; Enter advanced to the next row and Space left that row selected. The journal remained the three initial read operations with no assignment mutation. The live Dark role screen exposed low-contrast disabled grant text. A new test failed before the correction, then passed after enabling the read-only list and rejecting `ItemCheck` until **Sửa quyền** starts a draft. The rebuilt Dark role screen showed readable grant text. These live observations are bounded visual/keyboard checks, not the full SEC-L attended matrix; screenshots and raw TRX stay local and uncommitted.

The final Debug/net48 project TRX at `TestResults/security-ui-lab-native125-final/security-ui-lab-native125-final.trx` records **130 discovered/executed, 130 PASS, 0 failed/skipped/not-executed** with `MYDMSVN_UIN_EXPECTED_SCALE=125`, VSTest `17.11.1` x64 and SDK `8.0.426`. An initial post-edit build failed only because the interactive Lab held its own output DLL open; the Lab was closed and the targeted test and whole project passed on the rebuilt files. The new Reset case failed before the fixture could model an uncooperative response, then passed with a gate that ignores both request cancellation and owner release. It observed the 5-second drain timeout, poisoned host, blocked Reset/scenario replacement, retained old graph and cleanup after explicit release. This bounded cooperative cleanup does not prove termination of a permanently hung worker. No High Contrast setting was changed or tested. Native keyboard activation of the remaining actions and all SEC-L01–SEC-L12 attended states remain open. This follow-up does not approve SEC-T05 or authorize Local/SQL work.

**Further SEC-T04 keyboard and pane review — 2026-10-25:** Source/test commit `00f3d155c60d2404de7691bf5b28fbdedf9fee14` retains the same vendor pins and SQL-free Lab graph. A live physical100 keyboard observation found that Tab from User search skipped its activity filter and list. Both User and Role native Tab cases failed 2/2 before explicit search/filter/list `TabIndex` ordering, then passed 2/2. Local create and password-reset validation cases failed before the controls rejected empty or over-72-byte UTF-8 passwords; the corrected controls keep the dialog open, focus the field and clear its secret buffer without dispatch. Independent Role/Catalog denied and failed reload cases exposed a null-role draft comparison in render: a Role error could incorrectly turn the separate Catalog pane Failed. The guard was corrected and all four pane cases passed. Native Enter opened User/Role create dialogs; the Escape cancellation cases failed 2/2 until an explicit Cancel button was wired, then passed 2/2 without a mutation. Focused green runs: validation/Tab 4/4, pane 4/4, Enter/Escape 2/2. The first full Debug/net48 project run at physical100 was 134/134 PASS before the pane and Escape corrections. `GetScaleFactorForMonitor` returned **125** before and during the final run; the final TRX at `TestResults/security-t04-final2-native125/security-t04-final2-native125.trx` records **140 discovered/executed, 140 PASS, 0 failed/skipped/not-executed**, with `MYDMSVN_UIN_EXPECTED_SCALE=125`, VSTest `17.11.1` x64 and SDK `8.0.426`. Raw TRX remains local and uncommitted. These automated and bounded native observations do not prove the whole attended SEC-L01–SEC-L12 matrix or all keyboard actions. High Contrast remains **NOT RUN / OWNER-WAIVED** for SEC-T04; SEC-T05 remains NOT APPROVED.

**SEC-T04 L02 and grant-keyboard follow-up — 2026-10-25:** Source/test commit `c56fb96b80844e3dfa126e05d3fee0b8e5d7af52` adds held/loading → successful-empty and explicit access-restoration checks for independent Users, Roles and catalog panes. The two new held/empty cases passed 2/2, and six denied/failed → restored rows passed 6/6. A new native Role-grant test found that clicking a checkbox and then pressing Space lost the selected key during rendering; the underlying CheckedListBox also emitted opposing `ItemCheck` events for one Space, reverting the draft. The candidate now retains selection by key, updates existing rows without rebuilding the list, and handles Space once against the role draft while mouse `ItemCheck` remains separate. The focused native/contrast selectors passed 2/2. In a live Lab at physical125, an actual `SendInput` Space after a click returned `Sales.Receipts.Read` from unchecked to checked and cleared the draft with no `Security.Mutate` journal entry. The final Debug/net48 TRX at `TestResults/security-t04-space-native125/security-t04-space-native125.trx` records **143 discovered/executed, 143 PASS, 0 failed/skipped/not-executed** with `MYDMSVN_UIN_EXPECTED_SCALE=125`; raw TRX and screenshots remain local and uncommitted. This strengthens L02/L07 and keyboard readiness, while the complete SEC-L01–SEC-L12 review rehearsal and remaining keyboard actions stay OPEN. High Contrast remains **NOT RUN / OWNER-WAIVED** for SEC-T04; SEC-T05 remains NOT APPROVED.

**Further keyboard selectors — 2026-10-25:** Source/test commit `6b5d24750dd3fd4be67e274e66e52c9918c83c63` adds actual native Down/Space coverage for user direct Allow/Deny/Inherit and Down/Enter/Space coverage for read-only catalog navigation, with no implicit assignment mutation. Both focused cases passed 1/1. The full Debug/net48 TRX at `TestResults/security-t04-keyboard-native125/security-t04-keyboard-native125.trx` records **145 discovered/executed, 145 PASS, 0 failed/skipped/not-executed** with `MYDMSVN_UIN_EXPECTED_SCALE=125`. This is the latest automated candidate revision; the broader scenario rehearsal and remaining command-keyboard paths still need evidence before SEC-T04 can be marked review-ready. No SEC-T05 decision is inferred.

**Owner exception — 2026-10-25:** The Owner explicitly directed that SEC-T04 does **not require a High Contrast test**. Record that context as **NOT RUN / OWNER-WAIVED**, not PASS. This exception applies only to the SEC-T04 review-readiness checklist; it does not establish High Contrast behavior, waive the other open SEC-T04 checks, approve the running candidate at SEC-T05 or authorize SEC-T06/SEC-T07.

**Scenario mapping:** SEC-L01/05/09 structure and read-only explanations/catalog: `SecurityWorkspaceControlTests`, `SecurityPermissionProjectionTests`; L02 independent Users/Roles/Catalog denied/failed → restored reload plus held/loading → successful-empty state: `SecurityPaneStateTests`; L03 profile/protection and local create/password validation: `SecurityWorkspaceLifetimeTests.Profile_create_rename_activation_and_password_use_explicit_commands`, `SecurityWorkspaceControlTests`; L04 membership/read-only members: `Membership_uses_both_loaded_revisions_and_refreshes_both_entities` plus control member navigation; L06 direct tri-state: lifetime direct and import tests; L07/08 serial/partial/uncertain Save and native role-grant Space: `SecurityAssignmentSaveTests`, `SecurityNativeAndResetTests` and lifetime recovery tests; L10 dirty close: lifetime close tests; L11 session replacement: lifetime/control tests; L12 Reset/native Tab/Enter/Escape: `SecurityNativeAndResetTests` plus retained `UiLabResetTests`. This maps automated behavior only. Scenario IDs L03–L07/L09–L12 share a base fixture; selecting an ID does not execute or pass that scenario. Full attended matrix evidence and Owner decision belong to SEC-T05, which has not started.

### Baseline contracts that constrain implementation

| Existing source | Required consequence |
|---|---|
| [FeatureApiClients.cs](../../src/MyDmsVn.Desktop.Application/FeatureApiClients.cs) and [mutation DTO](../../src/MyDmsVn.Contracts/Security/SecurityAdministrationMutationRequest.cs) | `MutateAsync` changes one operation/key with expected revisions. No bulk role-grant replacement or arbitrary-selected-user effective-rights endpoint exists. |
| [Administration DTOs](../../src/MyDmsVn.Contracts/Security/SecurityAdministrationDtos.cs) | Create returns revisions rather than a new ID; reload/identify safely. Membership has primary and related revisions. Use loaded IDs for counts even when details are unavailable. |
| [Legacy view-model](../../src/MyDmsVn.Desktop.Application/Security/SecurityAdministrationViewModel.cs) | Do not reuse its stale/missing-data projection or loop its result-swallowing `SetRolePermissionAsync` for role Save. Preserve supported operations, not ambiguous presentation. |
| [Production factory](../../src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs) | `SecurityAdministrationControlFactory.Create()` currently builds the legacy control/view-model. Assert that isolation through SEC-T05; switch only in SEC-T06. |
| [Close coordinator](../../src/MyDmsVn.Desktop.WinForms/WorkspaceCloseCoordinator.cs) | Candidate implements existing `IWorkspaceCloseParticipant`; `WorkspaceSaveOutcome` and local editor revision must not be confused with server assignment revision. A refresh failure cannot admit Save-and-Close. |
| [Local Security client](../../src/MyDmsVn.Desktop.Infrastructure.Local/LocalSecurityAdministrationApiClient.cs) | Current plain `SendAsync` has no captured-session admission fence. Pre-gate fake ownership tests do not prove the Local check-to-dispatch boundary. SEC-T06 must close and test that seam. |
| [SQL security tests](../../tests/MyDmsVn.Server.Infrastructure.Security.Tests/SecurityAdministrationSqlTests.cs) | Existing single-command revision/audit/import guarantees remain the reference. Serial UI Save adds no transaction spanning those commands. |

## 2. Implementation tasks

All new type, file and test names below are **proposed** until implemented and discovered. Existing source links identify inspected seams. Keep each behavior change with its focused tests; establish meaningful failing behavior before its correction, then stop once the affected contract is verified.

### SEC-T00 — Define dataset, explanation and draft foundations

**Create:** `src/MyDmsVn.Desktop.Application/Security/SecurityWorkspaceState.cs`, `SecurityPermissionProjection.cs`, `SecurityRolePermissionDraft.cs` and `SecuritySaveOutcome.cs` in that same directory; `tests/MyDmsVn.Desktop.Ui.Tests/SecurityPermissionProjectionTests.cs` and `SecurityAssignmentDraftTests.cs`.

**Interfaces:** the spec's `SecurityProjectionSnapshot`, `SecurityPermissionExplanation Evaluate(SecurityProjectionSnapshot snapshot, string permissionKey)`, `SecurityDatasetState`, `SecurityRolePermissionDraft` and `SecuritySaveOutcome`. These are application-only models, with no WinForms or SQL types and no transport DTO changes. The candidate view-model takes an explicit captured session version and already-bound administration/catalog dependencies; both retain the same owner for the workspace lifetime.

- [ ] Reconcile current registry lifecycle, user/role activity, direct decisions, protection metadata and per-pane read authority against the spec. Record drift before implementing a different rule. Model each dataset's state/freshness/session separately; missing data must not be represented as a successful empty collection.
- [ ] Add `SecurityPermissionProjectionTests.Direct_deny_explains_precedence_over_active_role_grant`, `Missing_role_data_does_not_fabricate_default_deny`, `Unavailable_catalog_does_not_classify_every_key_as_unknown`, and `Protected_marker_does_not_claim_superuser_bypass`. Cover direct Allow/Deny, active/inactive user/role/key, observed-but-incomplete role provenance and complete ordinary-account default deny with meaningful theory rows.
- [ ] Add `SecurityAssignmentDraftTests.Filtering_preserves_hidden_changes_and_target_identity` and `Reverting_to_baseline_produces_no_mutations`. Separate immutable loaded baseline, current draft and submitted snapshot; keep server assignment revision distinct from editor identity/local edit revision/session owner. Produce an ordinal changed-Active-key list only.
- [ ] Define orthogonal completion and stop-reason fields. A confirmed item followed by conflict or uncertainty must retain both partial completion and the specific stop reason. Do not collapse uncertain, rejected and unattempted items into one boolean failure.
- [ ] Run these focused tests without Local/Server/SQL composition. Inspect the production and sample/test project graphs before introducing any new reference; preserve current package/lock pins.

**Exit:** deterministic application models express the spec without inferring live authorization or batch atomicity. No UI or SQL acceptance is claimed.

### SEC-T01 — Add deterministic Security scenarios and fake ports

**Create:** `samples/MyDmsVn.Desktop.UiLab.Scenarios/SecurityScenarioData.cs`, `ScenarioSecurityAdministrationApiClient.cs`, `ScenarioPermissionCatalogApiClient.cs` and `SecurityScenarioCatalog.cs`; `tests/MyDmsVn.Desktop.Ui.Tests/SecurityScenarioBehaviorTests.cs`.

**Modify only as needed:** [ScenarioInfrastructure.cs](../../samples/MyDmsVn.Desktop.UiLab.Scenarios/ScenarioInfrastructure.cs) for reusable gate/journal/task tracking, without changing Sales behavior. The existing Sales `ScenarioPermissionClient` is not a Security catalog fake.

**Interfaces:** implement existing `ISecurityAdministrationApiClient` and `IPermissionCatalogApiClient` with fake constructors bound to the captured workspace session version; reuse `ScenarioGate<T>`, `ScenarioJournal`, `ScenarioDesktopSession` and scenario-owned tracking. Publish fixture IDs/revisions for aliases in the [review guide](../development/SECURITY_UI_LAB_REVIEW.md).

- [ ] Model independent Users/Roles/catalog access, readiness and failure; Active/Deprecated/orphan keys; ordinary/protected/inactive users; active/inactive roles; memberships and direct decisions. Treat ordinary inactive-target editing separately from import's active-target restriction.
- [ ] Implement realistic single-command responses: returned revision chain, no-op, both membership revisions, validation/conflict/forbidden/missing target and creation followed by list reload. Match actual envelope semantics: validation is 400/`ValidationError` with specific details; conflict is 409, missing target 404 and forbidden 403.
- [ ] Support deterministic held reads/writes, first-item success then second-item rejection, uncertain completion, refresh failure, cancellation-ignoring late response, and changed actor/session. Sanitized journals record target/key/revisions and call order; redact passwords **before** journaling, and never retain raw password-bearing requests in fixture snapshots.
- [ ] Add `SecurityScenarioBehaviorTests.Independent_reads_preserve_authorized_panes`, `Membership_returns_both_revisions`, `Held_mutations_can_complete_after_cancellation_without_retargeting`, and `Password_requests_are_redacted_before_journaling`. Verify fake behavior and bounded drain, not just scenario names.
- [ ] Inspect evaluated transitive project references and verify no Local/Server/SQL composition. Do not reference broad Desktop.Tests or Desktop.Session.Tests from Ui.Tests; reuse only the existing permitted support links or narrowly justified SQL-free helpers.

**Exit:** repeatable SEC-L fixtures and controlled responses support actual candidate review without a connection string, SQL instance or real Login.

### SEC-T02 — Build the candidate views and host them in UI Lab

**Create:** `src/MyDmsVn.Desktop.Application/Security/SecurityWorkspaceViewModel.cs`; `src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs`, `SecurityUserDetailView.cs`, `SecurityRoleDetailView.cs` and `SecurityPermissionCatalogView.cs`; `tests/MyDmsVn.Desktop.Ui.Tests/SecurityWorkspaceControlTests.cs`.

**Modify:** [UiLabForm.cs](../../samples/MyDmsVn.Desktop.UiLab/UiLabForm.cs), [ScenarioControlHost.cs](../../tests/MyDmsVn.Desktop.Ui.Tests/ScenarioControlHost.cs) and existing Lab tests only where host composition changes. Keep [Program.cs](../../samples/MyDmsVn.Desktop.UiLab/Program.cs)'s STA host and existing Sales scenarios.

**Interfaces:** candidate uses existing `IActivatableDesktopControl`, `IUiDispatcher`, session and workspace feedback/lifetime primitives. Do not modify `SecurityAdministrationControlFactory.Create()` in this task. Names of private controls/helpers may follow the selected toolkit APIs without adding a generic form framework.

- [ ] Add Security module/scenario selection to the current Sales-oriented toolbar and create the actual candidate with Security fakes. Record the selected module/scenario and sanitized journal in Lab-only controls. Production candidate views must not depend on the sample assembly.
- [ ] Implement the three main tabs, independent user/role selections and the exact child tabs in the spec. Render user headers without role chips/manage shortcut; render role headers without the duplicate member-count icon/text. Keep tab counts dynamic and distinct from filter counts.
- [ ] Implement read-only effective explanations, role members and catalog. Member navigation enters the selected user's Roles tab when authorized. Show source completeness, catalog lifecycle and selected-key details; canonical keys remain available even when visually ellipsized.
- [ ] Add `SecurityWorkspaceControlTests.Headers_do_not_duplicate_child_tab_membership`, `Catalog_has_no_assignment_actions`, `Members_open_the_authorized_user_roles_tab`, and `Unknown_data_is_not_presented_as_an_empty_success`. Mount real controls with real HWNDs, a message pump and deterministic disposal.
- [ ] Check Light/Dark and normal/minimum shell outer sizes (`1200 × 760`, `900 × 600`) with actual client/work-area bounds. Reflow and local vertical scrolling must keep actions reachable without workspace-wide horizontal scrolling or a larger shell minimum. Preserve keyboard order, accessible labels, source text and focus.
- [ ] Prove pre-gate composition: production factory still constructs the legacy view-model/control; Lab constructs the new candidate. Verify evaluated Lab isolation and release references; a source-only architecture scan is insufficient.

**Exit:** the actual candidate can be reviewed through SEC-L01/02/05 and catalog/member portions of SEC-L04/09; no production cutover.

### SEC-T03 — Implement explicit editing, serial Save and close ownership

**Create:** `src/MyDmsVn.Desktop.Application/Security/SecurityAssignmentSaveCoordinator.cs`; `tests/MyDmsVn.Desktop.Ui.Tests/SecurityAssignmentSaveTests.cs` and `SecurityWorkspaceLifetimeTests.cs`. Extend the candidate, scenario ports and control tests from prior tasks.

**Interfaces:** `Task<SecuritySaveOutcome> SaveRolePermissionsAsync(SecurityRolePermissionDraft draft, CancellationToken token)` uses raw `ISecurityAdministrationApiClient.MutateAsync` and an injected captured-owner admission check. Implement existing `CaptureCloseState()` and `SaveForCloseAsync(CancellationToken)` on the candidate control; keep `WorkspaceCloseCoordinator` as the close authority.

- [ ] Implement create/profile/rename/activation/password actions with explicit submission and visible target; no typing-triggered writes. Safely identify newly created targets through reload. Retain nonsecret validation input; clear credential UI/view-model references at the defined attempt/lifetime boundaries and require re-entry. Protected-account changes dispatch nothing.
- [ ] Centralize membership writes in **Người dùng → Vai trò (n)**. Each explicit add/remove sends one User-target `SetMembership`, using user and role revisions from latest successful authorized loads; consume both returned revisions and reload both affected entities. Role Members remains read-only even with role-management permission.
- [ ] Implement direct decisions as the three radio choices **Theo vai trò / Cho phép / Từ chối**, with one-key **Lưu quyền này**, committed-versus-draft preview and explicit cancel. Implement staged role checkboxes, whole-draft pending count, discard and **Lưu quyền vai trò**; filtered-out changes persist.
- [ ] Implement ordered single-key role Save exactly as spec section 7: freeze owner/baseline; chain actual returned revisions including no-op; stop at the first non-success/uncertain outcome; preserve per-item results and independent stop reason. No parallel mutation, legacy setter loop, compensating rollback or automatic retry. Correlation IDs are not assumed idempotency tokens.
- [ ] Add `SecurityAssignmentSaveTests.Serial_save_chains_returned_revisions_including_noop`, `Conflict_after_success_preserves_committed_and_unattempted_keys`, `Unknown_outcome_never_replays_automatically`, and `Refresh_failure_does_not_relabel_confirmed_writes_as_unsaved`. Include empty diff, permission loss, membership-induced revision change and explicit reload/review before retry.
- [ ] Add `SecurityWorkspaceLifetimeTests.Dirty_navigation_and_close_preserve_the_same_editor_revision`, `In_flight_write_blocks_ordinary_navigation_and_close`, and `Forced_session_replacement_stops_later_items_and_discards_old_results`. Exercise Save/Discard/Stay, rejected save, changed direct-key/target/tab, read-only navigation, same-row rebind and disposal. Add `Context_actions_and_refresh_resolve_dirty_owner_without_overwrite`: activation/password/rename and other same-entity mutations or ordinary baseline-replacing refresh must resolve the draft first. Distinct recovery reload may read current state alongside retained intent for comparison, without automatic replay. A confirmed write with failed required refresh keeps Close open.
- [ ] Resolve partial/unknown outcomes with explicit authorized reload and current-state comparison. A matching value after reload is not confirmation of the earlier unknown operation. Authority loss scrubs protected presentation; forced logout invalidates the owner without the ordinary dirty prompt.

**Exit:** SEC-L03/04/06/07/08/10/11 behavior is demonstrable with fakes. Fake session tests establish candidate ownership only; Local dispatch admission remains a post-gate obligation.

### SEC-T04 — Complete import, recovery and native-review readiness

**Create:** `tests/MyDmsVn.Desktop.Ui.Tests/SecurityPermissionImportTests.cs`. Extend candidate controls, scenarios, focused control/lifetime tests and the Lab toolbar tests where required. Update [UI_LAB.md](../development/UI_LAB.md) to describe implemented Security commands only when they actually exist.

**Interfaces:** retain `PreviewPermissionImportAsync(...)` and `ApplyPermissionImportAsync(...)` with exact original JSON, selected target, preview revision and catalog digest. Import remains schema-v1 additive and separately atomic; no replace-all or role-Deny semantics.

- [ ] Add contextual JSON import to user direct decisions and role grants. Resolve another draft first, show target-specific preview/confirmation, invalidate on target/input/session/draft/catalog changes and send no Apply on cancel. Preserve 1 MiB/5,000-entry limits and protected/inactive-target restrictions.
- [ ] Add `SecurityPermissionImportTests.Apply_uses_the_original_json_target_revision_and_catalog_digest` and `Target_or_catalog_change_invalidates_preview_without_retry`. Cover malformed/unknown/Deprecated keys, conflicting decisions, no-op, forbidden and stale preview. Keep legacy import tests in their existing owner until cutover mapping is recorded.
- [ ] Exercise the whole SEC-L01–SEC-L12 matrix. Distinguish loading, no selection, successful-empty, filtered-empty, denied, failed, dirty, busy, conflict, partial save and uncertain result. Verify inactive ordinary targets remain editable where the current administration contract permits; import restrictions are separate.
- [ ] Use existing workspace alerts/toasts/validation/spinners and safe details. Test canonical key fallback for future unmapped display names, long names, hidden draft rows, persistent errors and per-pane data revocation. No raw exception/credential display or duplicate shell/workspace errors.
- [ ] Test actual native per-key input, caret/selection/focus and keyboard activation. Extend existing permitted native support only if needed; do not treat whole-string assignment or handler invocation as native typing evidence. Check both themes and minimum/normal geometry. High Contrast is NOT RUN / OWNER-WAIVED for SEC-T04 only under the dated exception in section 1A.
- [ ] Exercise Reset with pending work and cancellation-ignoring responses, repeated arm/release, finite drain and timeout. Cancel/drain/dispose the old graph on the pumped UI thread before creating another; failed drain blocks replacement of that host. Preserve the Sales `UiLabResetTests` and `UiLabToolbarReviewTests` behavior.
- [ ] Run the whole isolated Ui.Tests project once for this candidate, then only focused affected selectors. Collect nonempty discovery and actual executed rows. Record physical native100/native125 observations separately; unavailable Windows/DPI contexts remain NOT RUN / BLOCKED.

**Exit:** review-ready candidate with evidence for all Lab scenarios, unresolved findings explicitly listed, no Local/SQL composition and no production factory cutover.

### SEC-T05 — Obtain attended UI Labs approval before integration

**Create when real evidence exists:** `docs/evidence/security/20261025-security-ui-lab.md` and sanitized candidate captures/TRX summaries. Update this plan's gate record only from actual review. Use the [review guide's evidence template](../development/SECURITY_UI_LAB_REVIEW.md#gate-evidence-template); do not create a second task ledger or machine approval manifest.

- [ ] Demonstrate the actual candidate in UI Lab, covering SEC-L01–SEC-L12 with fixed fixture identities/revisions and recorded gate-release order. Include account/role maintenance, membership, direct Allow/Deny/Inherit, role Save, incomplete data, import, partial/conflicting/uncertain writes and Close/session/Reset behavior.
- [ ] Record source/test SHAs, vendor gitlinks, configuration/bitness/toolchain, evaluated Lab/release references, exact commands and nonempty executed Facts/Theory rows. Distinguish hosted automation, native observations and attended review; no SQL/authentication claim follows from fake-port evidence.
- [ ] Record Light/Dark at physical 100% and 125% with actual monitor scale, reported DPI, client geometry, native focus/caret behavior and sanitized screenshots. Confirm all user-header/role-header corrections and read-only catalog. A resized image or virtualized `DeviceDpi` is not native125 evidence.
- [ ] Resolve blocking findings and rerun only affected checks; preserve original failure evidence. Required unavailable checks keep the gate open. Document declared exceptions without silently converting them into PASS.
- [ ] Obtain an explicit **dated Owner approve/reject decision**, linked to the exact reviewed candidate and scope. Record it in section 3 before beginning SEC-T06, SEC-T07 or SEC-T08. A mockup acknowledgement, documentation PR approval/merge or green test run is not this decision.

**Exit:** only an explicit approved gate admits integration. If rejected or incomplete, remain in SEC-T00–SEC-T04 correction/review; do not start SQL work in parallel.

### SEC-T06 — Connect the approved candidate through session-bound Local admission

**Precondition:** approved SEC-T05 for the actual candidate. Reconcile any UI/behavior drift before proceeding.

**Modify:** [FeatureApiClients.cs](../../src/MyDmsVn.Desktop.Application/FeatureApiClients.cs), [DesktopServiceCollectionExtensions.cs](../../src/MyDmsVn.Desktop.WinForms/DesktopServiceCollectionExtensions.cs), [LocalSecurityAdministrationApiClient.cs](../../src/MyDmsVn.Desktop.Infrastructure.Local/LocalSecurityAdministrationApiClient.cs), [LocalPermissionCatalogApiClient.cs](../../src/MyDmsVn.Desktop.Infrastructure.Local/LocalPermissionCatalogApiClient.cs) and implementing test/sample fakes for interface compatibility. Create `tests/MyDmsVn.Desktop.Session.Tests/LocalSecurityAdministrationApiClientTests.cs`; extend [PermissionCatalogClientTests.cs](../../tests/MyDmsVn.Desktop.Session.Tests/PermissionCatalogClientTests.cs). Map affected legacy Session/Desktop tests before retiring duplicate UI paths.

- [ ] Add proposed application-only `ISecurityAdministrationApiClient ForSession(long expectedSessionVersion)` and `IPermissionCatalogApiClient ForSession(long expectedSessionVersion)` methods to their respective interfaces. Follow [LocalSalesReceiptApiClient.cs](../../src/MyDmsVn.Desktop.Infrastructure.Local/LocalSalesReceiptApiClient.cs): immutable bound version, same-version binding returns self, different-version rebinding throws. Pre-gate fake constructors already retain this owner; compatibility changes must preserve their reviewed behavior. No transport endpoint/DTO or SQL change is required.
- [ ] Route all five bound administration operations and bound catalog reads through existing `LocalRequestDispatcher.SendForCurrentSessionAsync(request, null, token, expectedSessionVersion)` in [LocalRequestDiagnostics.cs](../../src/MyDmsVn.Desktop.Infrastructure.Local/LocalRequestDiagnostics.cs). Its captured-generation check must run before account/permission/handler admission; preserve response fencing. Retain legacy unbound `SendAsync` behavior for other consumers. A wrapper that merely checks the session and then calls unbound `SendAsync` is insufficient. No dispatcher signature change is selected.
- [ ] Add deterministic bound-client tests for replacement/signout before admission, replacement during account validation, replacement after handler admission, old Unauthorized completion, same/different-version rebinding and serial item boundaries. Use the existing Sales Local test pattern with fake application handlers and no SQL. Assert `401 Auth.SessionChanged`, zero additional account/permission/handler admissions where applicable, no data reaching a new session and no expiry of the replacement session. An already admitted old-actor mutation may finish: fenced output does not prove rollback, and no later item may use the replacement actor. Extend catalog tests for equivalent read fencing; preserve request/permission/revision/digest parity.
- [ ] Make `SecurityAdministrationControlFactory.Create()` capture `_session.Version` once, bind both ports to that value and pass that same owner value to the reviewed view-model/control. The coordinator retains that bound administration instance; it never rebinds per item or resolves an unbound client. Assert this composition. Keep shell key/activation, workspace feedback host and `IWorkspaceCloseParticipant` lifecycle. No sample/fake reference may enter production or release packages.
- [ ] Run affected `SecurityAdministrationViewModelTests`, `PermissionImportViewModelTests`, `SecurityControlAcceptanceTests` and `SecurityAdministrationFormTests` in their existing owners, or explicitly map each superseded obligation to discovered candidate tests before removal. Preserve shared factory/activation interfaces if moved out of the legacy control file. Avoid a permanent duplicate implementation.
- [ ] Record the exact cutover diff and candidate identity. A visible behavior/shared-contract change reopens the affected UI Labs review; obtain its approval before relying on the earlier gate. Keep SQL verification in SEC-T07.

**Exit:** approved components are wired through the existing Local service boundary, the replacement-session admission race is tested and mapped legacy obligations remain covered. This is not SQL acceptance yet.

### SEC-T07 — Verify focused real-SQL integration after the gate

**Precondition:** approved SEC-T05 and completed SEC-T06. No SQL fixture or procedure work before this point.

**Create:** `tests/MyDmsVn.Server.Infrastructure.Security.Tests/SecurityWorkspaceSqlIntegrationTests.cs`. Reuse existing application/Local references and SQL fixture ownership in that project. Extend [SecurityAdministrationSqlTests.cs](../../tests/MyDmsVn.Server.Infrastructure.Security.Tests/SecurityAdministrationSqlTests.cs) only for a demonstrated gap. Create `docs/evidence/security/20261025-security-local-sql.md` when execution evidence exists.

- [ ] Use a nonproduction explicit `MYDMSVN_TEST_SQLSERVER_CONNECTION_STRING` with effective `master`, the existing ownership-token lifecycle, serial fixture owners and bounded teardown. Preserve database cleanup/drain/quarantine policy. Missing configuration or unsafe/unproven ownership is BLOCKED, not a fake SQL pass; never record the connection string.
- [ ] Add `SecurityWorkspaceSqlIntegrationTests.Serial_role_save_chains_real_returned_revisions_and_noops`, `Conflict_after_one_commit_preserves_that_commit_and_stops_remaining_items`, and `Membership_change_invalidates_a_stale_role_draft`. Exercise the reviewed coordinator through the real Local port, using existing commands and controlled interleaving, not a synthetic atomic-batch API.
- [ ] Assert database assignments, returned revisions and audit evidence for each confirmed operation. A later item's failure does not undo an earlier successful item's transaction. Use the existing independent audit-failure rollback test for a single command; do not generalize it to all keys in a serial Save. A lost response's actual commit requires authoritative evidence, not UI value matching.
- [ ] Preserve direct Deny/Allow precedence, inactive/protected boundaries, unknown/non-Active key validation, authority failures, both membership revisions and preview/apply revision/digest revalidation. Check the real response envelope, including validation normalization, rather than SQL error names as assumed top-level codes.
- [ ] Run only the existing methods selected in section 4 plus implemented/discovered new cases and any directly affected Server.Application selectors. Inspect actual outcomes and cleanup; no whole SQL suite, unrelated Sales/Post/release gate or inherited six-owner matrix is selected.
- [ ] If a source defect requires a changed transport/schema/permission contract, stop that expansion, record evidence and revise the scope and affected Lab gate before implementation. Do not silently turn this plan into an atomic replacement or registry-migration project.

**Exit:** bounded Local/SQL evidence supports the approved Security operations and failure/revision behavior. SQL success does not substitute for the UI Labs Owner gate or establish full product/release acceptance.

### SEC-T08 — Reconcile acceptance and handoff

**Modify:** this plan, the candidate evidence records and [execution index](README.md) when the workstream is actually complete. Update [UI_LAB.md](../development/UI_LAB.md), the spec or review guide only to reconcile implemented names/commands and approved decisions.

- [ ] Trace each SEC-UI decision, SEC-L scenario and task exit to actual candidate evidence. Record exact final source/test/pin identities; distinguish Lab, Local/SQL and any real-app observations. Leave unresolved/NOT RUN checks explicit.
- [ ] Recheck only behavior affected by post-gate integration, including shell activation/close/reopen, protected-data clearing and real error presentation. If UI behavior changed, reopen and resolve the affected Owner review before declaring the scope accepted.
- [ ] Confirm no sample/fake release dependency, stale legacy implementation, missing mapped test obligation, changed toolkit pin or unexpected schema/transport change. Review the final diff and retain original failures/recovery notes.
- [ ] Close checkboxes and gate record only from evidence, summarize remaining limitations and update current-work routing without changing Sidebar state or archived records. Merging a plan or implementation PR alone does not complete these acceptance steps.

**Exit:** a bounded, reviewable handoff for this Security workstream. No claim about real Login, installer/package, full product or production release without separate evidence for that scope.

## 3. Mandatory Owner gate record

**Current decision: NOT APPROVED. Integration is BLOCKED.** The Owner requested this design/plan PR; that request is not approval of an implemented candidate.

| Required field | Current value |
|---|---|
| UI Lab candidate/source/test SHA | `6b5d24750dd3fd4be67e274e66e52c9918c83c63`; bounded implementation evidence in section 1A, not an approved reviewed candidate |
| Evidence record and actual native contexts | Section 1A; interactive physical 100% and physical 125% full Ui.Tests evidence; High Contrast NOT RUN / OWNER-WAIVED for SEC-T04 only |
| Evaluated SQL-free Lab and production factory isolation | Nine-project Lab graph excludes Local/Server/SQL; production factory remains legacy and release graph excludes fake/sample dependencies |
| SEC-L01–SEC-L12 results / blocking findings | Automated behavior mapped in section 1A; full attended matrix and remaining T04 keyboard/review checks OPEN; High Contrast exception recorded in section 1A |
| SEC-T04 High Contrast exception | Owner direction dated 2026-10-25: no High Contrast test required for SEC-T04; no test result or SEC-T05 approval inferred |
| Owner identity and dated decision | Pending — no running-candidate decision |
| Approved scope / declared exceptions | Pending |
| Integration admission | BLOCKED until all required evidence and explicit approval are recorded |

Update this record in the same change that records the Owner's decision. Keep the exact reviewed revision and evidence stable; later differences require an affected-scope review. Do not backfill approval from this planning conversation or a merged documentation PR.

## 4. Focused verification commands and ownership

### Documentation-only PR

Check changed-path scope, `git diff --check`, UTF-8/no-BOM/working-tree line endings, relative Markdown links/anchors, source-versus-proposed names, SVG structure/rendering and fidelity to the reviewed layouts, current-work routing and preserved Sidebar task state. No application build, GUI/native exercise or SQL test is required or claimed by this planning PR.

### Before the Owner gate

Use the [review guide's commands](../development/SECURITY_UI_LAB_REVIEW.md#commands-and-evidence-discipline): locked restore/build of `MyDmsVn.UiLab.sln`/its host, the whole isolated Ui.Tests project once, then focused source-verified selectors. Run on interactive Windows/net48 with the repository SDK and exact recursive pins. Use serialized STA, `WinFormsTestGuard`, a real pump, finite waits and disposal. Inspect project references before treating the run as SQL-free.

New names in this plan are proposals, not already runnable filters. After implementation, list tests and inspect every intended method/theory row before execution. Existing `UiLabResetTests` and `UiLabToolbarReviewTests` are preservation selectors in the Ui.Tests project, despite their `MyDmsVn.Desktop.Tests` namespace. Missing native context, zero discovery and skipped cases remain explicit; setting `MYDMSVN_UIN_EXPECTED_SCALE` declares an expected **physical** scale and does not change it.

### After approved SEC-T05 only

Existing focused selectors are source-verified at the inspected baseline:

| Project | Existing filter / reason |
|---|---|
| `tests/MyDmsVn.Desktop.Session.Tests/MyDmsVn.Desktop.Session.Tests.csproj` | `FullyQualifiedName~SecurityAdministrationViewModelTests` and `FullyQualifiedName~PermissionImportViewModelTests`; map legacy session/import obligations at cutover. |
| `tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj` | `FullyQualifiedName~SecurityControlAcceptanceTests` and `FullyQualifiedName~SecurityAdministrationFormTests`; affected GUI behavior only, sequentially. |
| `tests/MyDmsVn.Server.Application.Tests/MyDmsVn.Server.Application.Tests.csproj` | `FullyQualifiedName~SecurityAdministrationTests`; use only when application authorization/validation behavior is affected. |
| `tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj` | Selected `SecurityAdministrationSqlTests` methods below plus new integration cases after discovery. The existing class namespace is `MyDmsVn.Server.Infrastructure.IntegrationTests`, not the project name. |

The following is a **post-gate** example for the focused SQL project. Read fixture prerequisites in [tests/README](../../tests/README.md) and [TEST_POLICY](../TEST_POLICY.md) first; configure the explicit test connection securely outside this document.

```powershell
$securitySqlProject = 'tests/MyDmsVn.Server.Infrastructure.Security.Tests/MyDmsVn.Server.Infrastructure.Security.Tests.csproj'
$securitySqlFilter = @(
  'FullyQualifiedName~SecurityAdministrationSqlTests.Administration_is_revisioned_audited_and_preserves_direct_decision_precedence'
  'FullyQualifiedName~SecurityAdministrationSqlTests.Audit_failure_rolls_back_the_security_mutation_and_revision'
  'FullyQualifiedName~SecurityAdministrationSqlTests.Conflicting_security_writes_serialize_and_only_one_revision_commits'
  'FullyQualifiedName~SecurityAdministrationSqlTests.Missing_targets_and_bound_superuser_lifecycle_changes_are_rejected'
  'FullyQualifiedName~SecurityAdministrationSqlTests.Permission_import_rejects_stale_catalog_revision_conflicts_and_audit_failure'
) -join '|'
# Append discovered SecurityWorkspaceSqlIntegrationTests cases only after implementation.
dotnet restore $securitySqlProject --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Security SQL restore failed.' }
dotnet test $securitySqlProject -c Release -f net48 --no-restore --list-tests --filter $securitySqlFilter
if ($LASTEXITCODE -ne 0) { throw 'Security SQL discovery failed.' }
# Inspect nonempty discovery and all intended methods before execution.
$securitySqlResults = 'TestResults/security-sql-' + [guid]::NewGuid().ToString('N')
dotnet test $securitySqlProject -c Release -f net48 --no-restore --filter $securitySqlFilter `
  --logger 'trx;LogFileName=security-sql.trx' --results-directory $securitySqlResults
if ($LASTEXITCODE -ne 0) { throw 'Security SQL tests failed.' }
```

Keep SQL owners and GUI processes serial as required by policy. Retain sanitized TRX/call/revision/cleanup summaries with candidate identity. Restore/build success and historical counts are not execution evidence. Broaden a selector only for a concrete changed seam or required gate, and stop once that risk is verified.

## 5. Implementation handoff

Read [AGENTS](../../AGENTS.md), [AI_CONTEXT](../../AI_CONTEXT.md), the [execution index](README.md), the [specification](../design/SECURITY_ADMINISTRATION_UI.md), this plan and [TEST_POLICY](../TEST_POLICY.md). Start with SEC-T00 and current-source reconciliation. SEC-T05 is the mandatory stop for actual Owner UI Labs review; SEC-T06–SEC-T08 remain blocked until that decision is recorded. Do not infer approval from this documentation PR or begin SQL tasks alongside the Lab review.
