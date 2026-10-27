# Security UI Labs — review guide

This is the repeatable review guide for the **implemented** Security workspace (SQL-free Lab candidate and production Local composition). It is **not** a current execution ledger or a replacement for explicit acceptance. The historical [Security execution plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md), [SQL-free candidate evidence](../evidence/security/20261025-security-ui-lab.md) and [Local/SQL evidence](../evidence/security/20261025-security-local-sql.md) own dated gate decisions and remaining gaps. SEC-T05 was Owner-approved on 2026-10-26, followed by bounded SEC-T06–SEC-T08; incomplete SEC-T04/native/attended checks are still open. The [UI and behavior specification](../design/SECURITY_ADMINISTRATION_UI.md) remains normative. The [execution index](../plans/README.md) selects the active [Business Compact and independent UI Labs roadmap](../plans/20261027-001-business-compact-and-independent-ui-labs.md).

Read the [UI Lab runbook](UI_LAB.md), [test policy](../TEST_POLICY.md), [test commands](../../tests/README.md) and [repository instructions](../../AGENTS.md) before running a candidate. Reading or updating this guide does not run GUI/SQL checks or grant runtime acceptance.

## Current Business Compact review scope

Use the durable [Business Compact policy](../design/BUSINESS_COMPACT.md) and [independent Lab specification](../design/INDEPENDENT_UI_LABS.md). UIL-T03/T04 review **Quyền của vai trò** and one account create/edit dialog as a native pilot; only explicit pilot approval permits broad SEC conformance. UIL-T06 separately reviews the completed SEC candidate before the SQL-free product-host check in UIL-T07. Both new gates start NOT APPROVED; the active plan owns their state.

Review the real production controls in the direct SEC Lab after UIL-T02 exists, using the product theme/bootstrap, typography and measured content-client bounds. Cover Dark/Light and physical 100%/125% at normal/minimum product geometry, changed dialogs, command hierarchy, long labels/keys, selected/disabled/focus states and preserved native editing. Record toolkit mappings and justified native/custom exceptions. The existing commands below still build the mixed host until UIL-T02 updates them; a new launcher or result is not implied by this documentation change.

Select relevant scenarios below for changed presentation/bindings and preserve their behavioral expectations. Historical gaps remain recorded in original evidence; the new plan does not silently select the entire unfinished historical matrix or another SQL round. Reset is a developer fixture boundary; verify ordinary Close admission separately.

## Candidate boundary and prerequisites

- Original historical baseline: commit `0216701835cc3326124a94760186989c1ac4f729`; baseline WinFormsKit gitlink: `c232a4c09dc0386a5bd8bb484a5c991acae827d5`. Record the actual candidate and gitlinks for every
  review; baseline facts are not candidate evidence.
- `SecurityWorkspaceControl` and `SecurityWorkspaceViewModel` were proposed relative to that baseline and are now implemented in the production WinForms and Desktop.Application assemblies. The Lab constructs those actual types, not a sample-only imitation.
- The production factory stayed on the legacy control until the Owner approved SEC-T05 on 2026-10-26. SEC-T06 then cut over to the reviewed candidate; SEC-T07 focused SQL integration and SEC-T08 handoff are recorded in the [plan](../plans/20261025-002-security-administration-ui-lab-first.md) and [post-gate evidence](../evidence/security/20261025-security-local-sql.md).
- Use interactive Windows, .NET Framework 4.8, the pinned SDK/submodules and a real WinForms message pump. No connection string, SQL instance or real Login is required for this review.
- New security fakes implement the existing `ISecurityAdministrationApiClient` and `IPermissionCatalogApiClient` ports. Reuse `ScenarioGate<T>`, `ScenarioJournal`, `ScenarioDesktopSession` and the scenario
  assembly's `ScenarioTaskTracker` where appropriate.
- Redact passwords before recording journal entries. Do not place raw security mutation requests, credentials, import secrets or password values in diagnostics, screenshots, exported evidence or persistent
  logs.
- Inspect evaluated transitive references: the Lab/scenario/Ui.Tests graph must exclude Local, Server, DbMigrator and SQL fixture composition. Inspect production/release references separately to exclude
  sample/fake dependencies.
- Existing Desktop and Session tests retain their owners. Do not reference those broad test projects from the Lab; they bring Local/server and, for Desktop.Tests, SQL fixture dependencies.

