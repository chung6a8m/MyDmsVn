# Security administration — UI and behavior specification

**Design basis:** Owner-reviewed conversation and mockups, 2026-10-25. **Original inspected baseline:** commit `0216701835cc3326124a94760186989c1ac4f729` (historical). **Implementation state:** the reviewed candidate and bounded Local/SQL handoff are implemented; the [archived plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md) and [evidence](../evidence/security/20261025-security-local-sql.md) retain exact acceptance and remaining limits.

The [archived implementation plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md) retains historical task state and verification. The [archived Business Compact and independent UI Labs roadmap](../archive/20261028-business-compact-and-independent-ui-labs/plans/20261027-001-business-compact-and-independent-ui-labs.md) retains the bounded presentation/toolkit conformance and separate Lab startup handoff. The narrow [three-exception correction plan](../plans/20261028-001-business-compact-exceptions.md) is complete; its [candidate evidence](../evidence/ui-labs/20261028-business-compact-exceptions.md) records Windows/native PASS and Owner APPROVED/CLOSED on 2026-10-28. The durable [Business Compact policy](BUSINESS_COMPACT.md) governs visual/control choices, and [Independent UI Labs](INDEPENDENT_UI_LABS.md) defines the implemented host design. This specification retains business ownership, permission, draft and recovery behavior. The [visual references](../design-references/security-administration/README.md) illustrate composition; this specification and the Owner's explicit corrections take precedence over illustrative values in the wireframes and original conversation mockups. The [UI Labs review guide](../development/SECURITY_UI_LAB_REVIEW.md) defines the repeatable review exercises.

## 1. Intent, scope and decisions

Replace the ambiguous shared Users/Roles/Permissions editor with **Quản trị truy cập**. Every edit identifies its owning user or role. Separate a role's grants, a user's direct decisions and an explanation of the user's loaded assignments.

| Decision | Required behavior |
|---|---|
| SEC-UI-01 — Object ownership | Three main tabs: **Người dùng**, **Vai trò**, **Danh mục quyền**. No shared User/Role target dropdown and no dependence on an invisible selection in another tab. |
| SEC-UI-02 — User composition | A user list and selected-user detail with **Thông tin**, **Vai trò (n)**, **Quyền hiệu lực**, **Quyền riêng (n)**. |
| SEC-UI-03 — Role composition | A role list and selected-role detail with **Quyền của vai trò** and **Thành viên (n)**. |
| SEC-UI-04 — Catalog | A read-only definition browser with module filters, search, lifecycle and selected-definition details. No assignment controls. |
| SEC-UI-05 — Owner's user-header correction | Remove the role chips and the separate manage/assign-role link from **every user header**, including the direct-decision view. Membership information/actions belong to **Vai trò (n)**. |
| SEC-UI-06 — Owner's role-header correction | Remove the separate people icon and member-count text next to the role's active badge. The detail count belongs to **Thành viên (n)**. Counts in the role master list are permitted. |
| SEC-UI-07 — Read versus edit | Effective rows are read-only and explain sources. Role grants use binary checkboxes. User direct decisions use three named radio options. |
| SEC-UI-08 — Approval order | Review the actual candidate component in a SQL-free UI Lab and obtain dated Owner approval at the selected plan's gate. The original candidate required this before its completed Local cutover/SQL work. The archived UIL plan required a pilot gate before broad SEC conformance and final native approval before product-host parity/handoff; no new SQL work is selected. A mockup or documentation PR is not that approval. |

Preserve account/role create, update and activation, password reset, membership management, direct decisions, role grants and contextual JSON import. Keep .NET Framework 4.8, the current architecture, permission rules, transport DTOs, SQL procedures and dependency pins. No new authentication protocol, permission registry CRUD, automatic grants, role inheritance, audit-history screen, database migration or generic UI framework is selected.

### Approaches and chosen boundary

1. Restyling the current shared Permissions tab would retain cross-tab target ambiguity.
2. **Chosen:** object-scoped desktop workspace, read-only catalog and production components exercised with fake ports in the existing UI Lab. This preserves the current service boundary while making edit ownership visible.
3. An atomic replace-all-role-grants command would require a new contract/server/SQL transaction. This scope instead defines an explicit serial-save workflow with partial-success recovery; it does not promise batch atomicity.

These decisions defined the reviewed candidate. Runtime outcomes and limitations are recorded in the plan and evidence, not inferred from this specification.

