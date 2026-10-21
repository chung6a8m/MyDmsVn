# BCF-T01 — lookup binding repair and focused boundary verification

**Date:** 2026-10-21 (Asia/Bangkok). **Outcome:** T01 COMPLETE / hosted Windows GREEN. Final affected selection: **172 discovered, 172 executed, 172 passed**, no skipped, failed, aborted or timed-out rows. T02 remains OPEN; native100/125 Light/Dark, attended observations, package and visual acceptance are not granted by this checkpoint.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Design](../../BUSINESS_COMPACT_REFINEMENT.md) · [Acceptance ledger](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [Immutable T00 RED proof](20261021-t00-native-typing.md)

## Source, environment and scope

- Existing isolated worktree/branch: `codex/bcf-t00-native-lookup-proof`; original `v1a-pro` checkout remains unchanged. T01 starts at `eb62f26` after the T00 documentation checkpoint; the underlying product baseline remains PR #40, `6040d7bcfca698fc9daba8268bb94e4454e8eacb`.
- Repair/test commit: `0089ef535e9eea7de781f79a12dd567bea2ac345`. Independent review correction: `58c8aa2996d404bcb0ae9d32d0d8a0d8e5c485e7`. The final run compiled the exact source/test tree later committed as `58c8aa2`, with build-parent HEAD `0089ef5`; only documentation changed afterwards. This distinction matters for build metadata.
- Production changes are confined to `GoodsReceiptControl.cs` and its existing `InlineEditing.cs` partial. Tests extend `GoodsReceiptLookupFixture.cs`/`NativeLookupTypingDriver.cs` and add `GoodsReceiptLookupBindingTests.cs`. Original T00 typing test assertions remain unchanged.
- Vendor pins unchanged: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`; Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`; SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Interactive Windows NT 10.0.26200.0; SDK 8.0.425, VSTest 17.11.1; Release/net48/x86 (native fixture asserts `IntPtr.Size == 4`). New lookup cases use Light, hosted `DeviceDpi=96`, form client 1500×850, virtualized work area 1920×1032. OS profile AppliedDPI is 120. **Hosted 96 is not proof of native100 or native125.** Existing selected regression anchors include their own theme rows, not a full three-control theme/scale matrix.
- No vendor upgrade, Desktop.Application behavior change, API/DTO/schema/permission change, SQL allocation, package build, actual Login, visual implementation or release acceptance.

## Behavior and proof

Ordinary binding updates options through pinned DataSource reconciliation and calls `SelectValue` only when the committed identity actually differs. Removing same-identity reselect and Text-only restoration preserves pending query, caret and selected range. The filter remains optional/active-only; headers preserve inactive historical selected labels. The shared warehouse ViewModel is retained, with separate filter/header roles.

Header search/selection callbacks recheck full editability (Posted, busy, access denial and uncertain-Post recovery). Filter callbacks reject busy state but remain usable for clean Posted inspection. Admitted New/select/Refresh boundaries cancel header lookup requests and query presentation; rejected navigation retains native pending input. Source completion ignoring cancellation cannot publish across a newer query, document or disposed/replaced session.

Command buttons retain native header focus during mouse-down and abandoned/rejected clicks. Save/Post then explicitly resolve pending header input through the existing native leave policy before reading model/dirty state; they do not synthesize Enter or commit an arbitrary highlighted result. A boundary generation fence rejects a queued Save if a newer document is admitted. Save tests inspect the actual outgoing request IDs; Post must not send old IDs while a new selection makes the draft dirty. Synthetic save rejection avoids any SQL dependency.

Filter commit yields only when the filter contains focus, so synchronous Local-style loading does not disable the native EDIT in the middle of Tab traversal. Review found that this yield originally left admission open: Select/Refresh/New could run before the deferred filter load. The final correction reserves admission before yielding, then transfers it synchronously to the list load; the reservation ends before normal asynchronous list processing. Existing search/paging/status behavior outside that short reservation remains intact. Three tests fail before the correction (extra Get/list request, or New clears receipt 41) and pass afterwards, also proving a subsequent selection is admitted after filter completion. This isolates ordering without requiring a held detail response or claiming an attended race reproduction.

The driver verifies HWND ancestry, native EDIT class, owning process/thread and focus. Text comes from individual `WM_CHAR` input; selection is observed with `EM_GETSEL`. Native keys cover F4/Up/Down/Enter/Escape/Tab/Shift+Tab, including non-first selection. Paste uses `WM_PASTE` with clipboard save/restore; clipboard contents are not logged. Popup/outside-click tests direct owned `WM_LBUTTONDOWN/UP` to verified controls with client coordinates. They prove message/selection behavior, **not physical cursor hit-testing or attended mouse interaction**. Owner deactivation uses real foreground activation with ownership checks. Whole-string Text and direct `SelectValue` remain labeled setup/binding/callback probes, not per-key evidence.

The fixture uses actual receipt/list/editor/lookup ViewModels and synthetic API sources, a real outer `Application.Run` loop and owned WindowsFormsSynchronizationContext. It checks UI context/thread and document mutation thread ownership, attaches all three grids to WinFormsTestGuard, and uses finite source/debounce barriers. Cleanup cancels and drains tracked source/debounce/control work, releases every staged response, and disposes controls/VMs/context. Uncertain cleanup poisons GUI admission. All **107 lookup rows** have clean drain/disposal records and `guard exceptions=0`; affected inline tests retain their existing bounded owned-subprocess cleanup checks.

## Final discovery and execution

Locked restore succeeded. The discovered names were reconciled against final TRX names (sorted `Compare-Object`: no difference).

| Selected class / method | Actual rows | Result |
|---|---:|---|
| GoodsReceiptLookupTypingTests (unchanged T00 cases; five per target) | 15 | PASS |
| GoodsReceiptLookupBindingTests | 92 | PASS |
| GoodsReceiptControlTests | 35 | PASS |
| GoodsReceiptInlineEditorTests | 15 | PASS |
| InventoryAccessibilityAcceptanceTests | 14 | PASS |
| LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace_and_relogin_rejects_the_old_workspace_version | 1 | PASS |
| Total | 172 | PASS, zero skip/timeout/abort |

The 92 binding rows cover: same identity (3), shared warehouse hosts (2), explicit/non-first selection (3), historical selection (2), late completion after New/select/Refresh/denial/disposal/session replacement (18), rejected dirty navigation (6), locked header callbacks and search (8), ignored cancellation (3), Escape open/closed (6), exact Tab/Shift+Tab (6), unmatched Tab (3), paste (3), popup mouse (3), outside click/deactivation (6), command mouse admission (4), denied recovery reload (2), busy filter/Posted inspection (1), filter admission ordering (3), Save/Post request boundaries (8), and Save across New (2). Test names and exact argument rows live in the committed class and local TRX; these totals are observations for this revision, not permanent gate counts.

```powershell
$bcfProject = 'tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj'
$bcfFilter = 'FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace'
dotnet restore $bcfProject --locked-mode
dotnet test $bcfProject -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $bcfFilter --list-tests
dotnet test $bcfProject -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $bcfFilter `
  --logger 'trx;LogFileName=focused.trx' --results-directory TestResults/bcf-t01-final-admission
```

Actual final command exited 0; TRX start `2026-10-21T11:07:45.6010943+07:00`, finish `2026-10-21T11:08:44.1872346+07:00`, reported test duration 54 s. x86 is asserted inside the selected native fixtures; VSTest launcher identifies itself as x64. No full Desktop/Session/SQL suite was run as the task gate.

| Local artifact | SHA-256 |
|---|---|
| MyDmsVn.Desktop.Tests.dll | `2EC87170C03CEDB1666324DE6F937D8929FAAF83AED1931598087D16004DFD88` |
| MyDmsVn.Desktop.WinForms.dll | `833D385E7A1716601C93F0BC46500D47A1F4A9F9A1E478C81FCF4126C66D0C44` |
| TestResults/bcf-t01-final-admission/focused.trx | `21D4A6AF6A50E3747BD67E7B91A8FBA939B23AE3EBE2C450D6A804ECA6FE066B` |

Raw TRX, stdout and binaries remain local/ignored; only sanitized synthetic observations are committed. No screenshots or proprietary SQL identifiers are added.

## Retained development attempts and review

Paths below are under ignored `TestResults/`. Invalid harness/setup attempts are retained and do not establish product RED or acceptance PASS.

| Attempt | Actual outcome / disposition |
|---|---|
| bcf-t01-baseline-red/typing.trx | 15 intended RED on unchanged T00 product baseline |
| bcf-t01-binding-red/binding.trx | 36 rows: 28 fail / 8 pass; includes 2 fixture drain failures and wrong popup rank assumption; not wholly valid product RED |
| bcf-t01-binding-red2 | 36: 28 fail / 8 pass; clean guards, rank/await helper issues still present |
| bcf-t01-expanded-red | 72: 51 fail / 21 pass; includes 6 virtualized cursor hit failures and wrongly awaited held header query |
| bcf-t01-corrected-red | 72: 42 fail / 30 pass; includes deactivation foreground setup failures and filter Tab failure |
| bcf-t01-filter-busy-red/filter.trx | 1 intended failure: disabled stale filter callback dispatches another list request |
| bcf-t01-first-green/lookup.trx | 88: 84 pass / 4 fail; 3 activation setup failures, 1 product filter Tab focus failure |
| bcf-t01-focus/focus.trx | 12: 11 pass / 1 fail; forward Tab returns to filter EDIT |
| bcf-t01-filter-focus-green/filter.trx | 13/13 pass after conditional yield |
| bcf-t01-search-guard-red/search.trx | 8 intended RED with only production search guards temporarily removed, then restored |
| bcf-t01-command-red | 4: 2 fail / 2 pass; initial no-line fixture insufficient to prove outgoing Save payload |
| bcf-t01-command-payload-red and command-admission-red | 4 and 10 rows respectively: native focus setup failures after loading a line; invalid RED |
| bcf-t01-lookup-green | 98: 88 pass / 10 fail; all 10 new command rows fail focus setup; fixed by re-focusing after load |
| bcf-t01-command-fence-red/commands.trx | 10 intended RED after focus fix, with only Save/Post fence calls temporarily removed; wrong Save IDs, premature Post and Save across New; production calls restored |
| bcf-t01-command-fence-green | 10/10 pass |
| bcf-t01-focused/focused.trx | 169/169 pass on source/test tree committed as 0089ef5; independent review then found deferred-filter admission race |
| bcf-t01-filter-admission-red/admission.trx | 3/3 intended RED on 0089ef5 plus new tests; no guard/drain failures |
| bcf-t01-filter-admission-green/admission.trx | Overbroad `~Tab` selector accidentally selected 44 rows: 43 pass, 1 existing packaged-login skip. Not a task gate; no package/SQL success claimed |
| bcf-t01-final/focused.trx | 172/172 pass; reservation guards then narrowed to the pre-load yield interval to preserve existing search behavior |
| bcf-t01-final-admission/focused.trx | Final source/test tree: 172/172 pass; authoritative T01 hosted result |

One fresh independent review examined `eb62f26..0089ef5` and the 169-row TRX. It reported one Important issue (filter admission race), no Critical or Minor findings. The issue was reproduced by the three RED rows and fixed in `58c8aa2`; the complete affected selection was rerun. No second reviewer was dispatched. The reviewer explicitly declined to judge T02 scales/themes, attended input, packaged Local flow and visual acceptance.

**Scope ruling:** the repository TEST_POLICY and explicit T01 plan select the smallest affected direct Desktop scope, overriding generic skill advice to run the full project suite. The additional Session-expiry anchor is in the Desktop project; the separate Session project/shared Session logic is unchanged. Cost if this scope is insufficient: unrelated integration regressions remain outside T01 evidence. Native message tests do not replace attended observations; all required T02 matrix rows and package-specific evidence remain OPEN. Operational release prerequisites remain unaccepted/unscheduled.
