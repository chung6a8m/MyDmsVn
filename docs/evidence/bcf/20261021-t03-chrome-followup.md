# BCF-T03 — tab chrome and lookup accessory follow-up

**Status: T03 remains OPEN / verification partial.** This follow-up repairs the previously recorded tab caption/band defects and the private vendor lookup accessory paint defect. Required1680×900 logical captures at native125 still cannot fit the available1920×1080 monitor; the Owner confirms no higher resolution is available. No waiver, full visual/Owner acceptance or T04 admission is inferred.

[Original T03 checkpoint](20261021-t03-shell-theme.md) · [Plan](../../plans/20261021-001-business-compact-lookup-and-visual-parity.md) · [Acceptance](../../BUSINESS_COMPACT_REFINEMENT_ACCEPTANCE.md)

## Repair and provenance

Start at `2a60610`; app tab repair is `83bc43270f832901a34315b10da01dc773702606`. The final consumer pin/test checkpoint is recorded after execution. Product remains net48/x86; no Application/API/schema/permission/Post/lookup selection semantics change.

The app-owned tab subclass now redraws only its text-only Primary/Underline caption using the available header width and explicit NoPadding. It fills the native header gaps and nonselected bevels, while preserving the actual expanded selected DrawItem bounds, complete keyboard focus pen and selected underline. Native pages, selection, keyboard, hit-testing and overflow remain authoritative. Other vendor styles/variants/image headers retain their vendor caption rendering.

The earlier logical-width assertion did not establish full caption rendering. A stronger test compares rendered glyph coverage with a full-caption GDI reference and independently checks top/nonselected-bottom bands. Before the repair, Goods Receipts renders196 versus236 reference glyph pixels and native bands are bright; afterward the full glyph coverage is238/236. New Light/Dark rows traverse twelve tabs by native keys, switch theme without replacing the selected page/HWND/focus, and verify both horizontal focus cue pixels. Initial focus checks expose a real paint overlap; the diagnosis also shows selected DrawItem bounds expand beyond GetTabRect, so repainting must preserve the observed paint rectangle. Invalid test-coordinate assumptions and intermediate failures remain local and are not credited as PASS.

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

Pending reconciliation on the final candidate. The original focused selector expands from226 to228 because of the two keyboard/focus/overflow rows. Native100 selects all228; native125 selects226 runnable rows, excluding only `Shared_shell_capture` with width1680. Those two hardware-blocked mandatory captures stay OPEN, not skipped or passed. The additional Dark selector remains typing/binding/inline122 rows. Every declared lookup case measures actual monitor DPI via the independent owned observer. All three pending-query shell cases also assert the real accessory's rendered surface and chevron while retaining raw text/range, native EDIT, focus and committed state.

Source, DLL/TRX hashes, actual outcomes and reviewed captures are reconciled below after execution. No SQL is selected/allocated, no T03 actual Login/shipped package or attended gate is claimed, and frozen T02 source/package/evidence remains unchanged.

## Remaining limits

The native DateTimePicker field/calendar chrome, status ComboBox drop arrow, FlowLayoutPanel/SourceGrid scrollbars and native overflow arrows can remain light under the platform theme. Their existing native interaction is retained; no undocumented OS dark-mode API, grid replacement or broader vendor upgrade is introduced. The old missing-chevron and truncated-tab/band observations belong to the original checkpoint and are superseded only by this follow-up's evidence. Catalog toolbar/inspector composition remains T04 and receipt List/Detail composition remains T05.

Wide native125 capture evidence remains blocked by the real display. Full BCF comparison/Owner acceptance and operational release prerequisites remain unaccepted. Review found one Important focus-paint overlap; its fix is gated by the new pixel RED/GREEN and affected suite. No further runtime finding was reported for the upstream seam.