## Repeatable fixtures and observations

Each SEC-L scenario begins with Reset and its own deterministic fixture. The implementation must publish the fixed fixture identity/revision map with the candidate so another reviewer can repeat the same
actions.

| Fixture alias | Required content |
|---|---|
| `U-ACTIVE` | Ordinary active user; two memberships, direct Allow and Deny, and at least one Inherit key. |
| `U-INACTIVE` | Inactive user with retained assignments. |
| `U-PROTECTED` | User with `IsProtectedAccount=true`; this flag locks editing but does not prove a runtime superuser bypass. |
| `R-ACTIVE` | Active role granting multiple Active catalog keys; at least two members. |
| `R-INACTIVE` | Inactive role sharing one grant with `R-ACTIVE` and owning another grant. |
| `R-OTHER` | Independent role used for selection races and member changes. |
| Catalog | Multiple modules, Active and Deprecated entries, an unassigned Active key, and a controlled unavailable response. |

Use controlled response gates rather than timing-dependent sleeps. Journal operation names, sanitized target/key/revision data, response order and outcome; snapshot submitted drafts separately from
committed fixture state. Record actual UI observations and call counts, not merely the selected scenario name.

Users, Roles and catalog have independent access and freshness. A denied, failed, pending or stale source is not an empty successful result. Where missing information prevents a conclusion, show `Chưa xác
định` and its reason. Use confirmed snapshots for displayed effective results; distinguish any local draft preview explicitly.

## Scenario matrix

### SEC-L01 — Workspace structure, selection and geometry

1. Reset to populated fixtures; open Security and visit Users, Roles and the read-only permission catalog.
2. Select `U-ACTIVE`; visit information, `Vai trò (n)`, effective permissions and direct overrides. Select `R-ACTIVE`; visit grants and `Thành viên (n)`.
3. Resize through the specification's minimum and review widths; repeat with the longest fixture labels and keyboard navigation.

Expected: exactly three top-level tabs; selected identity remains clear. No user-header role chips or role-management shortcut appears on any user subtab. No role-header member count duplicates `Thành viên
(n)`. The `Vai trò (n)` and `Thành viên (n)` tab counts remain. No clipped commands, overlapping labels or unintended horizontal scrolling occurs; counts describe their defined complete scope, not a
filtered subset.

### SEC-L02 — Independent access, empty results and freshness

1. Independently return success, successful-empty, Forbidden, safe failure and held loading for Users, Roles and catalog; keep the other sources successful.
2. Refresh a previously populated source into denied/failed state, then explicitly restore access and reload. Repeat after selecting a user or role.
3. Inspect dependent effective rows, membership details, member names, tab counts and command admission after each response.

Expected: unrelated authorized areas remain usable. Successful-empty has an explicit empty state. Unavailable/stale data has an explicit reason and `Chưa xác định` where needed; it never becomes false
default-deny, zero members or a fabricated empty catalog. Revoked access scrubs protected presentation. Role-side member names remain unavailable without Users access; no substitute lookup bypasses that
boundary.

### SEC-L03 — User information, create, password reset and protection

1. Create a user with missing required fields, then valid fixture values. Edit `U-ACTIVE` information and exercise explicit password reset.
2. Inject validation error, rejected mutation and revision Conflict. Hold a request and attempt a second submission.
3. Repeat authorized ordinary maintenance with `U-INACTIVE`, then attempt it with `U-PROTECTED`; inspect fields, commands, journal and post-operation password references.

Expected: local validation dispatches nothing; a valid explicit action submits the selected identity/revision once. Errors preserve safe editable information. Password fields are masked and cleared at the
specified attempt-completion/lifetime boundaries, including server validation rejection; retain only nonsecret editable fields and require password re-entry. Journals contain only redacted projections. Protected-account edits and membership/direct-override changes dispatch nothing. Protection is not shown as proof that
all permissions are allowed. Inactive ordinary accounts remain editable through supported maintenance commands; import has its own active-target restriction.

### SEC-L04 — Read-only role members and user-owned membership changes

1. Open `R-ACTIVE` → `Thành viên (n)`; inspect the read-only list and use **Mở người dùng** for `U-ACTIVE`. Confirm navigation to that user's `Vai trò (n)`.
2. Only there, explicitly add/remove a role. Inspect User-target requests and confirmed counts from both perspectives; inject a stale user revision, then a stale role revision.
3. Deny `Security.Users.Manage` while allowing role management; separately make Users data unavailable while role data remains available. Try the member-navigation action and membership commands.

