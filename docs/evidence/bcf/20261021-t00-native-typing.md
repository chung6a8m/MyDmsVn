# BCF-T00 — native lookup typing RED checkpoint

**Date:** 2026-10-21 (Asia/Bangkok). **Outcome:** T00 COMPLETE as a test-only RED checkpoint. Product repair T01 is NOT STARTED; T02 remains OPEN. A01–A03 are failing baseline obligations, not acceptance PASS. No visual or production release acceptance is implied.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Design](../../BUSINESS_COMPACT_REFINEMENT.md) · [Acceptance ledger](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md)

## Scope and immutable baseline

- Worktree branch: `codex/bcf-t00-native-lookup-proof`, created through the native worktree tool from `v1a-pro@7fdc61b1b1c22600afb57bcf22d17ea2a2f29fca`.
- Product source is unchanged from PR #40 merge `6040d7bcfca698fc9daba8268bb94e4454e8eacb`: `git diff --name-only 6040d7b 7fdc61b -- src vendor` returned no paths, and T00 changes no product/vendor files.
- Test source: `845ca0a223e55d2742d2f7a2df6b1d8644f81335` adds driver/fixture/typing tests; `dbd079825f00fcf4b96294c90918cbaf712f7606` fixes test-owned message-loop/context admission and adds ownership assertions. The final run used exactly the latter source tree before its commit; only documentation changed afterwards.
- Pins unchanged: BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`; Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`; SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`.
- Smallest affected scope: existing `MyDmsVn.Desktop.Tests`, `net48`, x86, typing class only. No application/API/schema/vendor repair, SQL invocation, package build, Login, visual work or broader acceptance run.

## Test implementation and observations

`NativeLookupTypingDriver` verifies native EDIT class, HWND ancestry within the intended lookup, owning process/thread and current focus. It sends each character separately with `WM_CHAR`, reads native text and `EM_GETSEL`, and exposes native range selection plus the existing posted-key primitive. It never assigns an entire query through Text, WM_SETTEXT or UIA.

`GoodsReceiptLookupFixture` hosts the real GoodsReceiptControl and its actual list/editor/lookup ViewModels, with synthetic API/source data. The filter starts null, Warehouse ID 3 and Employee ID 5 are committed, and the Draft is clean. A shared warehouse ViewModel remains in use. Initial text, selection, pending flag, highlighted option, popup state, owned HWND and model/filter effects are recorded locally; unchanged IDs, clean state and null warehouse in every actual list request are asserted at each checkpoint.

The test owns a real outer `Application.Run` loop on the existing guarded STA. Its fixture explicitly owns/restores a WindowsFormsSynchronizationContext. Every wait verifies the owning UI thread/context and all observed document collection-mutation threads. The three lookup result grids are attached to WinFormsTestGuard for DataError capture. Deadlines remain finite; source/debounce barriers use TaskCompletionSource and bounded message pumping, with no timing sleeps. This is a private helper in the existing test class, not a new runner or process framework.

For cancellation proof, `g` and `u` create separate held debounce operations. The first token is verified canceled, its delay is released, and the captured preceding control operation is awaited through its final ApplyState while the latest operation is still held and no source request has been admitted. Only then is `i` sent. This isolates the canceled continuation from response rebinding.

For response proof, a delay is released, source admission is observed, its response is completed, and the captured control operation is drained through final ApplyState before the next key/checkpoint. Middle insertion, range retention and replacing selected `u` with `a` are separate Theory rows. Product assertion differences are collected after every step so later input remains observable on a broken baseline; native/fixture preconditions fail immediately and cannot be counted as intended RED.

## Final discovery and execution

Three Theory methods select **15 rows**, five per target. This is the observed T00 inventory, not a future fixed gate count.

| Target | First character + response | Canceled preceding completion | Middle insertion | Selected range | Range replacement | Total |
|---|---|---|---|---|---|---:|
| WarehouseFilterLookup | RED | RED | RED | RED | RED | 5 |
| WarehouseLookup | RED | RED | RED | RED | RED | 5 |
| EmployeeLookup | RED | RED | RED | RED | RED | 5 |

Final run: **15 selected / 15 executed / 15 intended failures / 0 passed / 0 skipped / 0 timeout / 0 aborted**. All 15 rows reached clean drain/disposal and `guard exceptions=0`; there were no focus/ownership/context/fixture precondition failures. `dotnet test` exited **1**, as required for this T00 checkpoint. The TRX window was 10:16:51–10:17:08 +07:00; reported test duration was approximately 12 seconds.

| Step / independent expected assertion | Filter actual | Warehouse/Employee header actual |
|---|---|---|
| First response: Text `g`, caret 1, range 0, pending true | Empty Text, caret 0, pending false; committed filter still null | Text `g`, caret 0, range 0, pending true; committed ID unchanged |
| Canceled old completion after `gu`: caret 2 | Empty Text, caret 0, pending false | Text `gu`, caret 0, pending true |
| Then native `i`: Text `gui`, caret 3 | Text `i`, caret 1 | Text `igu`, caret 1 |
| Insert `a` in `gui`, response: Text `gaui`, caret 2 | Empty Text, caret 0 | Text `gaui`, caret 0 |
| Select `u`, response: Text `gui`, range 1+1 | Empty Text, range 0+0 | Text `gui`, range 0+0 |
| Replace `u` with `a`, response: Text `gai`, caret 2 | Empty Text, caret 0 | Text `gai`, caret 0 |

All keys before the deliberately released boundary retained the correct literal prefixes and selection. Query typing did not change the committed Warehouse/Employee IDs, dirty the Draft or apply a warehouse filter. The exact broken `igu` is an observation of this run, never the test oracle.

## Environment and artifact attribution

- Interactive Windows NT `10.0.26200.0`, .NET Framework 4.8 target, SDK `8.0.425` selected under global.json, VSTest `17.11.1`; tests assert `IntPtr.Size == 4`.
- Light theme; hosted Form.DeviceDpi **96**, client **1500x850**, reported work area **1920x1032**. The Windows profile `AppliedDPI` registry value was **120**. These different measurements are retained: this run is not credited as the native100 or native125 T02 matrix. No OS scaling change or simulated DPI was performed.
- This is hosted native EDIT/WM_CHAR proof, not physical attended typing, packaged Login or a package screenshot comparison. Dark, native100/native125 gate qualification, attended checks and all A04–A23 acceptance remain uncredited.

| Local artifact | SHA-256 |
|---|---|
| `MyDmsVn.Desktop.Tests.dll` | `22d9358a9dbf9b6c657bd1bc71592a569e537f59bb1174bbcc9539fc4f21ca13` |
| `MyDmsVn.Desktop.WinForms.dll` | `1b081a4ca9b88d5f4887e13c313543db2a8ed6c0259d6e73bd6486d023d79f5b` |
| `TestResults/bcf-t00-loop-guard/typing.trx` | `65f03badb94315a7a84c82ef8f565547d93dc24b247c690c39be542046868cda` |

No package was produced, so no package hash is claimed. Raw TRX/logs stay local and ignored; the tables above contain synthetic, sanitized observations only.

## Commands actually run

Locked restore succeeded. The existing baseline Fact `GoodsReceiptControlTests.Activation_and_selection_render_master_detail_lookups_total_and_read_only_state` ran before new tests and passed **1/1**, with local TRX `TestResults/bcf-t00-baseline/baseline.trx`. Discovery with `--list-tests` selected the 15 typing rows, with no binding-test class invented for T01.

Final execution from the worktree root:

```powershell
dotnet restore tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj --locked-mode
dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj `
  -c Release -f net48 --no-restore -p:PlatformTarget=x86 `
  --filter 'FullyQualifiedName~GoodsReceiptLookupTypingTests' `
  --logger 'trx;LogFileName=typing.trx' `
  --results-directory TestResults/bcf-t00-loop-guard
