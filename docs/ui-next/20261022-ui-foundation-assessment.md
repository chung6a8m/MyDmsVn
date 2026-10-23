# SME UI foundation assessment

> Historical assessment — 2026-10-22. Scheduling/proposal status below is preserved for that date. The [2026-10-23 active roadmap](20261023-active-roadmap.md) now selects the bounded work, and its refreshed baseline supersedes old T08/R4 status summaries. These source findings do not imply new runtime verification.

**Date:** 2026-10-22  
**Status:** Source-backed assessment and recommendations; implementation not selected  
**Baseline and continuation:** [UI Next index](README.md)

## 1. Recommendation

Evolve Bootstrap5WinFormUI and BootstrapSourceGrid into a small reusable input/workspace foundation, with MyDmsVn retaining business workflow policy. The current direction has enough useful infrastructure to justify incremental development. The largest gaps are inconsistent integration contracts, field/cell error presentation, and reuse of editing/lifecycle mechanics.

For an SME application, "polished" should mean that operators can enter documents quickly, understand why an action failed, correct the exact field, and move between workspaces without losing data. Theme consistency supports this goal; it cannot substitute for trustworthy input and commit behavior.

| Approach | Benefit | Cost / limitation | Assessment |
|---|---|---|---|
| Repair each screen independently | Fastest isolated fixes | Feedback, validation, disposal, focus and keyboard code continue to diverge | Useful for an immediate bounded defect, insufficient as the long-term rule |
| Shared UI primitives plus application adapters | Reuses proven controls; centralizes generic mechanics; protects business boundaries | Requires a small set of explicit behavioral contracts and cross-repository tests | Recommended |
| Build a comprehensive ERP control suite or replace the grid stack | Broad eventual feature surface | Large compatibility/maintenance scope before current workflows establish demand | Not justified by the present SME needs |

