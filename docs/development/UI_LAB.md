# Sales UI Lab — developer runbook

The Sales UI Lab is a separate net48 WinForms development host for the existing `SalesReceiptControl`. It is intended for inspecting list/editor input, session and response races without a SQL instance or real Login. The Lab does not run the production Local adapter or prove Server authorization, SQL valuation, posting atomicity or a release package. The [current plan](../plans/20261024-001-a0-a1-sales-ui-lab.md) owns implementation state and candidate evidence; this page owns repeatable use and commands.

## Prerequisites and source boundaries

Use an interactive Windows desktop with .NET Framework 4.8 targeting/runtime support, the SDK pinned by [`global.json`](../../global.json), and the repository's pinned submodules. Do not update submodules to remote HEAD. The source projects are:

- [`MyDmsVn.Desktop.UiLab`](../../samples/MyDmsVn.Desktop.UiLab/MyDmsVn.Desktop.UiLab.csproj): the small STA executable, scenario picker, Reset, workspace close and diagnostics.
- [`MyDmsVn.Desktop.UiLab.Scenarios`](../../samples/MyDmsVn.Desktop.UiLab.Scenarios/MyDmsVn.Desktop.UiLab.Scenarios.csproj): fixtures, fake API ports, session and controlled response gates shared by the host and tests. It does not use WinForms.
- [`MyDmsVn.Desktop.Ui.Tests`](../../tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj): focused scenario and native WinForms checks.
- [`MyDmsVn.UiLab.sln`](../../MyDmsVn.UiLab.sln): the small solution for Visual Studio and locked restore.

The Lab constructs the real [`SalesReceiptControl`](../../src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs) with its seven API/session/dispatcher/preview/lookup/permission/product dependencies. The existing [`WorkspaceCloseCoordinator`](../../src/MyDmsVn.Desktop.WinForms/WorkspaceCloseCoordinator.cs) owns ordinary dirty-close admission. A real Windows Forms synchronization context and message loop must stay active during async operations and reset drain. The Lab is a developer executable; do not add fake service switches to Desktop.App.

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

Open `MyDmsVn.UiLab.sln` in Visual Studio 2022, set `MyDmsVn.Desktop.UiLab` as startup project and start Debug/net48 with F5. The exact executable path and bitness depend on the project output settings; inspect the successful build output rather than assuming a bin path. Use the Lab toolbar's scenario selector and Reset to create a fresh workspace graph. `Close workspace` uses the close coordinator; `Replace session` changes the fake session version. `Fail next Save` arms one server field error after local validation. In S03, `Arm lookup A/B` queues two Product lookup responses; type search A then B, press `Complete B` and finally `Complete A` to observe a deliberately late A. The access dropdown offers `Allow all`, `Deny Read`, `Deny Write` and `Deny Post`; choose one and press `Apply access` to refresh the control through the fake permission client. For a busy presentation check, press `Hold next Reload`, then the control's Reload button; press `Release Reload` to return the current fixture sale. These access and held-response controls exercise UI admission only. The lower journal panel shows operation sequence/names; the fake journal retains request objects for test assertions. Keep the UI responsive while responses are held; reset cancels owned gates before draining.

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

## Reset, close and diagnostics

Reset is a fixture lifetime boundary: it cancels the old graph, releases or cancels owned gates, drains tracked operations with a finite wait while the message loop runs, disposes controls/subscriptions on the UI thread, and then creates a new graph. Ordinary Close must use the close coordinator and can be rejected for dirty, busy or uncertain-Post state. Reset is not a substitute for S05 Close evidence. If a drain times out, the host disables new scenario/reset commands for that instance. Keep the failure visible and stop using that host instance; do not claim the next scenario starts cleanly.

The fake call journal records request and response operations and stores request objects for assertions; the host panel displays operation sequence and name only. Reproduce a failure with the scenario ID, action order, gate-release order, raw text/caret/focus and relevant journal entries. For request contents, inspect a focused test assertion or debugger rather than assuming the host panel displays them. Do not copy credentials, raw proprietary TRX or unreviewed screenshots into the repository.

## Focused tests and evidence

Restore first; run the new project as a whole once for the implementation candidate, with a fresh TRX directory:

```powershell
$results = 'TestResults/ui-lab-' + [guid]::NewGuid().ToString('N')
dotnet test tests/MyDmsVn.Desktop.Ui.Tests/MyDmsVn.Desktop.Ui.Tests.csproj `
  -c Debug -f net48 --no-restore `
  --logger 'trx;LogFileName=ui-lab.trx' --results-directory $results
if ($LASTEXITCODE -ne 0) { throw 'UiLab tests failed.' }
```

Existing `Desktop.Tests` and `Desktop.Session.Tests` cases stay in their original owners; no legacy test has been retired or moved. `Ui.Tests` adds scenario/native cases and links the small `NativeLookupTypingDriver` helper source. Its `NativeDriverIndependenceTests` checks that the linked driver can send a native key without the Goods Receipt test fixture. This helper correction does not transfer the old driver behavior or prove replacement of any legacy test.

For later edits, select a test class/method only after checking the current source and nonempty `--list-tests` discovery. Native checks require an interactive desktop, serialized STA execution, exception/DataError capture, finite waits and deterministic disposal. Per-key date/lookup proof must inspect raw text, caret or selection and focus after actual keyboard input; whole-string assignment is insufficient. Run one GUI command at a time. An in-process STA timeout cannot kill a noncooperative UI thread; use the existing bounded GUI-owned subprocess where a fatal/hang probe requires it.

Record source/test/gitlink revisions, toolchain, bitness and DPI where relevant; exact selected/discovered/executed Facts/Theory rows, pass/fail/skip counts and TRX path; scenario observations, cleanup, evaluated dependency graph and remaining limitations. A missing/zero/skipped mandatory case is not PASS. Choose additional regression only for shared behavior actually changed, following [TEST_POLICY](../TEST_POLICY.md), [TEST_STRATEGY](../TEST_STRATEGY.md) and [tests/README](../../tests/README.md). Historical Sales/GUI evidence belongs to its original candidate. The Lab does not provide new SQL, actual Login, package or production acceptance.