For the two stale-revision cases in the SQL-free Lab, Reset `SEC_L04` between cases and open **Security review tools** before opening the membership dialog. Click **Advance U101 revision** or **Advance R202 revision**, then use U101 → **Vai trò (2)** → **Gán vai trò…** to select R202 **Kiểm soát** and confirm. The fixture journal must show the corresponding `Security.ReviewTargetRevision` before exactly one User-target `SetMembership target=101 related=202 revision=11 relatedRevision=22` and a Conflict result. The visible membership remains unchanged and the recovery action requires explicit reload/diff review. These controls affect only the Lab fixture.

To check a continuing membership denial independently of role-management controls, use `SEC_L04` and open **Security review tools**. Check **Deny membership writes**, then send a U101 `SetMembership` request from **Vai trò (2)**. The Lab returns Forbidden and clears Users presentation while Roles/catalog remain available. Use **Reload Security** to restore the fixture's readable Users data; the checkbox stays checked, so another explicit membership request is also denied. The checkbox only controls `User/SetMembership` responses in the SQL-free Lab. It does not emulate the full server permission policy.

Expected: `Thành viên (n)` has no add/remove-members editor and sends no membership writes. **Mở người dùng** routes to the selected user's `Vai trò (n)` only when authorized and navigation is admitted.
All membership writes originate in that user tab and use User-target `SetMembership` with primary user and related role revisions from the latest successful authorized loads. They require `Security.Users.Manage`; role-management
access alone cannot authorize them. Respect both returned revisions and reload both affected entities. Conflict is visible with no automatic retry. Missing Users access prevents member-name disclosure
and user navigation; known member IDs/counts remain distinct from unavailable profile details, and an unknown count never silently becomes zero.

### SEC-L05 — Effective permissions and source explanation

1. Inspect keys with direct Allow, direct Deny, active-role grants, inactive-only role grants, multiple sources and no assignment.
2. Inspect a Deprecated key, `U-INACTIVE` and `U-PROTECTED`. Open source details without entering an edit flow.
3. Independently withhold Users, Roles or catalog information, then restore it by explicit authorized reload.

For one combined-source review in `SEC_L05`, open **Security review tools** with U101's **Quyền hiệu lực** selected. Leave **Hold read** unchecked; arm **Roles / Failure**, then **Catalog / Forbidden**, and use **Reload Security** once. Distinct armed sources are consumed by that reload; arming the same source twice is refused. Use a second explicit Reload to restore the fixture's successful responses. The Lab permits only one held read at a time.

Expected: effective permissions and source details are read-only. Direct Deny wins over ordinary role grants; inactive roles do not contribute effective grants; only confirmed applicable data supports a
conclusion. Unknown/Deprecated keys are never offered as new grants. Missing required source data produces `Chưa xác định`, not a claimed runtime denial. Protected status explains the edit lock without
inventing a verified server binding or bypass.

### SEC-L06 — Direct overrides remain tri-state

1. For an ordinary selected user, choose Allow, Deny and Inherit in turn and commit each through the specification's explicit action.
2. Inspect the submitted `Decision` values and confirmed explanation. Inject a validation error, Conflict and denied mutation.
3. Try a non-Active/Deprecated key and a protected user. Change selected user during an admitted held read; attempt the same change during a held write and verify it is blocked.

Expected: requests preserve `true`, `false` and `null` respectively. Inherit removes the direct override; it does not mean direct Deny. The UI keeps direct assignments separate from effective results and
source details. Invalid/protected actions dispatch nothing. Errors and late responses cannot alter another selected user's draft or confirmed state.

### SEC-L07 — Role grant draft, Save and Cancel

1. Select `R-ACTIVE`; toggle several binary grant checkboxes, including reverting one to its original value. Inspect the journal before Save.
2. Cancel the draft, confirm restored committed values, then repeat edits and Save with controlled successful responses.
3. Inspect request order, each returned revision and the next request; include an unchanged/no-op response and attempt duplicate Save while busy.