There is no source evidence that the consumed UI controls are behind the inspected upstream implementation: the UI pin and latest main have identical file content. See the [baseline comparison](README.md#exact-inspected-baseline).

## 2. Date inputs: three distinct controls

### F01 — what the application actually uses

`SalesReceiptControl` constructs a `BootstrapDatePicker` for the sale date and plain `DateTimePicker` controls with checkboxes for From/To filters. Goods Receipt also uses `BootstrapDatePicker`. Stock Card has separate UTC date-time filters. Calling every one of these a plain DateTimePicker loses a useful distinction, but the theme concern remains: `BootstrapDatePicker` is itself a native DateTimePicker composition. [Sales source][app-sales-dates] · [Goods Receipt source][app-goods-date] · [Stock Card source][app-stock-date]

The UI library explicitly documents native `BootstrapDatePicker` and custom date-only `BootstrapCalendarPicker` as separate contracts. The custom calendar controls do not replace the native wrapper automatically. Operating-system visual styles can control native calendar colors despite application color assignments. [UI component contract][ui-components] · [Microsoft calendar appearance][ms-calendar]

### F02 — what CalendarPicker can and cannot do

| Control | Existing behavior | Fit for this application |
|---|---|---|
| `DateTimePicker` | Native Windows date/time editor and calendar; optional selection via checkbox | Existing fallback, with OS-owned appearance |
| `BootstrapDatePicker` | Bootstrap outer shell around native DateTimePicker; forwards Value, range, format, checkbox state and native keyboard behavior | Useful compatibility wrapper, does not guarantee a fully custom-themed editor/calendar or expose every native property |
| `BootstrapCalendarPicker` | Painted themed summary, nullable `SelectedDate`, single/range/multiple selection, bounds, validation border, calendar popup | Suitable for date-only calendar selection and optional filters; not a rapid free-text date editor |
| Proposed typed date input | Pending text plus parsed/committed date, partial-date policy, segment keyboard editing, themed calendar composition | Recommended target for frequent document-date entry; new behavior requires implementation |

The consumed CalendarPicker accepts `DateTime? SelectedDate` and `ClearSelection()`. Its collapsed keyboard handler opens the calendar with Enter/Space/F4/Alt+Down; it has no editable text/parser pipeline. Calendar keyboard navigation exists inside the popup. Do not describe the control as lacking keyboard support altogether. [CalendarPicker source][ui-calendar-picker]

For the existing sale date, which is date-only, there is no demonstrated fundamental blocker to adopting CalendarPicker for calendar selection. From/To nullability is also representable, although the application must bind its selection events and provide a clear/reset interaction instead of assuming native `Checked` behavior. The requested quick typing of day/month is a separate missing capability.

### Why the current choices occurred

Source and documentation establish these facts:

1. The application inherited a native-wrapper date path and uses native optional-filter semantics.
2. Current SAL/BCF instructions specify grid adapters, raw-input safety and navigation, but do not prescribe CalendarPicker for every date-only field or a common date-control selection policy.
3. CalendarPicker lacks the advanced typed-date behavior now requested.

This supports a combination of an integration-guidance gap and a genuine typed-input gap. It does not prove the internal reasoning of the Codex run that authored the screen. There is no verified requirement forcing plain DateTimePicker for these sales filters.

Recommendation: define a date-input selection policy and develop typed date-only entry with a shared custom calendar. Keep explicit date-time/UTC cases separate. Do not globally replace every DateTimePicker or change the meaning of Stock Card UTC filters.

## 3. Feedback: scope and presentation need a common contract

### F03–F04 — reusable primitives exist, routing is inconsistent

`DesktopNotification` stores only `Kind` and `Message`. The notification center retains one `LastNotification`; `FoundationShellForm.OnNotificationPublished` calls `SetStatus`, replacing its status text. It cannot distinguish which tab/document owns a message. [Notification contract][app-notifications] · [Shell subscriber][app-shell-notification]

The application is not entirely Shell-only: Catalog has a local `_error` label, Goods Receipt has `OperationStatusLabel`, and Sales selects one message from inline/editor/list/local state for its own label. Login already uses `BootstrapAlert`. These are useful existing pieces but do not form a uniform feedback policy. [Catalog presentation][app-catalog-state] · [Sales presentation][app-sales-state] · [Login Alert][app-login-alert]

The library already has:

- `BootstrapAlert`, suitable for persistent, contextual messages.
- `BootstrapToast` and `BootstrapToastContainer`; the container is a Panel that can be hosted within a tab and owns queueing/toast lifetime.
- `BootstrapToastService`, whose `relativeTo` parameter selects a screen for a top-level host. Passing a TabPage does **not** make this service tab-scoped.

Use the existing container as the rendering primitive for tab feedback. Add scope/routing/composition in the application rather than developing another toast engine. After a successful `ShowToast`, the container owns the Toast, including disposal; callers must not dispose, reparent/remove, or manually change its visibility. Hiding the container pauses Toast lifetime; unread state and retention policy remain application concerns. [Toast container][ui-toast-container] · [Toast service][ui-toast-service] · [Hidden-host contract][ui-toast-hidden]

### Recommended feedback policy

| Situation | Primary presentation | Lifetime / interaction |
|---|---|---|
| Save succeeds | Tab-local success toast; saved/dirty state in the document header | Passive; no focus theft; no per-keystroke toast |
| Header/cell validation failure | Invalid state, readable message, error summary when needed | Remains while relevant; focus the first error after an explicit failed command |
| Duplicate Code | Invalid Code editor plus field message; concise contextual summary | Preserve attempted input; retain field error until edited/revalidated |
| Permission denial | Tab-local persistent Alert; command state reflects current authorization | Keep context; app security still enforces the boundary |
| Insufficient stock / stale version | Contextual Alert; attach fields/cells only where the response identifies them | Provide the permitted review/reload action |
| Unknown Create/Post outcome | Persistent recovery Alert plus existing mutation lock | No auto-dismiss implying resolution; no automatic retry |
| Background lookup/list query | Local progress/status at the affected area | Usually no toast and no whole-tab blocking overlay |
| Sign-in/session/connectivity state | Shell/global presentation as appropriate | Global events remain global |

A proposed application `WorkspaceFeedbackHost` should compose content, Alert region, a ToastContainer within a bounded notification region, and optional progress presentation. A full-size Panel must not unintentionally cover or intercept the entire document. `MaximumVisibleToasts` limits visible items, not the entire queued backlog; the app must bound retention separately. Each async result must carry or be delivered through a workspace/document/session identity and operation generation. An event from a closed tab or old document must not surface on a new workspace instance that reuses the same module/document key.

An inactive tab should retain actionable errors and optionally show an unread/error marker. Successful transient events should have a bounded retention policy. Do not cover a busy operator's current tab with an unrelated tab's toast. A compact recent-message list may be added if observed use requires it; it is not necessary to duplicate the global service's notification-center implementation immediately.

## 4. Validation: fix the complete error path

### F05 — duplicate-code root cause

The product handlers catch uniqueness conflicts and return `Error.Conflict("Product.DuplicateCode", ...)` without Field metadata. Other catalog handlers follow the same pattern. `ApiResponseMapper.GetJsonFieldPath` returns null when metadata lacks `Field`; `DesktopViewModelBase.ApplyApiError` only groups details with a nonempty field path. `CatalogControl.ApplyErrors` consequently has no Code field error to display. [Conflict origin][app-product-conflict] · [Mapper][app-error-mapper] · [ViewModel][app-error-application] · [Control][app-catalog-errors]

This is an end-to-end mapping gap, not an ErrorProvider rendering defect. Recommended fix: attach stable field metadata to recognized domain conflicts (for example `Code`, converted to JSON field `code`) while retaining Conflict classification. A temporary explicit mapping from known error codes to fields can live in the app adapter when necessary. Do not infer field identity by matching human-readable message text.

Do not map every conflict to Code: rowversion conflicts, changed Product Unit, stock shortages and uncertain outcomes need different handling. Authorization responses intentionally omit field details and must remain general safe feedback.

### Existing invalid-state support is underused

`BootstrapTextBox.ValidationState` already supports None/Valid/Invalid; formatted and lookup controls inherit the input foundation, and CalendarPicker has its own validation property. The inspected Desktop WinForms source contains no assignments to `ValidationState`. Catalog currently applies ErrorProvider; Sales applies its inline error to the whole LineGrid. [Input state][ui-text-validation] · [Catalog errors][app-catalog-errors] · [Sales errors][app-sales-state]

Use a proposed field-validation binder to drive all relevant presentations from the same error collection:

1. Editor border/state plus a readable field message.
2. ErrorProvider as a supplementary cue for normal form controls.
3. Persistent cell/row errors for grids, including cells whose editor is no longer active.
4. A compact accessible summary, with an action to focus the affected editor/cell.

ErrorProvider is control-oriented; setting an error on the grid does not identify a logical Quantity cell. Microsoft also documents that ErrorProvider needs an additional accessible feedback mechanism. Invalid state should therefore not be color-only. [Microsoft ErrorProvider][ms-error-provider]

Keep client parse errors, server field errors and document-level failures distinguishable. Clear an error when its relevant input/version becomes obsolete or successful validation resolves it; do not clear all meaningful errors because an unrelated property changed. A valid-looking field after local editing does not establish that uniqueness or current stock has been accepted by the server.

## 5. Reuse boundaries and control priorities

### F06–F08 — editor mechanisms, not business decisions

The receipt and sales adapters independently implement session identity, raw candidate conversion, editability checks, keyboard routing, projection deferral and disposal. This is concrete evidence for extracting selected mechanics. It does not justify moving Save/Post orchestration, costing, permission interpretation or recovery policy into the UI library. [Goods adapter][app-goods-adapter] · [Sales adapter][app-sales-adapter]

BootstrapSourceGrid already provides text/formatted/lookup editors and a registry. BootstrapDataGridView already has `BootstrapLookupColumn`, with a distinct native DataGridView edit lifecycle. Keep renderer-specific adapters and share behavioral contracts, parsers and test scenarios where appropriate. [SourceGrid editors][bsg-editors] · [DGV lookup][ui-dgv-lookup]

### F09 — candidate components

| Component / capability | Existing foundation | Recommended addition | Ownership |
|---|---|---|---|
| Field validation | `ValidationState`, ErrorProvider, API error details | Field-to-control/cell binder, error summary and focus routing | Generic presentation in UI; domain mapping in app |
| Tab feedback | Alert, Toast, ToastContainer | Workspace scope, placement and retained errors | App composition; reusable presentation seam if proven |
| Closable tabs | BootstrapTabControl + app owner drawing | Close affordance/request, keyboard/accessibility, per-page closability and optional dirty/busy marker | UI renders and requests; app admits/closes/disposes |
| Themed surfaces/text | Theme tokens, Card, app recursive appearance helper | Small themed Panel/Label or token-role extender; consistent typography/spacing | Generic styles in UI; product visual profile in app |
| Busy overlay | Spinner/progress; DataGridView-specific loading panel | Target-Control overlay with message, progress, optional cancel affordance and lifecycle | UI primitive; app operation semantics |
| Decimal entry | FormattedTextBox and strict receipt parser | Information-preserving live display plus typed commit contract | Generic engine in UI; field limits in app |
| Date entry | Formatted text, Calendar, CalendarPicker | Typed date input with quick entry/segments and calendar composition | UI engine; business date policy in app |
| Grid integration | BSG editors and DGV LookupColumn | Strict numeric/date adapters, common error presentation and edit contracts | Respective grid libraries; document adapter in app |

### Closable tabs require more than an X button

The UI control should expose a close request without immediately removing the page. The application evaluates pending editor text, dirty changes, active operations, session identity and uncertain outcomes. An async Save prompt must not run after the control has already disposed the document. All pointer, Ctrl+W, context-menu and accessibility close routes must use the same admission path.

If closure is accepted, cancel queries, invalidate callbacks, detach subscriptions, release editors/notification hosts, then dispose the page and select a surviving tab predictably. Closing while Post is pending is a workflow decision; hiding the overlay or canceling a UI token does not prove a database write was rolled back. Existing enforced sign-out/session clearing remains a separate security path. [Current tabs][ui-tabs] · [Workspace creation][app-shell-workspace] · [Session clearing/disposal][app-shell-disposal]

### Theming and busy scope

`BusinessCompactAppearance` recursively styles panels/labels and uses names/tags to select typography. It also has vendor-child traversal boundaries. Replacing ad-hoc semantic names with explicit surface/text roles would reduce repetition. Avoid a wrapper class for every native layout container: an extender/presenter can cover TableLayoutPanel/FlowLayoutPanel while new primitives cover frequently used surfaces and text. Specify precedence between app typography overrides and vendor-owned semantic palettes. [Application appearance][app-appearance]

`BootstrapDataGridView.Loading` is evidence that spinner/loading building blocks exist, but it is a private overlay for one grid type. A reusable tab overlay should not block unrelated tabs. WinForms transparent backgrounds inherit parent painting rather than showing arbitrary sibling controls underneath. Choose and test an opaque/themed blocked region or a deliberate native overlay strategy; do not promise a translucent backdrop without native rendering proof. [Grid loading implementation][ui-dgv] · [Microsoft transparency behavior][ms-transparency]

Use busy operations with ownership tokens or reference-counted scopes; a first completion must not hide a later operation's overlay. Add a short configurable presentation delay to avoid flashes. Preserve caller-owned enabled state and restore focus only when the originating workspace is still active and restoration remains valid. Handle hide/dispose/DPI, and preserve command guards for keyboard and accessibility paths. A visual overlay alone is not concurrency or authorization enforcement.

## 6. SME scope

Prioritize the workflows already represented by catalogs, inventory, purchases/receipts and direct sales: repeatable keyboard entry, fast lookup, exact quantity/price values, clear errors, trustworthy save/post, useful read-only inspection and stable workspaces.

Useful subsequent scope includes staged Excel-style paste, barcode input, optional column layout persistence, and richer lookup columns when concrete modules need them. These require business-specific rules and are not implied by generic inline editing. Do not add pivot engines, formula languages, arbitrary drag/drop row movement, a complete docking suite, or dozens of wrapper controls merely for future flexibility.

The next documents define the [proposed input/grid contracts](20261022-input-and-inline-editing-contracts.md) and a [candidate implementation order](20261022-candidate-backlog-and-validation.md). None changes SAL's active status or certifies any unexecuted native behavior.

## Source references

[app-sales-dates]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs#L64-L80
[app-goods-date]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/GoodsReceiptControl.cs#L84
[app-stock-date]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/StockCardControl.cs#L223-L248
[app-notifications]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.Application/DesktopNotifications.cs#L13-L49
[app-shell-notification]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs#L493-L496
[app-shell-workspace]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs#L354-L404
[app-shell-disposal]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/FoundationShellForm.cs#L643-L670
[app-catalog-state]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/CatalogControl.cs#L326-L369
[app-sales-state]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/SalesReceiptControl.cs#L295-L337
[app-login-alert]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/LoginForm.cs#L48-L59
[app-product-conflict]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Server.Application/Catalog/ProductFeatures.cs#L197-L207
[app-error-mapper]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Server.Application/ApiResponseMapper.cs#L146-L185
[app-error-application]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.Application/DesktopViewModelBase.cs#L130-L152
[app-catalog-errors]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/CatalogControl.cs#L499-L505
[app-appearance]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/BusinessCompactAppearance.cs#L55-L102
[app-goods-adapter]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/GoodsReceiptLinesGridAdapter.cs
[app-sales-adapter]: https://github.com/chung6a8m/MyDmsVn/blob/9d8a199a27bc505f58b5997fd611ce249c26c00d/src/MyDmsVn.Desktop.WinForms/SalesReceiptLinesGridAdapter.cs
[ui-components]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/docs/COMPONENTS.md#L981-L1054
[ui-calendar-picker]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapCalendarPicker.cs#L106-L273
[ui-text-validation]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapTextBox.cs#L207-L225
[ui-toast-container]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapToastContainer.cs#L185-L225
[ui-toast-service]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapToastService.cs#L301-L335
[ui-toast-hidden]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/docs/COMPONENTS.md#L925-L932
[ui-tabs]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapTabControl.cs
[ui-dgv]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapDataGridView.cs
[ui-dgv-lookup]: https://github.com/chung6a8m/MyDmsVn.Bootstrap5WinFormUI/blob/2be9a4d778fda7016207b75470020be6126f2f3f/src/MyDmsVn.Bootstrap5WinFormUI/Controls/BootstrapLookupColumn.cs
[bsg-editors]: https://github.com/chung6a8m/MyDmsVn.BootstrapSourceGrid/tree/2bd7874f7e80b22ef181763749c2a2938ab7f16e/src/MyDmsVn.BootstrapSourceGrid/Editors
[ms-calendar]: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.datetimepicker.calendarforecolor?view=netframework-4.8.1
[ms-error-provider]: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.errorprovider?view=netframework-4.8.1
[ms-transparency]: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-give-your-control-a-transparent-background
