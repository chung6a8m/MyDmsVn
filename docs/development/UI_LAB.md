# UI Lab — developer runbook

The UI Lab is a separate net48 WinForms development host for the existing `SalesReceiptControl` and the Security workspace candidate. It is intended for inspecting input, session and response races without a SQL instance or real Login. The Lab does not run the production Local adapter or prove Server authorization, SQL behavior or a release package. The [archived A0/A1 record](../archive/20261025-pre-next-roadmap/plans/20261024-001-a0-a1-sales-ui-lab.md) retains historical Sales evidence; this page owns repeatable use and commands.

## Current host and selected independent-Lab target

At the inspected `main@8de4451573e8a9a033d7e5a0b07e5f22ef3018b9` baseline, one executable starts Sales and provides a Sales/Security module picker. The build/start instructions below describe that implemented host. The active [UIL roadmap](../plans/20261027-001-business-compact-and-independent-ui-labs.md) selects two direct launchers, `MyDmsVn.Desktop.UiLab.Sales` and `.Security`, with separate module-owned runtime graphs and shared neutral infrastructure. Those launchers are **not implemented by this documentation PR**.

[Independent UI Labs](../design/INDEPENDENT_UI_LABS.md) defines project/lifecycle boundaries. [Business Compact](../design/BUSINESS_COMPACT.md) requires the same product theme/bootstrap, typography, measured workspace content bounds and theme switching in both Labs, including owned dialogs. The current Lab's default-theme toggle does not meet that target merely because it hosts the production controls. UIL-T01/T02 implement and verify the target, then replace the current commands and module-selection instructions in this runbook. Keep one scenario assembly and one serialized Ui.Tests assembly initially.

## Security candidate boundary

The current host offers a **Sales / Security** module picker. Security constructs the candidate `SecurityWorkspaceControl` and `SecurityWorkspaceViewModel` from the production source assemblies with deterministic fake ports. The [Security UI specification](../design/SECURITY_ADMINISTRATION_UI.md) and [review guide](SECURITY_UI_LAB_REVIEW.md) define behavior and exercises; the [active UIL plan](../plans/20261027-001-business-compact-and-independent-ui-labs.md) owns new task/gate state. Scenario IDs alone do not establish that their exercises passed.

The Lab candidate remains SQL-free. The Owner approved its reviewed source at SEC-T05 on 2026-10-26, and the production Security factory now creates the same `SecurityWorkspaceControl` through session-bound Local ports. SEC-T06–SEC-T08 results and remaining limits are recorded in the [archived plan](../archive/20261027-main-roadmap-reset/plans/20261025-002-security-administration-ui-lab-first.md) and [post-gate evidence](../evidence/security/20261025-security-local-sql.md). Historical Sales Lab passes do not approve Security, and historical SEC approval does not approve a later Business Compact candidate.

## Prerequisites and source boundaries

Use an interactive Windows desktop with .NET Framework 4.8 targeting/runtime support, the SDK pinned by [`global.json`](../../global.json), and the repository's pinned submodules. Do not update submodules to remote HEAD. The source projects are:

- [`MyDmsVn.Desktop.UiLab`](../../samples/MyDmsVn.Desktop.UiLab/MyDmsVn.Desktop.UiLab.csproj): the small STA executable, scenario picker, Reset, workspace close and diagnostics.
- [`MyDmsVn.Desktop.UiLab.Scenarios`](../../samples/MyDmsVn.Desktop.UiLab.Scenarios/MyDmsVn.Desktop.UiLab.Scenarios.csproj): fixtures, fake API ports, session and controlled response gates shared by the host and tests. It does not use WinForms.
- [`MyDmsVn.Desktop.Ui.Tests`](../../tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj): focused scenario and native WinForms checks.
- [`MyDmsVn.UiLab.sln`](../../MyDmsVn.UiLab.sln): the small solution for Visual Studio and locked restore.

The Lab constructs the real [`SalesReceiptControl`](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs) or candidate [`SecurityWorkspaceControl`](../../src/MyDmsVn.Desktop.WinForms/Features/Security/SecurityWorkspaceControl.cs) with fake dependencies. The existing [`WorkspaceCloseCoordinator`](../../src/MyDmsVn.Desktop.WinForms/WorkspaceCloseCoordinator.cs) owns ordinary dirty-close admission. A real Windows Forms synchronization context and message loop must stay active during async operations and reset drain. The Lab is a developer executable; do not add fake service switches to Desktop.App.

## Build and open

From the repository root in PowerShell, check the exact checkout and gitlinks, then restore the small solution in locked mode. A fresh checkout must create no private SQL or Login configuration:

```powershell
git rev-parse HEAD
git submodule status --recursive
dotnet --version

dotnet restore MyDmsVn.UiLab.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'UiLab locked restore failed.' }

dotnet build samples/MyDmsVn.Desktop.UiLab/MyDmsVn.Desktop.UiLab.csproj `
  -c Debug -f net48 --no-restore
