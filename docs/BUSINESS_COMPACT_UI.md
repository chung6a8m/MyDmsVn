# Business Compact UI and inline Goods Receipt design

**Direction:** approved; frozen Business Compact UI acceptance is COMPLETE / OWNER ACCEPTED on 2026-10-21. The acceptance ledger records the completed T00 proof, T01 scalar parser, T02 projection/session foundation, T03 control-integration and T04 editable native-accessibility scopes. T04 includes a focused clean-package probe; T05 compact layout has hosted native 100%/125% evidence ([record](BUSINESS_COMPACT_ACCEPTANCE.md#86-bci-t05-business-compact-layout--2026-10-20)). T06 completes frozen UI/package acceptance; [final signed scope](BUSINESS_COMPACT_ACCEPTANCE.md#final-t06-acceptance-and-handoff--2026-10-21) records the accepted candidate and limitations. Operational release prerequisites remain unaccepted/unscheduled.
**Plan:** [BCI execution plan](plans/20261020-001-business-compact-inline-goods-receipt.md).
**Acceptance:** [Business Compact acceptance](BUSINESS_COMPACT_ACCEPTANCE.md).
**Planning source baseline:** `v1a-pro` at `f37038b250821a538b9cdc10a6d6d8b4bc1f386f` (2026-10-20).
This specification introduces no runtime PASS, completed implementation task, or production-release approval.
Historical GUI-P5 evidence retains its original candidate, scope, signatures, and exceptions.

## 1. Outcome and scope

Deliver a compact native Windows Forms shell and a Goods Receipt workspace with real controls inside the active line cell.
Reuse the existing application workflows and typed line values; preserve their server-authoritative validation and permissions.
The normal implementation uses BootstrapSourceGrid, BootstrapLookupBox, and BootstrapFormattedTextBox on `net48`.
The visual proposal is a layout reference, not an additional business contract or a promise of pixel-identical browser rendering.

- In scope: shared navigation/header/workspace styling, Goods Receipt layout, inline line editing, keyboard behavior, and accessibility.
- Preserve existing fields: receipt number/date, warehouse, employee, note, status/version, product, quantity, unit cost, and totals.
- Preserve search, paging, explicit Add/Remove line actions, historical display names, and current notification/error paths.
- Other catalog screens may inherit shared shell/theme changes; redesigning their content is outside BCI.
- No new supplier/tax/discount/unit fields, business calculations, API/DTO contracts, database migrations, persistence changes, or `net8` reactivation.
- Keep existing English UI copy unless a separate localization change defines its labels and acceptance scope.
  Vietnamese text in the conceptual proposal does not authorize wholesale localization.

## 2. Verified planning starting point

The application [project graph][app-project] references BootstrapSourceGrid through the pinned submodule.
The integration [project graph][integration-project] references both pinned vendors; BCI does not silently advance these pins.

| Dependency | Pinned revision |
|---|---|
| MyDmsVn.BootstrapSourceGrid | `09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f` |
| MyDmsVn.Bootstrap5WinFormUI | `aba102e33c48937fd92468791c287afda0a59e77` |
| SourceGrid | `f4e457b43582bf01892f50bdc74aa480531e5944` |

At the planning baseline, Goods Receipt lines were plain cells, with product/quantity/unit-cost controls in a panel below the grid. T03 has replaced those paths in [GoodsReceiptControl][app-lines] with the adapter; see [current integration evidence](BUSINESS_COMPACT_ACCEPTANCE.md#84-bci-t03-lookup-and-command-integration--2026-10-20).
The retained result-list accessibility bridge exposes read-only cells plus an Open row action; the line grid has the T04 writable provider and T05 physical screen bounds conversion. [Source][action-grid]
The integration already supplies real lookup and formatted-text editor adapters; their construction and ownership are reusable. [Source][editor-registry]
The current accessible numeric wrapper derives from BootstrapNumericBox, not BootstrapFormattedTextBox. [Source][accessible-number]
These facts justify an application integration task; they do not establish that inline editing or its accessibility is already complete.

## 3. Business Compact visual contract

Use a restrained navigation area, a clear workspace title, compact labeled fields, and a dominant line-entry grid.
Keep commands and document state visible without letting decorative cards consume the editing area.
Navigation may use navy with a light workspace; errors, disabled states, focus, and selection must remain distinguishable in each supported theme.
Use semantic colors and shared typography/spacing; app-specific tokens must be explicitly owned rather than scattered through controls.

| Region | Initial layout target at 96 DPI | Behavior when constrained |
|---|---|---|
| Navigation | Expanded width 224; collapsed width 64 | Preserve explicit collapse control and accessible item names |
| Shell header | Approximately 48 high, measured against content | Keep identity, theme, and session actions reachable |
| Workspace | Outer inset 16; related-field gap 8 | Reflow field groups; retain native scrolling where necessary |
| Receipt list | Initial split-pane width 260; user-adjustable | Respect measured pane minima; do not overlap the detail pane |
| Metadata | Short labeled rows above lines | Reduce field-group columns before compressing text below its measured size |
| Lines | Product takes remaining width; numeric columns are right-aligned | Allow horizontal scrolling rather than truncating editable numeric text |
| Commands/status | Compact footer, nominally 40-44 high | Wrap/measure commands; errors must not cover controls |

Use Segoe UI 10pt body text, 14pt headings and 9pt secondary text. The initial editable-row target is 36 logical pixels; the sizing rules below may increase it. Exercise logical workspace viewports 944x600 and 1680x900 at 96 DPI, with actual scaled measurements at 100%/125%. Smaller available space must remain reachable through reflow/scrolling.
These are implementation starting values, not existing vendor properties or final measured acceptance results.
The receipt list and detail retain their existing roles; BCI does not introduce a new document workflow.
Keep line columns `#`, `Product`, `Quantity`, `Unit cost`, and `Total`; row-number and total cells remain read-only.
Make active edit, selected row, validation error, and read-only state visually different without relying only on color.
An inactive cell can be painted with an input affordance; a real shared WinForms editor appears when that cell enters edit mode.
Do not instantiate an editor control for every cell or make every input-looking rectangle permanently editable.

## 4. DPI, font, and bounds

All proposed dimensions above are logical 96-DPI measurements; use existing DPI helpers for application-owned geometry.
Measure configured fonts and actual editor preferred/minimum sizes at the current DPI before sizing rows.
For each editable row, choose at least the maximum of:

- the scaled Business Compact row-height target;
- every applicable configured editor's device-pixel preferred/minimum height plus scaled vertical insets;
- the measured text height plus scaled cell padding.

The application owns this row sizing; do not assume the vendor automatically enlarges SourceGrid rows.
Do not rescale a device-pixel preferred size twice or use a smaller painted cell to conceal a clipped editor.
Calculate header, popup, pane, and command bounds from their content and the current monitor working area.
Recompute on supported DPI/font changes while retaining raw edit text, selection/caret, identity, and validation state.
If a popup must close for relayout, closing presentation must not commit or discard its pending value.
Minimum supported viewport and actual physical/logical sizes are recorded in the acceptance evidence.
Insufficient space must produce reachable scrolling/reflow, not hidden Save/Post buttons or overlapping fields.

## 5. Application-owned integration

T01 implements internal `ReceiptNumericInput.TryParse` and `ReceiptNumericField` with scalar tests; see [evidence](BUSINESS_COMPACT_ACCEPTANCE.md#82-bci-t01-lossless-scalar-conversion--2026-10-20). T02 implements `GoodsReceiptLinesGrid`, its adapter and `ReceiptEditIntent` as a tested foundation; see [evidence](BUSINESS_COMPACT_ACCEPTANCE.md#83-bci-t02-stable-projection-and-cell-sessions--2026-10-20). T03 now wires these internal seams into GoodsReceiptControl and removes the external line panel; [focused evidence](BUSINESS_COMPACT_ACCEPTANCE.md#84-bci-t03-lookup-and-command-integration--2026-10-20). T04 supplies writable accessibility. T05 implements BusinessCompactAppearance, BusinessCompactNavigation and ReceiptNumberEditor with explicit ownership; the numeric adapter preserves None-mode raw conversion and keyboard behavior while aligning its protected native input. None is a vendor API; pins are unchanged.
Keep this integration in Desktop.WinForms; do not move grid/control dependencies into Domain or Application.

| App-owned element | Intended responsibility (scope above governs implementation status) |
|---|---|
| `GoodsReceiptLinesGrid` | App-specific editable accessible grid; cell/editor identity, state, and meaningful actions |
| `GoodsReceiptLinesGridAdapter` | Project line models into cells; own edit sessions, event wiring, and registry/editor lifetime |
| `ReceiptNumericInput` | Lossless input inspection and strict decimal conversion for the existing contract |
| `ReceiptNumericField` | `Quantity`, `UnitCost` |
| `ReceiptEditIntent` | `Save`, `Post`, `SelectReceipt`, `NewReceipt`, `Refresh`, `AddLine`, `RemoveLine` |

T02 adapter construction: `(GoodsReceiptLinesGrid grid, GoodsReceiptEditorViewModel editor, Func<bool> canEdit)`.
T02 methods: `TryFinishEdit(ReceiptEditIntent intent): bool`, `CancelActiveEdit(): void`,
`ResetDocument(IReadOnlyList<GoodsReceiptLineViewModel> lines): void`, `RefreshEditability(): void`, and `Dispose()`.
`TryFinishEdit` applies the command policy below; it is not an unconditional commit operation.
The grid remains caller-owned; disposing the adapter must not dispose a caller-owned grid.
The adapter owns its sessions/subscriptions and coordinates disposal of registry-created editors through their documented owner.
Do not double-dispose shared resources, dispose consumer-owned fonts, or share an editor between grid instances.
Normally share one editor per editable column/configuration within the owning grid.
Use SourceGrid's commit/cancel/conversion lifecycle; do not replace the global static editor factory.

## 6. Stable identity and update rules

Each edit session captures document generation, line object reference or stable local key, column, original committed value, and raw candidate text.
Use a persisted identifier for reload mapping only where already available; new lines still require stable in-memory identity.
Never treat a row index as durable identity across removal, refresh, paging, reload, or document changes.
Increment document generation and invalidate old sessions on New, Load, successful reload, access clearing, session end, and disposal.
Validate generation, line membership, editability, and the active field before applying any deferred callback.
An async lookup response must additionally belong to the current query/request generation.
No result from the previous document or removed row may change the new document, even if its row index is reused.

Do not `Redim`, recreate all cells, or replace the active cell during ordinary property/result notifications.
Update unrelated status/totals and affected non-active cells in place; defer conflicting projection updates until the edit boundary.
Preserve raw text, caret, popup/highlight state, and validation errors while the user is editing.
`ResetDocument` is a deliberate document boundary after the command guard has resolved the previous session.
Mutate the line view model only after a successful cell commit; rejected conversion must leave its previous typed value intact.
Totals follow accepted model changes and existing calculation rules, not partially parsed text.
When an operation locks or clears the document, cancel its active session and invalidate callbacks before rebuilding.

## 7. Numeric input and display

The initial path uses `BootstrapFormattedTextBox` with `FormatMode.None` during editing.
That mode is identity formatting, so invalid/intermediate text remains available for validation. [Source][identity-format]
Do not use built-in Numeral sanitization as strict validation: its scale handling truncates strings. [Source][numeral-format]
Capture a read-only snapshot of `CurrentCulture` for the document edit session and use it consistently until that session ends.
Display formatting outside edit must not alter the stored decimal or the application's existing total calculations.

`ReceiptNumericInput` must inspect the complete raw input before decimal conversion:

1. Retain the original text for errors and retry; normalize only explicitly accepted surrounding whitespace for parsing.
2. Accept an ungrouped numeric representation with the captured culture's decimal separator and a permitted leading sign.
3. Reject wrong/ambiguous separators, grouping, exponent/currency syntax, mixed separators, or other unexpected characters; never strip them into another number.
4. Inspect every fractional digit before parsing. Beyond position four, only zero padding is allowed.
5. Accept `1.23000` as the exact value `1.23`; reject `1.23001` and any tiny nonzero fraction beyond allowed precision.
6. Remove only proven insignificant zero padding from the parsing candidate if necessary; never discard a meaningful digit.
7. Parse to decimal with explicit culture/styles, check overflow, and apply existing contract maxima: quantity `99999999999999.9999` (`decimal(18,4)`), unit cost `999999999999999.9999` (`decimal(19,4)`).
8. Require quantity greater than zero and unit cost greater than or equal to zero; reject empty input at commit.

This preserves the existing effective-precision contract, rather than imposing a new maximum raw-string length of four fractional digits.
The server remains authoritative for SQL precision, duplicate products, line limits, active-state rules, and field validation. [Contract](CONTRACTS.md#desktop-client-apis)
Do not rely solely on decimal parsing followed by rounding: the lexical check must detect a meaningful digit that parsing could lose.
Incomplete text such as a sign or decimal separator may remain while typing but cannot be committed as a valid field value.

Use the existing SourceGrid conversion event/validator boundary to reject the candidate before writing the cell. [Source][conversion-boundary]
Set range/null configuration and application conversion rules explicitly; do not depend on a numeric editor's implicit defaults.
A custom `IInputFormatter` is an app-only alternative only when its Format/Unformat round trip preserves candidate information.
The existing adapter's decimal-separator bridge runs only for Numeral mode; None/Custom raw text passes through unchanged. [Source][formatted-adapter]
Therefore the scalar parser and planned editor integration must agree on the captured raw-text culture; no vendor culture patch is assumed.
Validate `vi-VN` and `en-US`, zero padding, wrong/group separators, paste, overflow, and meaningful digits beyond decimal precision.

## 8. Keyboard and command semantics

Commands use one coordinator and `TryFinishEdit(intent)` before executing their existing application action.
Coordinate mouse/focus validation with that intent so native `Validated` does not commit a lookup merely because New was clicked.

| Action | Required behavior |
|---|---|
| F2 or double-click | Begin the selected editable cell only if the current editability predicate permits it |
| Typing to edit | Preserve the first character through the existing editor lifecycle; do not create a new line |
| Enter | Resolve/commit the active editor when valid; do not invoke Save or Post implicitly |
| Escape with lookup open | Close/cancel the lookup's pending presentation first; restore its committed selection |
| Escape with popup closed | Cancel the active cell candidate and retain the committed model value |
| Tab / Shift+Tab | Validate/commit, then move through Product -> Quantity -> Unit cost and reverse, skipping read-only cells |
| Save | Commit valid pending edits, update the model, then run the existing Save workflow; invalid input blocks Save and retains focus |
| Post | Attempt valid pending commit first; if the result is dirty, require an explicit Save and do not Post automatically |
| Select receipt / Refresh | Preserve the existing cannot-abandon-dirty-draft guard; pending data edits cannot be silently lost |
| New receipt | Keep the current dirty-draft guard; cancel query-only pending lookup text when New is allowed; never select the highlighted result implicitly |
| Add line | Finish the current cell successfully, then call the explicit Add action and focus the new line's Product cell |
| Remove line | Identify the target by line reference/key; cancel its active candidate, remove it explicitly, and invalidate its callbacks |

At the last editable cell, use documented native grid/form traversal; do not auto-create a blank trailing row.
New/selection/refresh must treat changed numeric cell text as pending data even before the view model becomes dirty.
The user must Save or explicitly cancel that candidate; a query without a selection is not itself a committed document change.
When a dirty guard rejects navigation, preserve the candidate and existing message/focus rather than rebuilding the grid.
An allowed New cancels pending header and line lookup queries and clears the old document generation. [Existing behavior][new-receipt]
Post still requires a saved ID/current version, a clean document, and the current application/server preconditions.

## 9. Lookup and history

Keep query text, highlighted result, committed `SelectedValue`, and line Product ID as separate states.
Use the existing async lookup view models and their cancellation/stale-result behavior; the UI vendor itself is a local lookup control.
Changing a query or moving highlight must not update the line Product ID or mark the document dirty.
Only a valid selection commit changes the product reference, after checking session/document/line identity.
Bind result updates without resetting the user's pending query when the committed identity is unchanged. [Existing binding][lookup-binding]
Retain selected historical/inactive values and their display names when loading existing receipts.
Restrict new choices according to the existing active-state rules; do not erase historical data because it is absent from current search results.
An empty/unmatched/ambiguous candidate must follow an explicit validation/cancel policy; no silent first-row selection.
Popup mouse selection, Enter, Escape, Tab, outside click, deactivation, and changing documents are separate acceptance cases.

## 10. State, permissions, and failures

Reuse the full current editability predicate: not busy, not read-only, not access-denied-locked, and not `RequiresPostOutcomeReload`.
The existing busy state includes refresh, list loading/busy, and editor busy state. [Source][editability]
Also require the current authenticated session/workspace and a live document/line before accepting an edit or callback.
Re-evaluate at edit start, commit, command execution, and async completion; setting `EnableEdit` once at construction is insufficient.
UI availability does not replace server permission enforcement or authorize a new role-prefetch/permission architecture.
Posted or otherwise read-only documents remain navigable as permitted but have no mutation path through keyboard, mouse, or accessibility. A saved Draft with historical product references remains editable whenever the current permissions/state allow it; historical display data is not an additional read-only rule.
Session expiry, sign-out, access-denied clearing, and disposal invalidate edit/lookup sessions and preserve existing navigation behavior.

Keep uncertain posting outcomes locked: no Save, Post retry, New, line mutation, or stale callback may bypass `RequiresPostOutcomeReload`.
Use the existing explicit reload/recovery path; do not infer success or retry posting from an editor event.
Preserve version conflicts, idempotent posting, server field errors, and current notification/error presentation.
Map `lines[index].field` errors through the current submitted line order and stable line mapping, not a stale visual row index.
Move focus to the actual failing editable cell, keep its raw candidate visible, and make the error available to accessibility clients.
Do not use an unbounded modal dialog for inline conversion errors or leave a partially updated line after rejection.

## 11. Accessibility and extensibility boundaries

`GoodsReceiptLinesGrid` must represent editable cells and the active editor; the existing read-only Open-row bridge cannot be reused unchanged.
Expose row/column context, meaningful names, value/state, validation help, and committed-versus-pending lookup state.
Reuse the intent of current accessible wrappers while preserving native focus, copy, popup, and edit semantics.
Names alone do not reproduce dynamic lookup Help or typed accessible-value validation. [Lookup wrapper][accessible-lookup]
Accessible value changes must pass the same editability, parsing, identity, and commit rules as keyboard changes.

Stock integration adapters are sealed and construct internal sealed controls; they do not automatically instantiate app accessibility subclasses. [Source][lookup-adapter]
First evaluate an app-owned editable grid bridge plus inherited control accessibility metadata with real native UIA evidence.
If exact control-subclass behavior is required, an app-owned EditorControlBase adapter or a narrow additive control-factory seam is possible.
Stock formatted controls also lack public TextAlign; an app custom subclass can use protected Editor.TextAlign, or a narrow additive property can be proposed. [Source][text-editor]
Cell/view alignment alone must not be mistaken for alignment of the active native input.
No library change is mandatory for strict numeric parsing; each proposed library seam needs its own documented benefit and compatibility review.

## 12. Shell customization and conditional grid fallback

The current sidebar uses the global Surface token for its private item host; assigning only outer BackColor does not provide an independent navy palette. [Source][sidebar-theme]
Choose either app-owned native navigation composition or a narrow additive sidebar appearance seam; do not traverse/reflection-patch private vendor children.
Preserve nested navigation, selection, icons, disabled behavior, keyboard access, and session actions when composing the shell.
Theme switching must retain the Business Compact metrics/typography and appropriate contrast rather than resetting a custom design accidentally.

BootstrapSourceGrid is the default and the first native UIA proof target.
A DataGridView fallback is conditional on a documented failed native UIA proof or measured implementation/maintenance cost evidence.
Record the result and obtain an explicit implementation decision in the BCI plan before switching; do not change grids silently.
The pinned UI already has a DGV lookup column/editor; it does not provide an equivalent formatted-text DGV adapter. [Source][dgv-lookup]
A fallback must preserve the same command, numeric, accessibility, history, permission, and uncertain-post contracts and account for its extra adapter work.
Native DGV textbox formatting is a different choice from using BootstrapFormattedTextBox; record that difference explicitly.

## 13. Acceptance boundary

Execute the [acceptance specification](BUSINESS_COMPACT_ACCEPTANCE.md) against the actual changed candidate and supported Windows configurations.
Capture native editor bounds/focus, UIA interaction, light/dark contrast, configured DPI/font sizes, and command/error transitions.
Check adapter sharing/disposal, stale callbacks, document changes, and no active-cell reconstruction while typing.
Preserve current business/session/access protections and perform the affected regression required by [TEST_POLICY](TEST_POLICY.md).
Source inspection, a mockup, or documentation approval is not runtime acceptance; no BCI task is completed by this file's creation.

[app-project]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/MyDmsVn.Desktop.WinForms.csproj#L1-L10
[integration-project]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f/src/MyDmsVn.BootstrapSourceGrid/MyDmsVn.BootstrapSourceGrid.csproj#L29-L32
[app-lines]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L859-L877
[app-line-panel]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L1012-L1028
[action-grid]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/AccessibleActionGrid.cs#L52-L122
[editor-registry]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapSourceGridEditorRegistry.cs#L7-L108
[accessible-number]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/AccessibleNumericBox.cs#L8-L29
[identity-format]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/aba102e33c48937fd92468791c287afda0a59e77/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapFormattedTextBox.cs#L373-L423
[numeral-format]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/aba102e33c48937fd92468791c287afda0a59e77/src/MyDmsVn.Bootstrap5WinFormUI/Formatting/BootstrapNumeralInputFormatter.cs#L75-L138
[conversion-boundary]: https://github.com/chung6a8m/sourcegrid/blob/f4e457b43582bf01892f50bdc74aa480531e5944/SourceGrid/SourceGrid/Cells/Editors/EditorBase.cs#L204-L231
[formatted-adapter]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapFormattedTextBoxEditor.cs#L14-L93
[new-receipt]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L380-L409
[lookup-binding]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L1037-L1051
[editability]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L796-L830
[accessible-lookup]: https://github.com/chung6a8m/MyDmsVn/blob/f37038b250821a538b9cdc10a6d6d8b4bc1f386f/src/MyDmsVn.Desktop.WinForms/AccessibleLookupBox.cs#L6-L15
[lookup-adapter]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/blob/09eab7ad3d6ef0a7fcd27c2b198f7ca573dc4b0f/src/MyDmsVn.BootstrapSourceGrid/Editors/BootstrapLookupBoxEditor.cs#L12-L36
[text-editor]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/aba102e33c48937fd92468791c287afda0a59e77/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapTextBox.cs#L115-L118
[sidebar-theme]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/aba102e33c48937fd92468791c287afda0a59e77/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapSidebar.cs#L944-L965
[dgv-lookup]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/aba102e33c48937fd92468791c287afda0a59e77/src/MyDmsVn.Bootstrap5WinFormUI/Controls/Internal/BootstrapLookupEditingControl.cs#L8-L30