## 2. Source baseline and component ownership

| Source at the original baseline | Fact at the inspected commit and current disposition |
|---|---|
| `SecurityAdministrationControl.cs` (retired baseline source) | Three basic tabs; shared assignment target selector; immediate membership/permission mutation; no `IWorkspaceCloseParticipant`. Removed at SEC-T06. Current [SecurityWorkspaceControl](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs) owns presentation. |
| `SecurityAdministrationViewModel.cs` (retired baseline source) | Single-item commands and textual explanations; successful datasets could coexist with failed/stale datasets. Its `IsAssigned` metadata was not an effective-authorization result. Removed at SEC-T06. Current [SecurityWorkspaceViewModel](../../src/MyDmsVn.Desktop.Application/Security/SecurityWorkspaceViewModel.cs) owns session-scoped state. |
| [FeatureApiClients](../../src/MyDmsVn.Desktop.Application/FeatureApiClients.cs) | Existing `ISecurityAdministrationApiClient` and `IPermissionCatalogApiClient`. No bulk replacement or arbitrary-selected-user authorization endpoint. |
| [Administration DTOs](../../src/MyDmsVn.Contracts/Security/SecurityAdministrationDtos.cs) | User role IDs/direct decisions/protection marker; role member IDs/granted keys; returned primary and related assignment revisions. |
| [Mutation request](../../src/MyDmsVn.Contracts/Security/SecurityAdministrationMutationRequest.cs) | One operation/key/decision plus expected revisions; role grants and user direct decisions have different meanings. |
| [Catalog DTO](../../src/MyDmsVn.Contracts/Security/PermissionCatalogDto.cs) | Key, module, description, lifecycle; friendly Vietnamese display names are a presentation concern. |
| [UI Lab runbook](../development/UI_LAB.md) | The host originally covered Sales; the SQL-free Security candidate and scenarios were added in SEC-T00–SEC-T04. |
| [WorkspaceCloseCoordinator](../../src/MyDmsVn.Desktop.WinForms/WorkspaceCloseCoordinator.cs) and [WorkspaceFeedbackHost](../../src/MyDmsVn.Desktop.WinForms/WorkspaceFeedbackHost.cs) | Reuse existing close admission and workspace-owned feedback rather than a new global mechanism. |

**Implemented candidate:** `SecurityWorkspaceViewModel` in Desktop.Application and `SecurityWorkspaceControl` plus focused views in Desktop.WinForms are application-owned production components. The SQL-free Lab constructed the same candidate for review. After the dated SEC-T05 Owner approval, SEC-T06 bound its Local ports and switched the production factory to it; duplicate legacy UI paths were retired and test obligations mapped in the [post-gate evidence](../evidence/security/20261025-security-local-sql.md).

The Lab references Contracts/Desktop.Application/WinForms and its sample scenarios. It must not compose Local, Server, DbMigrator, SQL fixtures or real login/configuration. Existing Sales scenarios remain usable. The current exception-correction candidate pins `vendor/MyDmsVn.WinFormsKit@a5dd3cec6daba71943549afac628e13727c00da5`; SourceGrid inside it is `f4e457b43582bf01892f50bdc74aa480531e5944`. Reuse the pinned controls, theme tokens, alert/toast/validation/spinner and lifetime primitives. Any proven vendor change requires its own reviewed upstream change and explicit pin; it is not assumed here.

## 3. Layout, copy and accessibility