Expected: checkbox edits are local and dispatch nothing. Cancel dispatches nothing; unchanged keys are omitted. Save sends serial per-key `SetPermission` calls through raw
`ISecurityAdministrationApiClient.MutateAsync`, carrying the exact returned `AssignmentRevision` into the next request. Do not derive revisions by incrementing locally. Role grants are binary and have no
direct-Deny/Inherit option. Save is not advertised as an atomic bulk operation.

### SEC-L08 — Partial role Save, Conflict and uncertain outcome

1. Stage at least three changed keys; confirm the first write, then fail or conflict the next write. Verify later keys are not sent.
2. Reset and repeat with cancellation/transport failure after dispatch, so the last write's outcome is unknown. Include a successful write whose subsequent refresh fails.
3. Inspect confirmed successes, unresolved/unsent keys and the retained draft. Explicitly reload using currently authorized reads, review the new diff, then choose whether to submit again.

Expected: confirmed partial success is distinguished from failure and uncertainty. No automatic retry, compensation, rollback claim or implicit continuation occurs. Cancellation does not prove that a
dispatched write rolled back. Uncertainty blocks unsafe continuation until authorized reload and review resolve it. A failed/denied reload keeps the outcome unresolved; the UI cannot clear the problem by
treating missing data as an empty state. Partial completion and stop reason remain separate: a later conflict must not erase a confirmed success. Reload establishes current values/revisions, not proof that the earlier uncertain call committed or wrote an audit event.

### SEC-L09 — Catalog and contextual additive JSON import

1. Search/filter the catalog; inspect key, module, description and lifecycle. Try keyboard activation and double-click on its rows.
2. Start import from an authorized selected user or role context; inspect preview, additions, target revision and catalog digest; cancel confirmation, then repeat and confirm.
3. Inject malformed/unknown/Deprecated keys, inactive/protected or unauthorized target, stale preview, target switch and catalog change before Apply.

For stale-preview review in the SQL-free Lab, choose a valid contextual import, then use **Đổi revision đối tượng** or **Đổi catalog digest** in the Lab confirmation dialog before **Áp dụng**. Reset between cases. Inspect the dialog's original preview revision/digest, the journal's Preview → review fault → Apply order, the reported Conflict and the unchanged assignment. For target-switch review, use **Đổi đối tượng** before confirmation; the selected user/role changes, the old preview is invalidated, and the journal must contain no Apply. These controls change only the Lab fixture or its current selection.

For late-preview review, open **Security review tools** and press **Hold next Preview** before selecting a valid contextual JSON import. Once the journal records `Security.ImportPreview` and the workspace shows that JSON is being checked, press **Replace session**, then **Release Preview**. The old response must not open confirmation or Apply, and the replacement session must not regain the old role, catalog or assignment presentation. Reset before another case.

For a Forbidden Preview response, select an active user or role in `SEC_L09`, open **Security review tools**, press **Deny next Preview**, then choose a valid contextual JSON file. Verify `Security.Forbidden`, the affected source's denied state, no confirmation and no `Security.ImportApply`. Explicitly reload, select the target again and repeat Preview without re-arming; the fault is one-shot. Cancel its confirmation. Separately select inactive and protected fixture targets and verify that contextual import is disabled before a file can be chosen. These Lab observations do not prove server authorization.

Expected: catalog navigation never dispatches an assignment mutation. Import is contextual and additive, retains the existing preview/confirmation contract and cannot act as replace-all role Save. Cancel
dispatches no Apply. Apply uses the exact preview target/revision/digest and revalidation result. Stale/invalid/unauthorized/inactive/protected cases remain rejected without automatic retry; a late preview cannot
confirm another target.

### SEC-L10 — Dirty navigation and ordinary Close

1. Create drafts in the information/direct-override/grants areas that support editing; attempt target change, subtab change, top-level navigation and workspace Close.
2. Exercise each specified Save, Discard and Cancel decision; then fail, conflict or hold Save and repeat Close.
3. Verify the same editor identity/revision and draft before and after rejected navigation; inspect the actual close-coordinator path.
4. While a draft is dirty, invoke activation/password/rename or another same-entity mutation and ordinary Refresh; exercise Save/Discard/Stay before any baseline replacement. Separately use explicit recovery reload after partial/uncertain Save.

In the current workspace, the product **Làm mới** button is on **Quyền hiệu lực** and the read-only catalog, not the direct-decision editor. From a dirty direct draft, first record the subtab/top-tab Save/Discard/Stay admission needed to reach that button, then test Refresh. The UI Lab toolbar's **Reload Security** helper is a separate path.

