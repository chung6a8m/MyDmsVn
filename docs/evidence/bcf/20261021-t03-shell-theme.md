# BCF-T03 — shared shell and theme refinement

**Status: IMPLEMENTED / verification partial.** Native125 cannot realize the required 1680×900 logical client on the available 1920×1080 monitor. Windows clamps it to 1540×845; the Owner confirms no larger resolution is available. Both Light/Dark cases remain OPEN. This is not a waiver, full visual parity, T04 admission or BCF/Owner/release acceptance.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [Capture index](20261021-t03-shell-captures/README.md) · [Frozen T02 gate](20261021-t02-lookup-gate.md)

## Source and scope

Entering source is `3dac275ce4e6a257ad2363fdd1c0cb7a1c26400a`, on isolated branch `codex/bcf-t00-native-lookup-proof`. Product/test checkpoint is `a8a535cb92ba47563e221939ff2046c29a8bfbd2`. The broad native125 run precedes this commit: review subsequently changes only a test name and normalizes C# BOM/CRLF; no product behavior is changed. The renamed frame test is rebuilt and rerun at native125. Native100 builds the checkpoint with documentation-only working changes. Artifact distinctions are retained below. T02's `67fff29` package and its evidence remain frozen and do not certify this changed shell.

The app-owned navigation now provides a cube brand, module icons, Catalog/Inventory/System groups, selected/focus states, 224/64 logical widths, full accessible names/tooltips, current user and compact native theme/sign-out buttons. Existing module tags, permission admission, workspace/session invalidation and disposal contracts are retained. Shared immutable public vendor theme tokens provide Light and blue-gray Graphite surfaces. Appearance traverses application-owned TabPages and late-added content, while private composite/editor children remain vendor-owned. An app subclass complements only the pinned native tab header gap/frame paint; it retains the native selection, pages, keyboard and overflow implementation.

No API/schema/permission/business/runtime or vendor changes. Pins remain BootstrapSourceGrid `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f`, Bootstrap5WinFormUI `aba102e33c48937fd92468791c287afda0a59e77`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Target remains net48/x86, interactive Windows NT 10.0.26200.0, SDK 8.0.425/VSTest 17.11.1. GUI owners run sequentially with guarded STA, bounded child processes and verified drain. No SQL is selected or allocated for this hosted shell scope. No T03 shipped package/actual Login or attended package claim is made.

## Reconciled verification

The focused selector is:

```text
FullyQualifiedName~BusinessCompactRefinementTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~BusinessCompactLayoutTests|FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace
```

Use ordinary `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter <selector> --logger trx --results-directory <context>`. The matching `--list-tests` discovery is retained locally. Declare `MYDMSVN_BCF_EXPECTED_SCALE=100|125`, `MYDMSVN_BCI_EXPECTED_SCALE` to the same scale and `MYDMSVN_BCF_THEME=Light|Dark`. The additional Dark selector is the existing typing, binding and inline-editor classes. Each context has its own actual TRX; raw outputs stay ignored.

| Actual run | Selected/executed | Outcome |
|---|---|---|
| Native125 focused, Light declaration plus parameterized Light/Dark shell cases | 226 | 224 PASS, 2 FAIL: required1680×900 client is OS-clamped; zero skipped |
| Native125 lookup/inline Dark | 122 | 122 PASS; zero skipped |
| Native100 focused | 226 | 226 PASS; zero skipped |
| Native100 lookup/inline Dark | 122 | 122 PASS; zero skipped |

The 13 refinement invocations cover grouped/icon/session composition in both themes, actual native Space activation, all-three pending lookup text/range/focus/committed state across theme/resize, late-owned-page styling, native tab edges and six viewport/theme captures. Existing tests cover affected shell/session, accessibility, receipt edit admission and inline editor boundaries. The caption assertion measures logical padding; it does not establish complete physical text rendering at native125 (see the recorded limit below).

## Capture provenance and comparison limits

The existing GUI-owned probe gains capture modes; a separate net48/x86 PerMonitorV2 process verifies target PID/HWND, actual effective monitor DPI and native client bounds. The hosted target remains window-DPI96 and OS-virtualized at monitor120/native125. `PrintWindow` paints in target-window units: its bitmap is therefore allocated at logical client size, without physical-sized black padding, resizing or simulated scaling. Independent physical `CopyFromScreen` captures at1200×760 verify actual composition; the probe requires the entire client in the real work area and rejects overlapping foreign-process windows. Same-process popup layers are admitted and counted. This is a guarded fixture screenshot, not proof of actual package Login.

Matched data uses the real CatalogControl and ProductCatalogViewModel with 18 synthetic English products, selected SP001, page1 and identical scroll origin. Other tabs are explicitly placeholders; the catalog search/grid/editor composition remains the pre-T04 implementation. Compare the shared shell against unchanged references 01/03 only. The navy sidebar, icon/group hierarchy, bottom session area, compact tabs and Graphite app-owned content are present. The reference Products toolbar/results/inspector hierarchy is still T04; receipt List/Detail hierarchy is still T05.

Physical Graphite inspection retains precise native limits: the Win32 DateTimePicker text field/drop arrow, plain status ComboBox, native lookup accessory squares FlowLayoutPanel/SourceGrid scrollbars, and horizontal native tab-edge strips still use light platform chrome. Public BackColor/ForeColor assignments do not recolor these native surfaces; private child replacement/vendor patch is not introduced. Long native tab captions can be ellipsized (Goods Receipts at native125), with the existing native tab selection retained. These visible limits prevent a blanket parity claim. Reference-driven integrated disposition remains T06.