- Keep the existing application shell/navigation identity. The Security workspace owns its internal layout; the [archived Sidebar plan](../archive/20261027-main-roadmap-reset/plans/20261025-001-sidebar-navigation-horizontal-scrollbar.md) retains navigation-overflow implementation and bounded evidence.
- Use a compact master/detail layout: normal master width approximately 240–280 logical pixels, reducible toward 180 at the existing minimum shell size. Keep the detail identity visible above its child tabs. Avoid a third permanent information column.
- Retain the existing shell's normal outer size `1200 × 760` and minimum `900 × 600`; record actual client/work-area bounds at review. Reflow labels/toolbars and use local vertical scrolling at constrained sizes. Do not increase shell minimum size, shrink text to fit, or introduce workspace-wide horizontal scrolling. Long keys/names may ellipsize with full accessible text, tooltip and selected-row detail/copy access.
- Prefer existing compact tokens; target 32–36 logical-pixel input/button heights, approximately 28–32 table-row height and 8/12-pixel spacing where the pinned controls permit. These are design targets to validate in the Lab, not a request to hard-code colors or bypass scaling.
- Dark is the primary visual reference; Light must use the same structure and theme tokens. All labels, inputs, selected rows, disabled states and focus cues must remain readable in both themes.
- Use text plus icon for allow/deny/unavailable and source kind. Color is supplementary. A red direct Deny must remain legible on a selected row.
- Keep logical keyboard order, visible focus, accessible names/values and native per-key editing. No double-click mutation. Space changes a focused role checkbox's draft value only; radio choices have explicit labels. Enter in a search field must not trigger a mutation.
- Search/filter rebinding must preserve raw text, caret/range and stable selected identity. Changing a filter cannot erase hidden draft changes or change the mutation target.
- Use one clear create action per object context. Use explicit action names: **Lưu thông tin**, **Lưu quyền vai trò**, **Lưu quyền này**, **Gán vai trò**, **Gỡ vai trò**, **Hủy thay đổi**. Put activate/deactivate/password actions in contextual menus/dialogs, with target names visible.

For Business Compact conformance, map each surface to the pinned toolkit or a documented capability exception under [Business Compact](BUSINESS_COMPACT.md). The original UIL native pilot was **Quyền của vai trò** with its structured grant table and one account create/edit dialog. The product and independent SEC Lab must share theme/bootstrap, typography and actual client geometry, including dialog presentation. Current reference images and historical screenshots guide composition but do not approve a later candidate.

Conversation mockup captions and illustrative counts are not runtime copy or fixed data. Display canonical permission keys from the catalog/assignment data; never implement shortened example keys such as `Products.Read` or `Inventory.Read`.

## 4. Người dùng

### 4.1 List and identity

Show display name, username and active state in compact rows; filter by text and active state. A protected marker comes from `IsProtectedAccount`, not a username or Administrator role. The detail header contains name, username/email, active/protected state and contextual actions. It has **no role chips, role list or role-management link**.

Keep independent selected IDs for users and roles. Switching the main tab cannot silently change the other tab's target or reuse a stale import preview. A user with no roles is a valid loaded state. No selection shows a neutral selection prompt, not an editable blank account.

### 4.2 Thông tin

Edit username, display name and email according to existing server validation; active state uses an explicit contextual action. **Tạo người dùng** is a clearly separate create mode with a masked temporary-password field. **Đặt lại mật khẩu** opens a target-named masked dialog; password input is not permanently visible in ordinary profile editing.

Typing does not write. **Lưu thông tin** submits one existing Create/Update command; validation retains nonsecret profile fields and focuses the relevant field. The Create result supplies revisions, not a new target ID: reload the authorized list and select the uniquely matching created identity; if it cannot be identified, require explicit selection rather than guessing. Clear password UI/view-model references after an attempted submission finishes or is cancelled, and on target/session replacement or disposal; require re-entry when necessary. Never put passwords in draft snapshots, recovery comparisons, journals, feedback or screenshots. A nonsecret password-entered indicator may support dirty tracking. Clearing references does not guarantee erasure of immutable strings transiently carried by the existing request. Protected-account maintenance stays outside this screen. Do not add Delete-account or password-reveal/export features.

### 4.3 Vai trò (n)

This is the sole membership-management area for the selected user. List assigned roles with their active state; inactive assignments remain visible with **Vai trò ngừng hoạt động — không cấp quyền**. Compute the count from loaded role IDs, including inactive membership. If role details are unavailable, retain known membership/count and use an unavailable-details placeholder; do not invent names or turn it into an empty list.

**Gán vai trò** selects one readable role and explicitly confirms assignment. **Gỡ vai trò** identifies the user and role and requires explicit confirmation. These are single existing `SetMembership` mutations, not writes caused by checking/rebinding a list. Require user and role revisions from the latest successful authorized loads and the user's management permission. Optimistic revision checks resolve intervening changes. Reload both affected entities after a confirmed response; use returned revisions rather than arithmetic. Cancel/failed confirmation sends no request.

### 4.4 Quyền hiệu lực

Show a read-only table **Quyền | Kết quả | Nguồn quyết định**, with module/search/result filters and a selected-row explanation. No grant checkboxes or Save action. The compact subtitle is **Theo dữ liệu phân quyền đã tải**; provide explicit refresh. This is a projection of loaded assignments, not a new authorization endpoint or a guarantee about a later server decision.