if ($LASTEXITCODE -ne 0) { throw 'UiLab build failed.' }
```

Open `MyDmsVn.UiLab.sln` in Visual Studio 2022, set `MyDmsVn.Desktop.UiLab` as startup project and start Debug/net48 with F5. The exact executable path and bitness depend on the project output settings; inspect the successful build output rather than assuming a bin path. Use the Lab toolbar's scenario selector and Reset to create a fresh workspace graph. `Close workspace` uses the close coordinator and retains pending editor focus until its admission decision; `Replace session` changes the fake session version. `Fail next Save` arms one server field error after local validation. In S03, `Arm lookup A/B` queues two Product lookup responses; type search A then B, press `Complete B` and finally `Complete A` to observe a deliberately late A. The access dropdown offers `Allow all`, `Deny Read`, `Deny Write` and `Deny Post`; choose one and press `Apply access` to refresh the control through the fake permission client. Apply is unavailable while the workspace is busy; release the operation, then apply. For a busy presentation check, press `Hold next Reload`, then the control's Reload button; press `Release Reload` to return the current fixture sale. The lookup race and held Reload can each be armed once per workspace; use Reset to start a new gate sequence. These access and held-response controls exercise UI admission only. The lower journal panel shows operation sequence/names; the fake journal retains request objects for test assertions. Keep the UI responsive while responses are held; reset cancels owned gates before draining.

## Scenarios

Each scenario starts from a deterministic fixture and a fresh session/workspace lifetime. S01, S03–S06 seed sale ID `41` with two lines for Product `11`; S02 opens a new Draft. The fixed reference date is 2026-10-22. Use the host for ordinary input, presentation, the S03 lookup race, the next-Save server error, access variants and a held Reload. The S06 fake automatically queues one failed recovery Get after the unknown Post result; the following Get returns the Posted sale. Native tests verify exact dispatch order and count. Inspect the journal/status alongside the visible UI. Scenario IDs describe behavior; a menu option by itself is not a passing check.

| ID | Exercise | Expected observation |
|---|---|---|
| S01 Draft | Open a Draft with two lines using the same ProductId and distinct line IDs. Change quantity/note, then Save. | Independent rows remain; Save journal contains the actual edits; confirmed response clears dirty state and retains coherent IDs/line order. |
| S02 Validation | Enter invalid raw numeric/date input and try Save/Post. Repair one field; press `Fail next Save` before a locally valid Save to receive a server field error. | Invalid raw text remains, no mutation is dispatched for local errors, repaired error clears selectively, and the server field error is mapped to the corresponding control. |
| S03 Lookup race | Press `Arm lookup A/B`; type Product search A then B; press `Complete B`, then `Complete A`. Use `Replace session` for the session variant. Native tests inspect the exact cancellation and response order. | Stale A cannot overwrite committed identity, raw text, unit or follow-on state; stale session cannot mutate the new workspace. |
| S04 Date caret | Use the fixed fixture reference date in Sale/From/To. Type one key at a time, move day/month/year segments and use F4/Escape. | Raw text, caret/range and focus stay in the intended segment; filter/query changes only at the relevant Apply/admission boundary. |
| S05 Dirty close | Edit a Draft with pending raw input. Try Back/`Close workspace`, then choose Cancel, No (Discard) or Yes (Save) in the prompt. For failed Save, first arm `Fail next Save`. | Rejected close preserves edits; the actual close coordinator admits only the allowed outcome; failed Save keeps the workspace open. |
| S06 Unknown Post | Post the seeded Draft once. Press Reload twice: the first recovery Get is configured to fail, and the second returns Posted for the same sale ID. | Recovery blocks unsafe commands; Post journal count remains one; only authoritative reload establishes Posted state. |

For denied Read/Write/Post variants, choose the matching dropdown item and press `Apply access`; choose `Allow all` and apply it to restore the fake grants. For busy presentation, arm `Hold next Reload`, start Reload in the Sales control, then press `Release Reload` after inspecting the held state. These fake responses exercise UI presentation/admission and do not establish Server permission enforcement. Empty-list coverage uses fixture/test setup rather than a dedicated toolbar choice. Preview arithmetic is a published small-demo range and should visibly recalculate quantity/price/gift totals; SQL moving-average cost remains outside the Lab.

## Security candidate exercises

Choose **Security** in the module picker, then a `SEC_L01`–`SEC_L12` fixture and Reset. The same toolbar offers Light/Dark, Reset, Close workspace and Replace session. Sales-specific lookup, Save-failure and permission controls apply only to Sales. The Security workspace opens **Người dùng**, **Vai trò** and read-only **Danh mục quyền**. User details contain **Thông tin**, **Vai trò (n)**, **Quyền hiệu lực** and **Quyền riêng (n)**; role details contain **Quyền của vai trò** and read-only **Thành viên (n)**. Use user-owned membership actions in **Vai trò (n)**. Role grant changes remain a draft until **Lưu quyền vai trò**; a user's direct choice remains a draft until **Lưu quyền này**. The JSON import buttons in those two editors preview the selected target and additions, then request explicit confirmation before Apply.

The Security fixtures publish fixed users `101` (active), `102` (inactive) and `103` (protected), roles `201` (active), `202` (active) and `203` (inactive), plus Active and Deprecated keys. `SEC_L02` initially denies Users; `SEC_L08` queues one successful mutation followed by a conflict. Other IDs share the populated fixture and expose fake gates and errors for controlled exercises. To reproduce a particular [SEC-L scenario](SECURITY_UI_LAB_REVIEW.md#scenario-matrix), follow its action order and configure the required fake responses through a focused test or debugger; selecting an ID alone does not run or pass that scenario. The lower journal displays sanitized operation names and order. It excludes password text and raw import JSON.

On a partial, conflicting or uncertain Save, use **Tải lại để đối chiếu** and explicitly review the new diff. A current value matching the intended value does not prove an earlier uncertain write committed. Close remains blocked while a write is busy or recovery is required. Reset cancels and drains the old graph before replacing it; a timed-out drain poisons the host instance. Security Lab observations do not prove Local admission, SQL behavior, actual Login or production acceptance. Record physical monitor scale separately from WinForms `DeviceDpi` and identify any native input or attended checks that were unavailable.

## Reset, close and diagnostics

Reset is a fixture lifetime boundary: it cancels the old graph, releases or cancels owned gates, drains tracked operations with a finite wait while the message loop runs, disposes controls/subscriptions on the UI thread, and then creates a new graph. Ordinary Close must use the close coordinator and can be rejected for dirty, busy or uncertain-Post state. Reset is not a substitute for S05 Close evidence. If a drain times out, the host disables new scenario/reset commands for that instance. Keep the failure visible and stop using that host instance; do not claim the next scenario starts cleanly.

The fake call journal records request and response operations and stores request objects for assertions; the host panel displays operation sequence and name only. Reproduce a failure with the scenario ID, action order, gate-release order, raw text/caret/focus and relevant journal entries. For request contents, inspect a focused test assertion or debugger rather than assuming the host panel displays them. Do not copy credentials, raw proprietary TRX or unreviewed screenshots into the repository.

## Focused tests and evidence

Restore first and select the changed behavior under the active task and test policy. The following is a focused Security example using existing source names, with discovery and a fresh TRX directory. It is not a recorded result; inspect the discovered cases before executing. Until UIL-T02 changes their setup, some selected Security host tests still instantiate Sales first.

```powershell
$results = 'TestResults/ui-lab-' + [guid]::NewGuid().ToString('N')
$uiFilter = 'FullyQualifiedName~Security'
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj `
  -c Debug -f net48 --no-restore --list-tests --filter $uiFilter
if ($LASTEXITCODE -ne 0) { throw 'UiLab test discovery failed.' }
# Inspect discovery: no missing mandatory cases and no empty selection.
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj `
  -c Debug -f net48 --no-restore --filter $uiFilter `
  --logger 'trx;LogFileName=ui-lab.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'UiLab tests failed.' }
```