Expected: the specification's draft ownership/admission rules are consistent at every boundary. Cancel/rejected Save keeps the draft and relevant focus; Discard is explicit. Confirmed Save admits Close
only for the same editor/revision and required successful refresh. Other same-entity mutations and ordinary baseline-replacing refresh resolve the draft first; they cannot silently erase it or invalidate it through a competing revision. Recovery reload loads current state for comparison with retained intent, without replay. Busy/uncertain state cannot be dismissed through an ordinary navigation shortcut. Reset is not a substitute for this scenario.

### SEC-L11 — Loading, write-busy admission and session races

1. Hold a Users/Roles/catalog read and a mutation separately; exercise operation cancellation with cooperative and deliberately late responses.
2. During held reads, navigate between admitted user/role/tab selections and close/reopen an otherwise clean workspace. During a held write, attempt target/tab changes and ordinary Close; verify refusal.
3. With either kind of response held, force signout or authenticated-session replacement. Release newer reads before older reads and release old responses after the forced boundary; record origin,
   target, session generation and journal order.

Expected: loading stays responsive, and read-only navigation follows ordinary dirty-state admission. A write in flight blocks competing mutations, target/tab changes and ordinary Close; canceling its
presentation does not prove rollback or admit navigation while its outcome remains unresolved. Forced logout/session replacement still invalidates the old owner and clears sensitive presentation,
passwords and drafts without a dirty-close prompt. A canceled read cannot overwrite fresh data. Origin/lifetime checks reject stale callbacks; hidden tabs, replacement workspaces and new sessions
receive no old data, errors or focus changes, and an old completion cannot dispatch later serial-save items.

### SEC-L12 — Reset, drain, disposal and repeated controls

1. Hold read and mutation gates; Reset with the message pump running. Repeat with gates that ignore request cancellation and with repeated arm/reset/release attempts.
2. Confirm old gates are canceled/released, tracked work drains within the defined bound, controls/subscriptions are disposed on the UI thread, and only then a fresh graph appears.
3. Inject a drain timeout; try another scenario and Reset, then inspect the retained failure and cleanup evidence.

Expected: no earlier gate is stranded or overwritten by repeated toolbar actions. Late callbacks cannot touch the replacement graph. Timeout keeps failure visible and refuses a fresh graph for the
uncertain host instance. A timeout alone does not prove worker termination; cleanup uncertainty is not a clean Reset or a passing result.

## Native keyboard, theme and geometry review

Run SEC-L01 and the affected input/edit/navigation scenarios in Light and Dark at physical native100 and native125. Record actual monitor scale, window/control reported DPI, client dimensions and font
settings separately; never infer physical scale from screenshot size or the process's reported DPI. Additional High Contrast checks apply to changed semantic color/focus behavior under the specification
and test policy.

- Keyboard: traverse lists, top tabs, subtabs, fields, search, checkboxes and explicit actions with Tab/Shift+Tab; use supported arrows/Space/Enter/Escape. Verify visible focus, accessible names,
  predictable tab order and no unintended mutation while navigating.
- Input: where per-key behavior is claimed, use actual native keyboard input and record raw text, caret/selection, focus and model effects after each relevant key. Whole-string Text/UIA assignment is not
  native typing evidence.
- Geometry: record window client width/height, selected list/detail split, tab-strip height, header/detail bounds, action bounds and scroll ranges at minimum and review sizes. Inspect long labels,
  empty/error banners, protected state and partial/uncertain-save notices.
- Counts and duplication: inspect every user subtab for absent header chips/manage link, and every role subtab for absent header member count. Verify the two count-bearing tabs remain visible and are not
  clipped by the longest count/label.
- Rendering: check text contrast, checked/unchecked/disabled/focus distinctions, semantic errors, readable source details and horizontal/vertical scrolling. A scaled screenshot does not replace actual
  native125 observations.

Capture sanitized screenshots with scenario ID, theme, physical scale and measured geometry. Automated control tests, native observations and attended Owner approval are separate evidence categories.

## Commands and evidence discipline

Follow [UI_LAB.md](UI_LAB.md) for locked restore and the currently implemented host launch. Select source-verified focused cases for the active task and confirm nonempty discovery. This example selects existing Security classes; inspect the discovered set before execution. Security host tests start Security directly. Run GUI commands sequentially.