| Display result | Required explanation |
|---|---|
| **Cho phép** | Direct Allow or observed active-role grant; name every visible contributing role, or provide a complete expandable list. |
| **Từ chối** | Direct Deny, inactive account or confirmed inactive/unknown permission; state the reason. |
| **Chưa được cấp** | Complete required ordinary-account data shows neither direct decision nor active-role grant. |
| **Chưa xác định** | Required data is unavailable/stale, or special-account bypass cannot be established from the supplied metadata. Explain the missing information. |

Projection order: first require a successfully loaded current catalog before classifying a key; a successfully known non-Active/missing key cannot be granted. An inactive user cannot gain rights through assignments. For an ordinary active user and Active key, a direct non-null decision determines the result; otherwise an observed active-role grant can explain Allow. Default deny requires complete relevant role data. Mark incomplete provenance even when a determining direct decision or observed grant is available. Failed catalog loading is not evidence that every key is unknown.

`IsProtectedAccount` locks protected mutations but is not proof of configured superuser bypass: the actual service also verifies active identity and configured/current/bound names. For an active protected user without authoritative bypass evidence, show **Chưa xác định** with a concise protected-account explanation. Do not probe another user through `PermissionAuthorizationService.IsAllowedAsync(userId, ...)`, which is bound to the current actor. No new selected-user authorization API is introduced.

Keep dataset/session freshness visible and clear failed or revoked data before projection. The existing separate list reads are not one coherent database snapshot. **Mở quyền riêng** retains the selected user/key and enters that user's direct editor only after navigation admission. In the canonical example, `Sales.Receipts.Post` is granted by Bán hàng but directly denied for `minh.anh`; the explanation must show why Deny wins.

### 4.5 Quyền riêng (n)

Show non-null direct decisions separately from inherited rights, with the applicable role explanation alongside. The count reflects direct decisions, not the number of effective grants. Search or **Thêm quyền riêng** selects an Active catalog key; existing orphan/deprecated assignments remain visible as read-only history when returned, without adding a cleanup command unsupported by the service.

| Exact label | Existing request `Decision` | Meaning |
|---|---|---|
| **Theo vai trò** | `null` | Remove the direct decision; reevaluate active-role grants. No active-role grant can still mean no access. |
| **Cho phép** | `true` | Ordinary-user direct Allow. |
| **Từ chối** | `false` | Ordinary-user direct Deny, taking precedence over role grants. |

Use radio choices, not a two-state checkbox. The editor identifies one user and one key, shows a **Kết quả dự kiến** derived from the draft, and uses **Lưu quyền này**. Preview is not a saved/effective result; dirty changes do not leak into the read-only committed view. **Hủy** restores the baseline without a mutation. Inactive ordinary users and roles remain editable through existing administration commands, subject to authority, revisions and key validity. Inactive state prevents effective access/contribution; it is not a general edit prohibition. Protected users remain maintenance-only. Non-Active keys are not editable, and import retains its separate active-target restriction. Server rejection remains authoritative.

## 5. Vai trò

### 5.1 Identity and role lifecycle

The master list may show name, state and member count. The detail header contains role name, active state, **Sửa tên** and contextual actions; it contains **no separate member-count text/icon**. The two child tabs are **Quyền của vai trò** and **Thành viên (n)**. Administrator is an ordinary role with no special bypass or automatic grants.

Create/rename/activate/deactivate use existing commands and revisions. After Create, reload and select the uniquely matching new role; the command does not return a new target ID, so ambiguous identification requires explicit selection. Deactivating a role does not delete memberships or its stored grants; it stops those grants contributing while inactive. Use explicit confirmation describing affected members. Do not add role deletion, role nesting or a clone/replace-all endpoint.

### 5.2 Quyền của vai trò

Group/filter canonical keys by module. A checkbox means **Cấp cho vai trò**; unchecked means remove that role's grant, never a role Deny. Display existing non-Active/orphan grants separately as read-only when available; filtering must not silently remove them from persisted state.

Checkbox edits update a local draft only. Show a pending-change count and markers, **Hủy thay đổi** and **Lưu quyền vai trò**. The count covers the whole draft, including rows hidden by filters. Reverting a checkbox to its baseline removes that change; an empty diff dispatches zero requests. No misleading select-all checkbox is required in this first version.