Existing `Desktop.Tests` and `Desktop.Session.Tests` cases stay in their original owners; no legacy test has been retired or moved. `Ui.Tests` adds scenario/native cases and links the small `NativeLookupTypingDriver` helper source. Its `NativeDriverIndependenceTests` checks that the linked driver can send a native key without the Goods Receipt test fixture. This helper correction does not transfer the old driver behavior or prove replacement of any legacy test.

For Sales Lab behavior, the baseline class set is `SalesScenarioBehaviorTests`, `UiLabResetTests` and `UiLabToolbarReviewTests`; `FullyQualifiedName~Sales` alone misses the latter two. `NativeDriverIndependenceTests` belongs to shared helper verification. Select affected classes/methods and verify discovery; UIL-T02 may introduce explicit module grouping/traits, but no such new filter is assumed until implemented.

For later edits, select a test class/method only after checking the current source and nonempty `--list-tests` discovery. Native checks require an interactive desktop, serialized STA execution, exception/DataError capture, finite waits and deterministic disposal. Per-key date/lookup proof must inspect raw text, caret or selection and focus after actual keyboard input; whole-string assignment is insufficient. Run one GUI command at a time. An in-process STA timeout cannot kill a noncooperative UI thread; use the existing bounded GUI-owned subprocess where a fatal/hang probe requires it.

Record source/test/gitlink revisions, toolchain, bitness and DPI where relevant; exact selected/discovered/executed Facts/Theory rows, pass/fail/skip counts and TRX path; scenario observations, cleanup, evaluated dependency graph and remaining limitations. A missing/zero/skipped mandatory case is not PASS. Choose additional regression only for shared behavior actually changed, following [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md). Historical Sales/GUI evidence belongs to its original candidate. The Lab does not provide new SQL, actual Login, package or production acceptance.