```

The execution output was redirected to a local temporary log and its actual exit code captured. TRX counters, all per-row ErrorInfo and StdOut, cleanup markers and selected target counts were inspected. Builds used current source; no unverified `--no-build` output was reused. Final diff/encoding/local-link checks are documentation-only and do not alter the runtime evidence.

## Retained attempts and review correction

Earlier failures are retained locally and do not replace the final result:

| Local attempt | Actual result | Disposition |
|---|---|---|
| `bcf-t00-trial1` (temporary build log) | Fixture used incorrect draft-request interface signatures; compile failed, no TRX | Test-authoring error corrected against FeatureApiClients; never counted RED |
| `bcf-t00-trial2/typing.trx` | 15 intended product failures with clean guard markers | Diagnostic source before review; insufficient UI-context ownership guarantee for final attribution |
| `bcf-t00-red/typing.trx`, test `845ca0a` | 14 intended product failures; one filter/insert row caught OverlappingCellException in queued line-grid Rebuild | Invalid full proof; retained, no acceptance credit |
| `bcf-t00-context/typing.trx` | 15 product failures after explicit context installation | Diagnostic run before context/collection ownership assertions |
| `bcf-t00-owned-context/typing.trx` | 15 setup/context failures: DoEvents left System.Threading.SynchronizationContext | Invalid RED; established that explicit installation alone was insufficient without a persistent outer loop |
| `bcf-t00-loop/typing.trx` | 15 guard-initialization failures because a startup HWND preceded the guard | Invalid RED; guard moved before any HWND creation |
| `bcf-t00-loop-guard/typing.trx`, exact source later committed as `dbd0798` | 15 intended product failures, all ownership/guard/drain assertions satisfied | Final T00 RED checkpoint |

A fresh read-only reviewer inspected `7fdc61b..845ca0a` under the executing-plans/requesting-code-review workflow: one Important finding, no Critical findings. The finding was insufficient UI SynchronizationContext ownership. The correction owns both the outer message loop and context, adds explicit UI/collection assertions, and was verified by the failed ownership assertion followed by the final clean product-RED run. The reviewer did not run GUI or certify later native/theme/package scope; no second review was claimed. No minor findings were deferred.

**Ruling:** the task's explicit T00 RED contract governs completion instead of the generic implementation workflow's all-green completion rule. Keeping the product assertions failing is intentional and reviewable; the cost is that the typing selector remains red until T01 and must not be advertised as a passing acceptance gate.

## Cause and handoff to T01

Filter `ApplyState` unconditionally selects `_warehouseFilterId` (null in this proof), clearing pending query presentation. Header `BindEditorLookup` replaces options, reselects the unchanged committed identity and restores only Text, resetting native caret/range to 0. A canceled DebouncedAsyncAction completes normally; GoodsReceiptControl.CompleteOperationAsync then invokes ApplyState even while the latest debounce remains pending. The independent response and canceled-completion cases expose both paths without a vendor change.

The pinned vendor DataSource reconciliation already leaves pending text intact; inline Product binding is the existing application reference. T01 should repair ordinary same-identity application rebinding and add its prescribed boundaries, preserving historical options, shared warehouse roles and explicit cancellation at admitted document boundaries. T00 does not implement that repair.

All held delay/source completions and captured control operations were released, canceled/drained and observed before fixture/control/Form/ViewModel disposal; no SQL allocation, package/probe subprocess or unresolved GUI resource was created. T02 remains OPEN until its independent native100/125 Light/Dark and attended evidence passes; visual implementation is not admitted.