Use the serial-save contract in section 7. Role Save changes shared grants; each member's actual result still depends on account/role state and direct decisions. A member link, if retained from the reference image, must say **Xem thành viên của vai trò** and navigate to the existing child tab; it must not imply identical effective rights for all members.

### 5.3 Thành viên (n)

Show members of the selected role, with readable username/display name/state from authorized user data. Use `MemberUserIds` for membership and count; an inaccessible name is not a missing member. Without user-read authority, display unavailable identities and a scoped explanation rather than silently dropping rows.

This tab is read-only in this scope. **Mở người dùng** navigates to that user's **Vai trò (n)** when authorized and admitted; membership writes remain centralized there. Roles.Manage alone never authorizes User-target membership mutation. No duplicate add/remove-members editor is introduced.

## 6. Danh mục quyền

Use the [catalog reference](../design-references/security-administration/04-permission-catalog.svg). The left panel filters modules; the right panel has **Danh mục quyền**, a **Chỉ xem** badge, search by friendly name/canonical key, lifecycle filter and a definition grid. Columns: **Tên quyền | Mã quyền | Nhóm | Trạng thái**. The selected-row details show full key, module, description, lifecycle and **Sao chép mã quyền**.

Copy: **Tra cứu quyền của hệ thống. Gán quyền tại Người dùng hoặc Vai trò.** Active is **Đang dùng**, Deprecated is **Ngừng dùng**. These are definition lifecycle labels, not grant/deny results. No user identity, user child tabs, target selector, assignment checkboxes, direct editor, Save, registry Create/Delete or assignment import appears in this tab.

Counts derive from the successfully loaded catalog; sample `19 / 8 / 5 / 3 / 3` values in the image are illustrative, not constants. Filter count/result count and empty-search copy must be consistent. Friendly Vietnamese names use an application presentation mapping keyed by exact canonical key; future unmapped keys fall back to description/key. Preserve full descriptions and unknown module labels. Catalog digest/application version may support internal preview validity but do not crowd the product UI with implementation metadata.

## 7. Saving, concurrency and recovery

### 7.1 Single edit owner

A workspace has one active mutable draft owner `(target kind, target ID, edit section, editor identity, local edit revision, session generation)`. Rebinding or moving within the same selected row does not replace that owner. Switching target/main tab/edit section or selecting a different direct permission while dirty requires **Lưu / Bỏ thay đổi / Ở lại**. Filters preserve the owner and draft. Ordinary close uses the existing `IWorkspaceCloseParticipant`/`WorkspaceCloseCoordinator`; forced logout/session replacement clears sensitive state without a dirty-close prompt.

Before any other mutation against the same entity, resolve its active draft through the same **Lưu / Bỏ thay đổi / Ở lại** admission. This includes activation, password reset, rename, membership changes and starting another editor; those commands share assignment revisions and must not silently overwrite or invalidate a pending draft. An ordinary refresh that would replace the draft baseline also requires admission. Recovery **Tải lại để đối chiếu** is a distinct read action: it may load current state alongside retained same-owner intent to resolve partial/uncertain outcomes, but never silently replaces that intent or retries a write.

An operation captures the owner and submitted local edit revision. The post-gate Local integration must fence admission against that captured session at the dispatcher boundary; view-model checks and late-result discard alone do not close the check-to-dispatch race. Block competing mutations/target changes while a write is in flight. Late responses, a replaced session or a disposed workspace cannot update a new editor or dispatch later items. Close-save succeeds only after a confirmed successful save plus successful required refresh for that same owner/revision. Failed, partial, conflicting or uncertain save keeps ordinary close/navigation blocked for review.

### 7.2 Serial role save — selected implementation decision

The existing port accepts one key per request. The implemented `SecurityAssignmentSaveCoordinator` calls raw `ISecurityAdministrationApiClient.MutateAsync`; it does not loop legacy `SetRolePermissionAsync`, which hides individual outcomes and refreshes on each call.