```powershell
dotnet restore MyDmsVn.UiLab.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'UiLab locked restore failed.' }
dotnet build samples/MyDmsVn.Desktop.UiLab.Security/MyDmsVn.Desktop.UiLab.Security.csproj -c Debug -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'UiLab build failed.' }
$results = 'TestResults/security-ui-lab-' + [guid]::NewGuid().ToString('N')
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj `
  -c Debug -f net48 --no-restore --list-tests --filter 'FullyQualifiedName~Security'
if ($LASTEXITCODE -ne 0) { throw 'Security UI discovery failed.' }
# Inspect discovery: no missing mandatory cases and no empty selection.
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj `
  -c Debug -f net48 --no-restore --filter 'FullyQualifiedName~Security' `
  --logger 'trx;LogFileName=security-ui-lab.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'UiLab tests failed.' }
```

Existing `UiLabResetTests` and `UiLabToolbarReviewTests` are Sales preservation selectors, alongside `SalesScenarioBehaviorTests`; `~Sales` alone misses the first two. Include affected Sales/common checks when host/theme/lifecycle is shared. New case names or module-trait selectors must be verified against implemented source and discovered cases before use.
Record actual Facts/Theory rows, outcomes and skipped/missing cases; zero discovery or an unexplained skip cannot establish a pass. SEC-T06 retired the duplicate legacy control/view-model and mapped their tests to the candidate owners in the [post-gate evidence](../evidence/security/20261025-security-local-sql.md); `SecurityAdministrationFormTests` now exercises the candidate. Run only the affected scope under [TEST_POLICY](../TEST_POLICY.md).

No Lab result alone proves Server authorization, SQL transaction behavior, real Login, production wiring or package acceptance. The historical SEC-T05 Owner gate was approved on 2026-10-26; later Local/SQL results remain in the archived plan and post-gate evidence. The new UIL pilot/final decisions require their own exact-candidate observations and explicit approval.

## Gate evidence template

Copy this template into the plan-linked candidate evidence record. Do not maintain a second task-status ledger in this guide. Use `pending`/`not run` until actual evidence exists; record the Owner's decision in the sole [active plan](../plans/20261027-001-business-compact-and-independent-ui-labs.md).

| Evidence field | Value to record |
|---|---|
| Candidate identity | `[pending: source SHA, test SHA, vendor gitlinks and build configuration]` |
| Environment and presentation | `[pending: Windows/toolchain, bitness, physical scale, reported DPI, theme, typography, outer/client/content bounds and tool/journal arrangement]` |
| Source/composition proof | `[pending: actual candidate types; direct SEC launcher and no transient SAL graph; evaluated SQL-free Lab graph; existing production factory and absence of sample dependencies]` |
| Scope and scenarios | `[pending: active task, pilot or final surface inventory, selected SEC-L scenarios, fixture map, exact actions and gate-release order]` |
| Component map | `[pending: pinned toolkit controls per surface, native/custom exception capability reasons, command hierarchy and reviewed density]` |
| Automation | `[not run: commands, nonempty discovery, executed Facts/Theory rows, pass/fail/skip and sanitized TRX summary]` |
| Native/attended evidence | `[pending: scenario/theme/native100/native125 observations, keyboard/focus and screenshot references]` |
| Serial-save evidence | `[pending: sanitized requests, returned revision chain, partial/conflict/uncertain outcome and explicit reload/diff]` |
| Isolation and lifecycle | `[pending: no-SQL composition, redaction, cancellation/drain/disposal and timeout outcome]` |
| Findings and corrections | `[pending: original failures, fixes, affected reruns, remaining issues and explicit exceptions]` |
| Owner review | `[pending: Owner identity, review date/time, exact candidate, explicit approve/reject decision and scope]` |
| Next-work dependency | `[UIL-T04 approval before broad SEC changes; UIL-T06 approval before final product-host parity; original SEC-T05 and SEC-T06–T08 remain historical]` |

Approval requires an explicit dated Owner statement tied to the reviewed candidate and declared exceptions. A PR merge, document approval, screenshot acknowledgement or green fake test suite does not
substitute for that decision. If the reviewed UI changes before product-host verification or handoff, record the difference and obtain review of the affected behavior; do not substitute an unreviewed implementation under the approved candidate's evidence.
