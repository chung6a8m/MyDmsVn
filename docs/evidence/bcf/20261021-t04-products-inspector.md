# BCF-T04 — Products results and inspector

**Status: T04 IMPLEMENTED / focused Windows verification PASS, within the limits below.** Source/test checkpoint `29cd2a1077c8a385c570f4bd3a51b72a88d38357`, from `d62a487`. The Owner explicitly requested T04 after the T03 partial-verification report. This admits Products work without waiving the two unavailable T03 native125 wide captures or accepting its native chrome limits. T05/T06 are NOT STARTED; full visual/Owner/release acceptance remains ungranted.

[Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Design](../../BUSINESS_COMPACT_REFINEMENT.md) · [Acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md) · [References 01/03](../../design-references/business-compact/README.md)

## Changed behavior and boundaries

Products now has one toolbar with title, actual query count, Search, Refresh and Add product. Its read-only result columns are Code, Name, Unit and textual Status; stable entity IDs remain in the row-key projection and accessible Open actions. A selected row has a full-row highlight. The footer shows the actual page and inclusive result range/count. Paging still uses the existing 25-row query page size; the mockup's sample count and numbered buttons do not define new business behavior.

The inspector contains a heading, loaded entity code, bordered textual Active/Inactive/Not saved/No selection badge, basic-information section, all existing fields, Save/Cancel group and separate outlined Activate/Deactivate group. Other catalog kinds retain every field and existing command/validation workflow. No new business field, transport/API contract, DTO, schema, permission, module, SQL consumer or vendor change is introduced. Product Unit was an existing editor/DTO field and is now also visible in the result grid.

At sufficient content width, the grid and 340-logical-unit inspector coexist. Below 880 logical content units, the same SplitContainer/control instances switch to a top/bottom arrangement with an explicit inspector scroll surface. Below 820 units, the toolbar puts search/actions below the title/count hierarchy. All four kinds' native fields and editor commands must fit fully within the inspector after deliberate scrolling. This is reachability, not a claim that all inspector content is simultaneously visible at 944×600.

The catalog ViewModel retains the loaded `SelectedItem` DTO separately from raw edit fields. Selection/save and relevant list results update metadata; confirmed activation updates only the loaded DTO's active flag, including an off-page selection. Refresh, search and paging do not populate raw edit fields from list results. Application-only `WithActiveStatus` projections preserve the existing DTO fields for all four kinds. New/create semantics remain explicit; Cancel keeps its existing meaning of canceling work, not discarding an edit. Busy/list/editor-load fields remain disabled by the existing policy. Raw text/native handle/focus/selection retention is proved for theme/resize; no focus-retention claim is made for explicit Refresh/Paging, which temporarily disable those native fields.

SourceGrid's selection stores row indexes. The app clears an obsolete selection before projecting changed result identities and restores only a surviving key. The app-owned read-only result subclass uses the existing public virtual `Focus(bool)` seam to suppress native focus acquisition during model projection only; logical keyboard position and selected row still use the vendor Selection implementation. Ordinary keyboard/mouse focus, accessible row actions, read-only cells and the pinned grid remain authoritative. The entire selection seam is application-owned; no vendor internals are patched.