1. Freeze role ID, original grant set, desired set, loaded role revision and operation/session owner. Sort changed Active keys with ordinal comparison; do not manufacture changes for filtered/inactive/orphan keys.
2. Send one existing Role/SetPermission command at a time. After each success, chain the exact returned `AssignmentRevision`, including successful `Changed == false` responses. Never use `revision + 1`, parallel writes or a guessed retry token.
3. Recheck ownership/cancellation before each subsequent dispatch. A successful item is committed independently; no shared Desktop transaction or batch rollback is promised.
4. Stop on the first failed/forbidden/conflicting response or uncertain in-flight outcome. Distinguish confirmed applied items, a rejected/unknown item and unattempted items. Never send compensating mutations or automatically replay the sequence.
5. Reload authorized current state, retain remaining same-owner intent for explicit comparison, and require the operator to review the new diff before another Save. Conflict/revision/catalog changes invalidate the old submitted baseline. Loss of authority/session clears affected presentation and does not restore it from cached drafts. Reload establishes current assignments/revisions; a matching desired value does not prove that an uncertain earlier request committed or produced an audit event.

Example failure copy: **Đã lưu 1/3 thay đổi. Hai thay đổi chưa được xác nhận. Tải lại để đối chiếu.** Use precise rejected versus uncertain wording when known. Cancellation/timeouts after dispatch do not establish rollback. A confirmed mutation followed by refresh failure is **đã lưu, chưa tải lại được**, not **chưa lưu**. Import's separate atomic additive transaction is not a substitute for checkbox replacement.

### 7.3 Proposed application-only interfaces

These names are new design decisions, not baseline APIs. Define them in Desktop.Application; do not place WinForms types in them or change transport contracts.

| Proposed type/interface | Contract |
|---|---|
| `SecurityDatasetState` | `NotLoaded`, `Loading`, `Ready`, `Forbidden`, `Failed`; owner/session generation and freshness accompany each users/roles/catalog dataset. |
| `SecurityPermissionExplanation` | Display result `Allowed`, `Denied`, `Unassigned`, `Unavailable`; reason, canonical key, visible role sources, direct decision and provenance completeness. |
| `SecurityProjectionSnapshot` | Selected `SecurityUserDto`, users/roles/catalog dataset states, freshness and session ownership, with the authorized catalog/role data. Immutable input; missing data is not an empty successful dataset. |
| `SecurityPermissionProjection.Evaluate(SecurityProjectionSnapshot snapshot, string permissionKey)` | Returns `SecurityPermissionExplanation` according to section 4.4. Pure function; never checks another identity through the current-actor endpoint. |
| `SecurityRolePermissionDraft` | Role ID, captured server revision, original/desired key sets, captured operation/session/editor identity and submitted local edit revision; freezes an immutable submission and computes an ordinal changed-key list. Server assignment revision and local editor revision are different values. |
| `SecurityAssignmentSaveCoordinator.SaveRolePermissionsAsync(SecurityRolePermissionDraft draft, CancellationToken token)` | Returns `Task<SecuritySaveOutcome>` using the existing administration port and an injected current-owner admission check. One outcome retains confirmed/rejected/uncertain/unattempted keys and the last confirmed returned revision. |
| `SecuritySaveOutcome` | Completion (`NoChanges`, `Complete`, `Partial`, `NoneConfirmed`) is separate from stop reason (`None`, `Rejected`, `Conflict`, `Forbidden`, `CanceledBeforeDispatch`, `OutcomeUnknown`) and refresh outcome. Retain each confirmed/rejected/uncertain/unattempted item; partial completion can coexist with a conflict or unknown result. No error-swallowing `Task`-only API for batch orchestration. |
| `SecurityWorkspaceViewModel` / `SecurityWorkspaceControl` | View-model takes an explicit captured session version plus already-bound administration/catalog ports and owns datasets/drafts/commands; control maps state to existing close/feedback interfaces. Lab fake constructors bind that same owner. The production factory captures/binds once and returns this candidate only after the UI Labs gate. |

Post-gate Local admission follows the existing Sales pattern: implemented application-only `ForSession(long expectedSessionVersion)` methods on the administration and catalog ports retain an immutable version and use the existing dispatcher's `SendForCurrentSessionAsync` path. Same-version binding returns the same instance; rebinding to another version is rejected. The factory captures one version for both ports and the candidate. A bound `Auth.SessionChanged` response fences presentation; an already admitted old-actor write may still finish, so it is not evidence of rollback. No new transport endpoint, request field or SQL procedure was added.

Keep private implementation details within these responsibilities. Any new transport endpoint, atomic batch, registry mutation or changed permission precedence requires revising the spec and affected gate rather than being inferred from a type name.

## 8. Import and independent access boundaries

