# BCF-T03 — tab chrome and lookup accessory follow-up

**Status: T03 remains OPEN / verification partial.** This follow-up repairs the previously recorded tab caption/band defects and the private vendor lookup accessory paint defect. Required 1680×900 logical captures at native125 still cannot fit the available 1920×1080 monitor; the Owner confirms no higher resolution is available. No waiver, full visual/Owner acceptance or T04 admission is inferred.

[Original T03 checkpoint](20261021-t03-shell-theme.md) · [Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md)

## Repair and provenance

Start at `2a60610`; app tab repair is `83bc43270f832901a34315b10da01dc773702606`. The consumer source/pin/test checkpoint is `064760f24c53bc86e4ef2c390e7dde04edc85cae`; native100 verifies this runtime/test tree. Native125 subsequently uses test-only `dfd29d2530c0d1dfa49c47570b5005522220d802` for the raster-aware focus oracle described below; the three runtime DLLs remain byte-identical. Product remains net48/x86; no Application/API/schema/permission/Post/lookup selection semantics change.

The app-owned tab subclass now redraws only its text-only Primary/Underline caption using the available header width and explicit NoPadding. It fills the native header gaps and nonselected bevels, while preserving the actual expanded selected DrawItem bounds, complete keyboard focus pen and selected underline. Native pages, selection, keyboard, hit-testing and overflow remain authoritative. Other vendor styles/variants/image headers retain their vendor caption rendering.

The earlier logical-width assertion did not establish full caption rendering. A stronger test compares rendered glyph coverage with a full-caption GDI reference and independently checks top/nonselected-bottom bands. Before the repair, Goods Receipts renders 196 versus 236 reference glyph pixels and native bands are bright; afterward the full glyph coverage is 238/236. New Light/Dark rows traverse twelve tabs by native keys, switch theme without replacing the selected page/HWND/focus, and verify both horizontal focus cue pixels. Initial focus checks expose a real paint overlap; the diagnosis also shows selected DrawItem bounds expand beyond GetTabRect, so repainting must preserve the observed paint rectangle. Invalid test-coordinate assumptions and intermediate failures remain local and are not credited as PASS.