Vendor candidates remain BootstrapSourceGrid `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, Bootstrap5WinFormUI `2be9a4d778fda7016207b75470020be6126f2f3f`, SourceGrid `f4e457b43582bf01892f50bdc74aa480531e5944`. Their [upstream provenance/draft PR status](20261021-t03-chrome-followup.md) is unchanged. Frozen T02 package/input evidence is preserved and is not relabeled as verification of this candidate.

## RED/GREEN and independent review

- Baseline CatalogControlTests: 14/14 PASS. Seven new composition/command/four-kind rows then fail the intended missing presentation assertions, with clean guarded STA completion. First implementation: 21/21 PASS.
- Stronger selected-row and off-page activation assertions: 3 intended RED, then 21/21 PASS. Product column composition: 2 intended RED, then 21/21 PASS.
- Visual inspection reveals a partly clipped Deactivate action at 944×600. The initial intersection assertion was insufficient. Full containment fails in all four composition rows: inspector client 695×251, button Y228/height32; a fill/scroll TableLayoutPanel under-reports the final nested command row's extent. A dedicated scroll Panel around preferred-height content fixes full containment. The final button is Y219/height32 in that same 251-unit client; it is wholly reachable.
- A fresh read-only reviewer identifies two Important selection findings: an index highlight can move to a different entity after filtering/paging, and clearing ActivePosition loses keyboard continuation after opening a non-first row. Separate one-row filter and row10→Down→row11 cases fail before correction. Both are repaired in one fix pass. An initial plain Selection.Focus attempt fails because SourceGrid invokes native Grid.Focus while the grid is disabled; the read-only projection seam described above preserves logical state without acquiring native focus. The final focused catalog scope is 26/26 PASS before the final fixture-only capture/context correction.
- An overbroad development assertion expecting native editor focus after explicit Refresh fails against the retained busy/disabled-field policy. It is not reported as a new product defect or weakened into layout proof: the test explicitly limits that scenario to raw-edit/keyboard-position retention; separate native theme/resize text/range/focus assertions remain intact.
- Compilation errors while wiring typed metadata delegates are setup failures, not valid RED. Local diagnostic TRX/logs are retained under ignored `TestResults/bcf-t04-*`.
- A final temporary font diagnostic initially mistakes the vendor's 9pt default token for the effective drawing font, then incorrectly dereferences nullable `View.Font` (two setup exceptions, not product RED). The pinned view deliberately sets Font=null; public `GetDrawingFont(grid)` inherits the application's 10pt Grid.Font. Corrected Light/Dark diagnostics both pass without any production change. The temporary assertion is removed, source is restored exactly to 29cd2a1, and rebuilding restores the byte-identical frozen test DLL and regenerates its matching PDB (test DLL 7AC7006C…E8CE). These diagnostics are separate from the frozen counts below.

**Deferred Minor:** the generic ViewModel's existing `ErrorMessage` can retain a list failure after a later successful Refresh. T04 exposes it in an alert region; the data/actions recover, but the old message may remain. A future focused repair should distinguish query errors from still-relevant editor validation, rather than clearing all errors on any successful list load. No mandatory case is silently waived by recording this limitation.

The reviewer did not run GUI/SQL or certify package/native125/Owner acceptance. Those exclusions are appropriate evidence boundaries, not additional PASS claims. Known T03 hardware/chrome limits remain explicit. No second review substitutes for the new RED/GREEN cases and final affected verification.

## Frozen verification

Windows NT 10.0.26200, SDK 8.0.425 selected by global.json, net48/x86 test host. The frozen context round uses one unchanged product/test DLL set built from 29cd2a1, actual measured Windows scale, serialized guarded STA and bounded owned capture/lookup observers. Native100 measures monitor 96 / window 96 DPI; after the human scale change, native125 measures monitor 120 / window 96 DPI. No SQL is allocated and no package, actual Login or attended catalog observation is claimed.

| Frozen invocation | TRX definitions / executed / passed | Result |
|---|---|---|
| Native100 affected GUI, Light declaration; parameterized shell/catalog cases cover both themes | 240 / 240 / 240 | PASS |
| Native100 extra Dark lookup/binding/inline | 122 / 122 / 122 | PASS |
| Native125 affected GUI, first limited selector | 234 / 234 / 234 | PASS within that selector |
| Native125 two fitted legacy layout rows omitted by the first selector | 2 / 2 / 2 | PASS; not full-wide capture proof |
| Native125 extra Dark lookup/binding/inline | 122 / 122 / 122 | PASS |
| Focused Session catalog/query/message-to-open-lookup behavior | 43 / 43 / 43 | PASS |

Every executed invocation above has zero failed, skipped/not-executed, aborted or timed-out rows. The native125 first predicate `DisplayName!~1680` was too broad: it excluded two existing fitted receipt-layout cases as well as the four strict shell/Products capture cases. Source inspection and an explicit two-row completion run repair that coverage omission without rerunning the 234 passes. The union is 236 runnable affected rows. The two legacy cases explicitly fit to the monitor; their PASS cannot certify a1680×900 logical client. Four strict capture rows remain unselected/NOT TESTED: Light/Dark `Shared_shell_capture_preserves_catalog_identity_and_reachable_session_at_logical_viewports` and Light/Dark `Product_catalog_composition_matches_reference_in_both_themes`, each with width1680.

Final `--list-tests` with the corrected selectors independently lists 240/236 cases, reconciled against the native100 TRX and native125 234+2 union. The full 240 classes are Refinement27, FoundationShell16, Layout11, Catalog14, GoodsReceiptControl35, InlineEditor15, LookupBinding92, LookupTyping15, InventoryAccessibility14 and named Session-expiry1. Native125 removes only four strict Refinement rows. Dark122 is Binding92 + Typing15 + Inline15. Session43 is the three named catalog/lookup/product-options ViewModel test classes. These are focused scopes, not complete Desktop/Session project runs.

The four lookup contexts contain428 independent `OBSERVER_CLEANUP=verified` records (107 each). The two affected GUI context sets contain 50 clean refinement STA records (27+23), including all 12 new T04 rows at native100 and ten runnable new rows at native125. Owned capture/window probes verify cleanup separately. No testhost or MyDmsVn probe remains after execution. No frozen T02 package/evidence is modified. The original checkout and pinned vendor source are untouched.

| Frozen test-host artifact | SHA-256 |
|---|---|
| MyDmsVn.Desktop.Tests.dll | `7AC7006C2BBDE932AA61212ADB5D0D74C2DFE1D41171A6A4892172705261E8CE` |
| MyDmsVn.Desktop.WinForms.dll | `167553A47CDEB1291F04081ED40EFC345B0B1AD74CB2B908E8F64CA8A080A172` |
| MyDmsVn.Desktop.Application.dll | `D62888F058D7CA0A96553E68FF8F9E5EC588A18D57BEB28E0EBE876F9A95C540` |
| MyDmsVn.BootstrapSourceGrid.dll | `D37A6195D6B090061B22AF526FBCF8A58192BD74A99BC422D49AA80DCA3149FD` |
| MyDmsVn.Bootstrap5WinFormUI.dll | `10256548B0F967F94162B9ADE431DD630F54201280F576DE35D023DB1503788A` |
| MyDmsVn.Desktop.Session.Tests.dll | `A3A798B93A536625616A7A2B1A59036F5BC2229A5EB83BF470E3E4066266FECD` |

No rebuild occurs between the two native GUI contexts. Hashes are verified unchanged afterward. Session has its separate test DLL and the same application source. Raw TRX and discovery/log files remain local/ignored; no SQL identifiers or connection strings are committed.

| Local TRX under TestResults/ | SHA-256 |
|---|---|
| bcf-t04-frozen-native100/desktop.trx | `3305285942CA86CFC175FB84522B276F2A2292872F2A0AD1BD178EC3B7D67437` |
| bcf-t04-frozen-dark-native100/lookup.trx | `7D916BF10A389640900411530FC74565042058786BE8258734F37EBE463FEA3D` |
| bcf-t04-frozen-native125/desktop.trx | `B6040B1AB645DDD2B1A211D7EB133C9FFCAFF8410573C491D7AF93AB695944B7` |
| bcf-t04-frozen-native125-layout-completion/layout.trx | `21DACF112C21FF29A59FA908485D5E21F5A4AD4F001088D2E934B3C175B7F1E5` |
| bcf-t04-frozen-dark-native125/lookup.trx | `C963D3794473EECC1A25F322E83C3D7EFE30FE54CD57905FF9E50E4C6993301C` |
| bcf-t04-frozen-session/session.trx | `E9D89E4E16D37881581A879DA9DC80457E41EB6C0233666BB25264E2DAB9AA55` |

The build command is ordinary `dotnet test tests/MyDmsVn.Desktop.Tests/MyDmsVn.Desktop.Tests.csproj -c Release -f net48 --no-restore -p:PlatformTarget=x86 --filter $scope --logger 'trx;LogFileName=desktop.trx' --results-directory <context-directory> -- RunConfiguration.TargetPlatform=x86`, after locked restore. Subsequent GUI contexts use `--no-build --no-restore` on the verified frozen outputs. Context declarations are `MYDMSVN_BCF_EXPECTED_SCALE=100|125`, `MYDMSVN_BCI_EXPECTED_SCALE` matching, and `MYDMSVN_BCF_THEME=Light` for the affected scope or `Dark` for the extra lookup/inline scope. Independent observers validate the declaration; variables do not simulate scale.

```powershell
$scope = 'FullyQualifiedName~BusinessCompactRefinementTests|FullyQualifiedName~CatalogControlTests|FullyQualifiedName~BusinessCompactLayoutTests|FullyQualifiedName~FoundationShellFormTests|FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests|FullyQualifiedName~GoodsReceiptInlineEditorTests|FullyQualifiedName~GoodsReceiptControlTests|FullyQualifiedName~InventoryAccessibilityAcceptanceTests|FullyQualifiedName~LoginSessionAcceptanceTests.Session_expiry_clears_private_workspace'
# Corrected native125 discovery selector; execution is the 234+2 invocations above.
$limited = "($scope)&((FullyQualifiedName!~Shared_shell_capture_preserves_catalog_identity_and_reachable_session_at_logical_viewports&FullyQualifiedName!~Product_catalog_composition_matches_reference_in_both_themes)|DisplayName!~1680)"
$dark = 'FullyQualifiedName~GoodsReceiptLookupTypingTests|FullyQualifiedName~GoodsReceiptLookupBindingTests|FullyQualifiedName~GoodsReceiptInlineEditorTests'
$layoutCompletion = 'FullyQualifiedName~BusinessCompactLayoutTests.Compact_shell_retains_visible_fields_and_native_focus_order&DisplayName~1680'
$session = 'FullyQualifiedName~CatalogViewModelTests|FullyQualifiedName~CatalogLookupViewModelTests|FullyQualifiedName~GoodsReceiptProductOptionsViewModelTests'
```

Pre-freeze trials are separate: 67/67 initial affected GUI rows, 43/43 Session rows, 239/239 broader GUI rows (the draft selector did not include the named session-expiry case), then 69/69 shell/catalog/session-expiry rows after correcting the synthetic Products navigation selection. These do not replace the final frozen round or imply an unfiltered project PASS.

## Reference comparison and remaining acceptance

The [14 reviewed captures and geometry/integrity register](20261021-t04-catalog-captures/README.md) use 30 synthetic products across two pages, active/inactive rows, a long Unicode name, selected SP001 on page1 with empty search, native Name editor focus and zero grid scroll. Both themes use identical records/selection/page/viewport. Constrained captures deliberately retain an unsaved Unicode edit/range and scroll the inspector to the final command. Count30 is actual fixture query state, not the raster's sample128. Native100 contains 1680×900 and 1200×760 PrintWindow and physical1200 pairs plus 944 reachability. Native125 contains 1200×760 PrintWindow, actual 1500×950 physical pairs, and 944×600 paint-unit/1180×750 physical-client reachability reports. All native125 reports measure monitor 120 DPI; the process remains window-DPI96/unaware.

The actual Products surface now contains the reference toolbar/results/inspector hierarchy, dense Code/Name/Unit/Status results, selected row, loaded-code/status context, grouped commands and paging/count footer. Differences remain explicit: English copy; actual 25-row paging and Previous/Next controls; title/count stacked within one toolbar hierarchy; existing Bootstrap native metrics and underline tabs; bordered textual status rather than the reference's colored pill/dot; both activation actions remain available for an existing selection; platform scrollbars can remain bright in Graphite. No numerical image similarity or pixel-identity claim is made.

Full wide native125 targets remain unavailable on the known 1920×1080 display. They must not be synthesized by bitmap resizing or credited from a clamped window. Reference disposition, complete receipt presentation, shipped-package workflows and frozen integrated Owner acceptance remain T06 obligations. T03 remains OPEN; T04 evidence cannot close its missing captures.