Keep **Nhập quyền từ JSON…** contextual to the selected role-grants or user-direct-decision editor. Resolve an existing draft before import. Preview names the target and proposed additions; declining sends no Apply. Use exact original JSON, target/revision and catalog digest in the current preview/apply contract. Invalidate preview on target/input/session/draft/catalog changes. Never retarget a preview from a formerly selected user/role.

Preserve schema v1, 1 MiB/5,000-entry limits, canonical Active keys, additive/no-op/conflicting-decision semantics, and inactive/protected target restrictions. Role import grants only; it cannot remove role grants or implement Inherit. Server reauthorization and revision/digest checks on Apply remain authoritative. Conflicts require another preview and explicit review; no automatic retry.

| Operation/data | Existing required authority and UI rule |
|---|---|
| Users list/profile/direct decisions/password/user activation | `Security.Users.Manage`; catalog access is also needed to select/validate keys in the permission editor. |
| Roles list/grants/role activation | `Security.Roles.Manage`; catalog access is needed for editable key definitions. |
| Membership mutation | User-target `SetMembership`, `Security.Users.Manage`, primary user revision and related role revision. This scope exposes it only in the user's Roles tab. |
| Catalog definitions | `Security.Permissions.Read`; a successful catalog read confers no assignment-management capability. |
| Member names and full role provenance | Require the relevant independently authorized list data. A readable role/member ID does not supply readable profile details. |

Handle independent users/roles/catalog read results separately. A forbidden pane cannot clear or misreport another successfully loaded authorized pane. Clear stale protected data immediately on revoked access; do not preserve old lists merely because a refresh failed. Server authorization remains authoritative for every request; UI admission is not enforcement.

## 9. Feedback, lifetime and required states

Each pane distinguishes **Đang tải**, **Chọn một đối tượng**, **Chưa có dữ liệu**, **Không có kết quả phù hợp**, **Không đủ quyền**, **Không tải được dữ liệu**, **Có thay đổi chưa lưu**, **Đang lưu**, **Xung đột**, **Đã lưu một phần**, and **Cần đối chiếu kết quả**. Unknown/failed is not an empty successful result. Counts appear only when their underlying dataset is known.

Use the existing response envelope accurately: validation is status 400 / top-level `ValidationError`, with the specific validation code in `Details`; revision/name conflict is 409, missing target (`Security.TargetNotFound`) is 404 and forbidden actor is 403. Import rejects inactive/protected targets as 409 / `Security.ImportTargetUnavailable`; ordinary protected-target mutation is 400 / `ValidationError` with `Security.ReservedSuperuser` in `Details`. Do not display an internal SQL code as though it were always the top-level API code.

Use the originating workspace's alert/validation/toast host. Keep validation next to fields, persistent errors/recovery in the affected pane and concise success feedback in the same workspace. Safe support details can be copied; do not duplicate the same error in both tab and shell status bars or expose raw exceptions/secrets. Busy indicators do not authorize commands or claim rollback.

Cancellation, target changes, session replacement, close/dispose and Lab Reset must be bounded and ownership-aware. Retain native raw input/focus on rejected navigation. A forced session boundary clears credentials, datasets, drafts and preview state; an old completion cannot repopulate them. Fake journals must redact password-bearing requests; cancellation-ignoring responses are a required scene. Reset cancels/drains/disposes its graph with a real UI pump before another graph is created; a failed drain leaves that host blocked.

## 10. Verification and acceptance boundary

The [UI Labs guide](../development/SECURITY_UI_LAB_REVIEW.md) covers successful, empty, denied, validation, partial-data, dirty, revision-conflict, partial-save, uncertain-result, import and lifetime states. Use the actual candidate components and fake ports; no HTML/image-only substitute can pass the gate. Exercise both themes, mouse/native keyboard, constrained/normal geometry and physical 100%/125% contexts with actual measurements.

The plan's **SEC-T05** gate requires nonempty discovered/executed selected tests, reviewed screenshots/observations, exact source/test/toolkit revisions, resolved blocking findings and dated explicit Owner approval of the running candidate. This document and the conversation mockups define the target; neither establishes that gate as passed.

Only then may **SEC-T06–SEC-T08** connect the reviewed candidate to Local, perform focused real-SQL verification and reconcile the bounded handoff. Any subsequent visible behavior or shared-contract change reopens affected Lab review before relying on its earlier approval. Historical Sales/Sidebar/SQL/release evidence stays with its own candidate and scope.