| Dependency | Previous pin | Candidate pin / provenance |
|---|---|---|
| Bootstrap5WinFormUI | `aba102e33c48937fd92468791c287afda0a59e77` | `2be9a4d778fda7016207b75470020be6126f2f3f`, [upstream draft PR72](https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/pull/72) |
| BootstrapSourceGrid | `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` | `2bd7874f7e80b22ef181763749c2a2938ab7f16e`, [integration draft PR17](https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/pull/17) |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` | unchanged |

The lookup's private accessory disables UserPaint while implementing its chevron in OnPaint; the textbox also omits that accessory when applying its palette. Public/protected app/adapter seams cannot repair the private child. The owning vendor patch enables the existing paint path while retaining nonselectability, and forwards the existing editor surface/foreground/invalidation when installing/retheming the framework accessory. No private-child replacement, new API, timer, Font ownership or subscription is introduced. The root BCF instructions conditionally authorize this proven narrow seam; its independent evidence and upstream source exist before advancing the consumer candidate pin. These draft dependency PRs are not merged or release approval.

## Vendor and adapter verification

| Scope | Target | Actual result |
|---|---|---|
| New vendor render rows on old pin | net48/x86 | 6/6 intended FAIL |
| Vendor Lookup/TextBox selector including render, lifecycle and interaction | net48/x86 | 178/178 PASS |
| Same vendor selector | net8.0-windows/x64 | 178/178 PASS |
| New real active-cell accessory rows on old Bootstrap pin | net48/x86 | 2/2 intended FAIL |
| Complete integration project on candidate pin | net48/x86 | 243/243 PASS |
| Complete integration project on candidate pin | net8.0-windows/x64 | 243/243 PASS |

The new vendor six-row matrix covers both themes and editable/read-only/disabled owners, real WM_PRINT surface/chevron, invalidation and stable EDIT handle/focus. The two adapter rows keep SourceGrid editing active with unchanged value/change count across theme switch and explicit cancellation. Tests use existing guarded STA conventions, serialization, ordinary dotnet test/TRX and bounded hang diagnostics; GUI commands are sequential. These vendor compatibility targets do not reactivate the app's parked net8 runtime. Raw outputs remain ignored in the separate upstream worktrees; their committed evidence pages record commands and scope.

## Consumer verification

Native100 final discovery/execution is reconciled: 228/228 focused PASS and 122/122 additional Dark lookup/inline PASS, zero skipped; native125 measures 120 monitor DPI / window 96 and passes 226/226 runnable focused rows plus 122/122 additional Dark lookup/inline rows, zero skipped. The original focused selector expands from 226 to 228 because of the two keyboard/focus/overflow rows. Native100 selects all 228; native125 selects 226 runnable rows, excluding only `Shared_shell_capture` with width 1680. Those two hardware-blocked mandatory captures stay OPEN, not skipped or passed. The additional Dark selector remains typing/binding/inline 122 rows. Every declared lookup case measures actual monitor DPI via the independent owned observer. All three pending-query shell cases also assert the real accessory's rendered surface and chevron while retaining raw text/range, native EDIT, focus and committed state.

[Reviewed captures](20261021-t03-followup-captures/README.md) remain separate from the original checkpoint. Source, DLL/TRX hashes and actual context outcomes are reconciled below. No SQL is selected/allocated, no T03 actual Login/shipped package or attended gate is claimed, and frozen T02 source/package/evidence remains unchanged.

## Remaining limits

The native DateTimePicker field/calendar chrome, status ComboBox drop arrow, FlowLayoutPanel/SourceGrid scrollbars and native overflow arrows can remain light under the platform theme. Their existing native interaction is retained; no undocumented OS dark-mode API, grid replacement or broader vendor upgrade is introduced. The old missing-chevron and truncated-tab/band observations belong to the original checkpoint and are superseded only by this follow-up's evidence. Catalog toolbar/inspector composition remains T04 and receipt List/Detail composition remains T05.

Wide native125 capture evidence remains blocked by the real display. Full BCF comparison/Owner acceptance and operational release prerequisites remain unaccepted. Review found one Important focus-paint overlap; its fix is gated by the new pixel RED/GREEN and affected suite. No further code finding was reported for the upstream seam.

## Frozen consumer artifacts

The net48/x86 runtime build from 064760f is retained for both scales. Native100 uses its original test DLL and exact-RGB focus oracle. Native125 initially exposes two overstrict RGB comparisons; the test-only dfd29d2 correction measures >=3:1 rendered contrast and the expected focus hue (within 15 degrees). This is an oracle correction, not a runtime repair: observed native125 pen pixels are RGB 64/131/249 on Light (3.60:1) and 125/181/249 on Graphite (7.08:1), with the intended hue. The original native100 exact-color passes imply the same contrast/hue requirement and are retained as evidence for that test build, not relabeled as execution of the newer test DLL. The parent test-host dependency binaries are distinct from the earlier upstream standalone builds despite their source pin identity. No package-equivalence claim is made.

| Test-host artifact | SHA-256 |
|---|---|
| MyDmsVn.Desktop.Tests.dll / native100, source 064760f | `031CD4AC585D30FD18692B6C3A95F446621FA304A0FD2D741BE9145169D5281A` |
| MyDmsVn.Desktop.Tests.dll / native125, test-only dfd29d2 | `DD6F6A983C33C0D49DD3C7EF66E64778CF734B91FD90875C33EC7A992D02AD43` |
| MyDmsVn.Desktop.WinForms.dll | `0B18E09C7DEBB7091477BD3D9C3F389F9958CADA0E495C6D07DFBD82A0BB35EE` |
| MyDmsVn.BootstrapSourceGrid.dll | `D37A6195D6B090061B22AF526FBCF8A58192BD74A99BC422D49AA80DCA3149FD` |
| MyDmsVn.Bootstrap5WinFormUI.dll | `10256548B0F967F94162B9ADE431DD630F54201280F576DE35D023DB1503788A` |

The final focused classes reconcile as Refinement15, FoundationShell16, Layout11, Catalog14, GoodsReceiptControl35, InlineEditor15, LookupBinding92, LookupTyping15, InventoryAccessibility14 and Session-expiry1 = 228 rows. At native125, exclude only the two unavailable wide Refinement rows; all other classes retain their selected rows. Dark's122 rows are LookupBinding92 + LookupTyping15 + InlineEditor15. Full app Desktop/Session/SQL and package matrices remain T06 scope.

Development trials are retained separately: band/caption/bottom native RED; initial 31/31 before stronger focus assertions; two-theme focus RED; a partial focus inset fix still failing because selected DrawItem differs from GetTabRect; corrected focused 31-row suite then exposes unfocused second-bevel row x1/y1; final 31/31 passes after the conditional bevel correction. The earlier 228/228 + 122/122 on the candidate pin precedes this last tab-only correction and does not replace the final frozen runs. No failed or invalid trial is silently reclassified.
The focused base selector is the same class/method selector printed in the [original T03 command scope](20261021-t03-shell-theme.md#reconciled-verification), with the newly discovered refinement rows included. At native125 its exact additional predicate is:

```text
(<base selector>)&(FullyQualifiedName!~Shared_shell_capture_preserves_catalog_identity_and_reachable_session_at_logical_viewports|DisplayName!~1680)
```

This omits only the two unavailable strict viewport rows, preserving existing Layout tests even when their display name contains 1680. It does not establish an exact wide-client capture from those other tests. Each ordinary project command uses Release/net48, -p:PlatformTarget=x86, an actual TRX/results directory and RunConfiguration.TargetPlatform=x86. Final context runs use --no-build --no-restore; only the test project is rebuilt for dfd29d2 with BuildProjectReferences=false, followed by explicit equality checks of all three runtime DLL hashes. Context declarations set MYDMSVN_BCF_EXPECTED_SCALE and MYDMSVN_BCI_EXPECTED_SCALE to 100/125, with MYDMSVN_BCF_THEME=Light for the focused set (which contains parameterized Light/Dark shell cases) and Dark for the additional 122. Both variables are admission declarations; independent native measurements supply the actual DPI evidence.

## Final context reconciliation

| Context | Selected/discovered/executed | Actual outcome |
|---|---|---|
| Native100 focused, 064760f test DLL | 228 | 228 PASS, zero skipped |
| Native100 additional Dark, same test DLL | 122 | 122 PASS, zero skipped |
| Native125 focused runnable subset, dfd29d2 test DLL | 226 | 226 PASS, zero skipped;2 mandatory wide captures excluded as hardware-blocked |
| Native125 additional Dark, same corrected test DLL | 122 | 122 PASS, zero skipped |

These final sets have 428 independently verified lookup-observer cleanup records and 28 clean refinement STA records. No testhost/capture-probe process remains after execution; no SQL resources were allocated. The unchanged frozen T02 release manifest still hashes 2738CDC2A8511EE544D5E28CB4C71C007BB9641D75E426F767B9737089B819B6.

The initial native125 run with the old exact-RGB oracle is retained at bcf-t03-followup-frozen-native125/desktop.trx:224 PASS / 2 FAIL, zero skipped, separate from the two unselected hardware-blocked wide cases. The measured contrast/hue diagnosis and 2/2 corrected-oracle probe precede the final 226-row rerun. No runtime DLL was changed to obtain the new PASS. The dfd29d2 test DLL was not rerun at native100; its applicable runtime proof and the stronger original RGB assertions remain explicitly source-specific above, with a new integrated frozen test/package round still required by T06.

| Local final TRX | SHA-256 |
|---|---|
| TestResults/bcf-t03-followup-frozen-native100/desktop.trx | 9BAAEC0A0136E8E035DF7CF4505B7EC6FED331D85FB33CE01985B9E10A2164C1 |
| TestResults/bcf-t03-followup-frozen-dark-native100/lookup.trx | EECBD97D47266C55617F2F75924B9493ECDA46F046542C0014687F14CA5B773A |
| TestResults/bcf-t03-followup-final-native125/desktop.trx | D3CEF700B77B4D965B604096498E4EE9140A77656F2AD8E9B108BBE435E387D2 |
| TestResults/bcf-t03-followup-final-dark-native125/lookup.trx | 205B295D77860DD2AF7F482FF298D3D3A50D69316BE50C29840F4EB9440837EA |