Native125 at 1680×900 Light/Dark is missing, not substituted with a clamped capture or a resized native100 image. Native125 at 1200×760 and944×600 pass. Native100 at 1680×900,1200×760 and944×600 all pass in Light/Dark with measured monitor/window96 DPI. The available monitor/resolution is an external evidence blocker.

## Retained development failures

Invalid setup trials (xUnit2031 analyzer, missing fake LookupAsync, inaccessible SizeFromClientSize and nullable tooltip) are compile failures, not product RED evidence. First shell tests expose missing module icons; unsupported TabControl.BackColor expectations were replaced by rendered-pixel checks. The first physical-sized PrintWindow buffer produced black padding and a false three-row green: **invalid evidence**, never credited. Corrected allocation yields three genuine white-header failures before the app-owned paint complement.

Dedicated late-page styling fails before owned-TabPage traversal and passes after it. A native tab-edge pixel case fails before the narrow bevel complement and passes after it. The navigation contrast test finds selected Light text at4.37895:1; white selected text raises it above4.5:1. Earlier screenshot attempts with white catalog panels remain ignored under `TestResults/bcf-t03-capture-attempts/before-page-style`. Intermediate 22/22,24/24 and4/4 focused passes are development checks, not substitutes for the final226-row context or the two missing sizes. The failed native125 viewport runs remain recorded rather than reclassified as passing.

## Review and handoff

Independent read-only review found no Critical or verified business/lifecycle regression. Its Important finding was an evidence overclaim: the tab test name implied full readability while it measures only logical padding and a complemented corner pixel. Rename to `Graphite_tab_frame_complement_preserves_logical_caption_padding` and the explicit caption/bright-strip limitations correct that claim; its native125 rerun passes 1/1. The visible native issues themselves remain OPEN. The Minor new-file BOM/CRLF finding is fixed for all changed C# files. No additional review round is used for this test-name/encoding correction. T03 remains open at the required native125 wide capture boundary and the documented native chrome limitations. T04–T06 are not started; all shared A14/A15/A18/A20/A21 obligations remain partial until their full downstream evidence and Owner disposition exist. Operational release prerequisites remain unaccepted/unscheduled.

## Artifact and row reconciliation

| Build boundary | Test DLL SHA-256 | Test-host WinForms DLL SHA-256 |
|---|---|---|
| Native125 broad226 and Dark122; pre-review-name/encoding normalization | `78C158E10EEEF4163621EBD03CF5D4F57666B4B112657869BD51EA71267CE071` | `2E3348934F09160A999AE1CCA5B53A2F50EE65FDB4697EAFCFF9361B496B8D4A` |
| Renamed frame/native125 rerun1; normalized source immediately before a8a535c | `ACECC2FDFF1E707BCE9E3AC73FE0F9B30E621FDFAB2925CD0A6E4815E33BDB55` | `D1C9997475AD6B6382C6A7CE7C58097C628F0CBD2F99394D7C5BD6B18D23A77C` |
| Native100 checkpoint a8a535c, documentation-only working diff | `1220399FDACFD0960F21E4B4085CDE39283DBF3E3FC26A2CC278BA572143E124` | `DBF803A91B5D3DBE5D1C8903FCFFC1AE33151B17BDE87EC732E86E70E5EA7F25` |

Different source-name/encoding/build metadata produces different artifacts; do not reuse T02 or pre-normalization hashes as the checkpoint's binary identity. Native125 catalog PNGs belong to the first boundary, with identical product behavior; no final-package equivalence is asserted.

The 226 selected invocations reconcile as: Refinement13, Layout11, FoundationShell16, Catalog14, GoodsReceiptControl35, InlineEditor15, LookupBinding92, LookupTyping15, InventoryAccessibility14, Session-expiry1. At native125 only the two wide Refinement rows fail. Dark's122 are Binding92 + Typing15 + InlineEditor15. These are targeted invocations rather than total project inventory; no full Desktop/Session/SQL matrix was run.

Local raw results: `TestResults/bcf-t03-focused-native125/focused.trx`, `bcf-t03-lookup-dark-native125/lookup-dark.trx`, `bcf-t03-frame-final-native125/frame.trx`; native100 uses `bcf-t03-focused-native100/focused.trx` and `bcf-t03-lookup-dark-native100/lookup-dark.trx`. Raw outputs and earlier development attempts stay ignored. Source review does not execute GUI or substitute for runtime evidence.


Discovery lists contain 226 focused rows and 122 Dark rows, matching execution; no skipped cases in these contexts. The renamed frame case is a separate 1/1 rerun. All 428 declared lookup invocations record independent observer cleanup verified; all 27 refinement/frame invocations record guarded STA exceptions=0, including the two failed geometry assertions. Child capture results require verified process cleanup before assertion. Final inspection finds no remaining testhost/probe processes; no SQL resources were allocated.

| Local TRX | SHA-256 |
|---|---|
| TestResults/bcf-t03-focused-native125/focused.trx | 56A2B4CD9F259CED3C52A319A003404074D5A771D53247A39E17BF95EBB9977C |
| TestResults/bcf-t03-lookup-dark-native125/lookup-dark.trx | 3186CEB568B0C47A9920FB86B370E86383F40A638692ADD63AD8905CF0A65A6C |
| TestResults/bcf-t03-frame-final-native125/frame.trx | 951B76C873AEF2C37DE8A5ECF3D2D73412194652C996B8B19CEAA06B580FC68B |
| TestResults/bcf-t03-focused-native100/focused.trx | 1C3BCADB0E84A6D5C68A05E272156748BB49ADF1785B67DBD376CEDFD482715F |
| TestResults/bcf-t03-lookup-dark-native100/lookup-dark.trx | EBBAED7323E120FB6FF9D33481EABE7D360F78EBB4287D2B0D3BFD8B3B2C2696 |
